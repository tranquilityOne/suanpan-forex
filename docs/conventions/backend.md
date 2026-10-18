# suanpan-forex 后端技术约定

| 项目 | 内容 |
|---|---|
| 版本 | v1.16 |
| 日期 | 2026-10-17 |
| 适用范围 | 解决方案中的全部 .NET 项目：Collector、Job、Crm、Portal 四个程序和共享类库 |
| 依据 | data-hub PRD v1.0.0.2（§7.15 技术约束、§10.6 决策日志、§10.10 技术基础）；VVPay `doc/architecture.md`、`doc/backend.md` |
| 相关约定 | [前端技术约定](frontend.md)、[数据库变更约定](database.md) |

> 本文是写代码时必须遵守的约定，记录"怎么做"；"为什么这样定"见 PRD 的决策日志。
> 标有 **【与 VVPay 不同】** 的条目，是本项目有意偏离 VVPay 做法的地方，迁移 VVPay 代码时要特别注意。
> 约定与代码不一致时，以本文为准修正代码；确需调整约定的，先改本文（走评审），再改代码。
> **本文没有覆盖到的情况，不要参考 VVPay 或其他项目自行决定**，先提出问题，确认后补充进本文再实现。

---

## 1. 解决方案结构（单体）

一个仓库、一个解决方案，四个可以分开部署的程序共享同一套类库（PRD DEC-019）。

```
suanpan-forex/
├─ Suanpan.sln
├─ global.json、Directory.Build.props、Directory.Packages.props、nuget.config、.editorconfig    工程配置（§3.1）
├─ docs/
│  ├─ product/                     PRD
│  └─ conventions/                 技术约定（本文所在目录）
├─ src/
│  ├─ Suanpan.Core                 实体、枚举、领域规则（利多利空判定、事件状态机），零依赖
│  ├─ Suanpan.Shared               鉴权基础组件、操作日志特性、分页、消息契约
│  ├─ Suanpan.Data                 DbContext、实体配置、审计字段拦截器、Migrations、对外视图映射
│  ├─ Suanpan.Infrastructure       Redis、RabbitMQ、Hangfire 接入，零业务依赖
│  ├─ Suanpan.Services             业务逻辑（日历、快讯、告警、权限、配置……）
│  ├─ Suanpan.Collector    【程序】盯盘常驻服务、采集执行、重新解析；内含数据源适配器和解析器
│  ├─ Suanpan.Job          【程序】Hangfire 定时任务、快讯生成、告警、RabbitMQ 消费
│  ├─ Suanpan.Crm          【程序】后台（MVC + Areas）
│  └─ Suanpan.Portal       【程序】前台站点（MVC），只读数据库账号
├─ tests/
│  ├─ Suanpan.Core.Tests           领域规则测试
│  ├─ Suanpan.Shared.Tests         鉴权基础组件测试（权限码生成、动态 Policy）
│  ├─ Suanpan.Services.Tests       业务服务测试（连 Testcontainers 启动的 PostgreSQL）
│  ├─ Suanpan.Infrastructure.Tests 基础设施测试（连 Testcontainers 启动的 Valkey、RabbitMQ）
│  ├─ Suanpan.Crm.Tests            后台请求管道测试（在内存中启动 Crm，服务换成假实现）
│  ├─ Suanpan.Data.Tests           数据层测试（审计拦截器、实体配置约定）
│  └─ Suanpan.Collector.Tests      解析器测试（用保存的官方页面快照当样例）
└─ deploy/                         Dockerfile、docker-compose
```

| 程序 | 类型 | 职责 | 部署（第一阶段 → 中期） |
|---|---|---|---|
| Collector | 后台程序（Generic Host，只暴露健康检查） | 盯盘、采集执行、重新解析 | 香港 → 香港（长期） |
| Job | 后台程序（Generic Host，只暴露健康检查） | Hangfire Server、快讯生成、告警、消息消费 | 香港 → 香港 |
| Crm | Web 程序（MVC + Areas） | 运营后台；挂载 Hangfire 面板 | 香港 → 香港 |
| Portal | Web 程序（MVC） | 前台站点 | 香港 → 大陆（连大陆只读副本） |

**【与 VVPay 不同】** VVPay 的 Admin、Worker 对应这里的 Crm、Job；Collector 和 Portal 是新增的程序；VVPay 的 Adapters 类库对应 Collector 程序内部的数据源目录。

### 1.1 模块

全项目统一使用以下 9 个模块名。同一个模块在各项目中的目录名、命名空间、后台的 Area 名都使用同一个英文名。

| 模块 | 目录名 | 范围 | 对应 PRD |
|---|---|---|---|
| 系统 | `Sys` | 账号、角色、权限、操作日志、登录日志、系统配置 | FR-028、FR-029 |
| 基础数据 | `Reference` | 经济体、货币、指标字典 | FR-005、FR-006 |
| 数据采集 | `Collect` | 数据源、采集任务、运行记录、快照、盯盘 | FR-001 ~ FR-004 |
| 财经日历 | `Calendar` | 排期、公布值、事件、预期值、字段锁定、历史回填 | FR-007 ~ FR-012 |
| 快讯 | `Flash` | 模板、生成、审核、汇总 | FR-013 ~ FR-017 |
| 市场数据 | `Market` | 央行利率、参考汇率 | FR-018、FR-022 |
| 新闻 | `News` | 新闻源、采集、去重、翻译、审核 | FR-019 ~ FR-021 |
| 前台发布 | `Publish` | 对外视图、前台缓存刷新、前台冻结 | FR-023、FR-024 |
| 运维 | `Ops` | 告警、质量校验、运营看板 | FR-025 ~ FR-027 |

新增模块前先补充本表。

**模块名不能与 .NET 的根命名空间（`System`、`Microsoft`）同名。** 否则命名空间中会出现 `.System` 这样的段（如 `Suanpan.Services.System`），在这些命名空间内写以 `System.` 开头的完整类型名时会解析到本项目的命名空间，导致编译失败。所以系统模块用 `Sys`（与表名前缀 `sys_` 一致），后台栏目的显示名仍为"系统管理"。

同理，Infrastructure 的 `Hangfire/` 目录对应命名空间 `Suanpan.Infrastructure.Hangfire`，会遮蔽 Hangfire 包的根命名空间：在 `Suanpan.Infrastructure` 及其下级命名空间中，Hangfire 的类型一律用 `using` 引入，不写以 `Hangfire.` 开头的完整类型名。

### 1.2 项目内目录组织

**每个项目内部都按模块、功能或同类分文件夹，不允许把文件平铺在项目根目录或一个大目录下。**

```
Suanpan.Core/
├─ Common/                         跨模块的基础类型（如审计字段基类）
└─ {模块}/                         如 Calendar/
   ├─ Entities/                    实体
   ├─ Enums/                       枚举
   └─ Rules/                       领域规则（状态流转、利多利空判定等）

Suanpan.Shared/
├─ Auth/、Mvc/、Logging/、Paging/、Web/      按功能分
└─ Messages/{模块}/                 消息契约

Suanpan.Data/
├─ SuanpanDbContext.cs
├─ SuanpanDbContext.{模块}.cs       按模块拆分的 partial，只放该模块的 DbSet
├─ Configurations/{模块}/           实体配置
├─ Interceptors/                   审计字段拦截器
├─ Migrations/                     迁移（由工具生成，不手工分目录）
├─ PublicViews/                    对外视图定义与前台只读上下文（PortalReadDbContext）
├─ DesignTime/                     dotnet ef 用的设计时工厂（数据库变更约定 §3.1）
└─ Seed/                           初始化数据

Suanpan.Infrastructure/
└─ Redis/、Messaging/、Hangfire/、Http/      按组件分

Suanpan.Services/
├─ Common/                         跨模块服务（如时区换算、告警发送）
└─ {模块}/                         如 Calendar/
   ├─ IXxxService.cs、XxxService.cs
   ├─ Models/                      Query、Row、Result 等 record
   ├─ Jobs/                        本模块的 Hangfire 任务类
   └─ Handlers/                    本模块的消息处理器

Suanpan.Collector/
├─ Sources/{经济体}/{机构}/          每个数据源的适配器、解析器和原始结构（如 Sources/US/Bls/）
├─ Watch/                          盯盘后台服务
├─ Execution/                      采集执行、重新解析
└─ Handlers/                       消息处理器

Suanpan.Job/
├─ Registry/                       周期任务注册
└─ Program.cs                      任务类和消息处理器放在 Services/{模块}/ 中

Suanpan.Crm/                       目录组织见前端约定 §3.3
Suanpan.Portal/                    目录组织见前端约定 §9.2

tests/
├─ Suanpan.Core.Tests/{模块}/                       与源码目录一一对应
├─ Suanpan.Shared.Tests/                           与 Suanpan.Shared 的目录一一对应
├─ Suanpan.Services.Tests/{模块}/                  与 Suanpan.Services 的目录一一对应；Fixtures/ 放测试数据库，Database/ 放数据库授权的测试
├─ Suanpan.Infrastructure.Tests/                   与 Suanpan.Infrastructure 的目录一一对应；Fixtures/ 放测试容器
├─ Suanpan.Crm.Tests/                             与 Suanpan.Crm 的目录一一对应；Fixtures/ 放启动器、假服务和测试用的页面
├─ Suanpan.Data.Tests/                             与 Suanpan.Data 的目录一一对应
└─ Suanpan.Collector.Tests/Sources/{经济体}/{机构}/  测试代码 + Snapshots/ 快照样例
```

规则：
- 命名空间与目录一致，如 `Suanpan.Services.Calendar`、`Suanpan.Collector.Sources.US.Bls`。
- 跨模块共用的代码放 `Common/`，并按功能继续分文件夹；**不建 Utils、Helpers、Misc 这类没有主题的杂项目录**。
- 一个文件夹里的文件超过约 15 个时，按子功能继续拆分。
- 测试项目的目录与被测源码的目录一一对应。

---

## 2. 依赖方向

```
Collector / Job / Crm  ──►  Services  ──►  Data / Infrastructure  ──►  Shared  ──►  Core
Portal                 ──►  Data（只用对外视图的只读上下文） / Infrastructure（只用消息消费） ──►  Shared ──► Core
```

- 依赖只能单向向下，违反即视为架构问题，代码评审不放行。
- **Core 零依赖**：领域规则（利多利空判定、状态流转、时区换算）收口在这里，不引用 EF Core 和 ASP.NET Core，方便单元测试。
- **Portal 不引用 Services**：前台只读不写（PRD DEC-021、BR-121）。
- **程序项目不直接引用第三方 NuGet 包**（沿用 VVPay）：第三方包都在类库中引用，程序项目只引用项目。
- 不用 `IRepository<T>` 包装 EF Core，不用 MediatR（沿用 VVPay）。

---

## 3. 技术栈

| 层 | 选型 | 说明 |
|---|---|---|
| 运行时 | .NET 10（LTS） | 沿用 VVPay |
| Web | ASP.NET Core MVC（Controller + Razor） | 后台页面不用 Razor Pages / Minimal API |
| ORM | EF Core 10 + Npgsql | **【与 VVPay 不同】** VVPay 为 Pomelo + MySQL |
| 数据库 | PostgreSQL 17 | **【与 VVPay 不同】** PRD DEC-009、DEC-022 |
| 表结构管理 | EF Core Migrations | **【与 VVPay 不同】** VVPay 为手写 SQL，见 §6.6 |
| 定时任务 | Hangfire + Hangfire.PostgreSql | Server 只在 Job 中运行 |
| 缓存 / 锁 / 限流 | Redis 协议（StackExchange.Redis）；服务端用 Valkey 8.1 | 接入方式沿用 VVPay；服务端见 PRD DEC-022 |
| 跨进程消息 | RabbitMQ 4.1（RabbitMQ.Client） | 沿用 VVPay 的事件总线 |
| 校验 | DataAnnotations | 后台表单；本项目没有对外 API |
| 权限 | 自建 RBAC + `IAuthorizationHandler` | 不用 ASP.NET Identity |
| 日志 | 内置 `ILogger`，JSON 格式输出 | Serilog 为可选项 |
| 测试 | xUnit | |

引入新的第三方包前，先确认许可证（PRD §13.2.4），并登记到 SBOM。

### 3.1 工程配置

| 文件 | 作用 |
|---|---|
| `global.json` | 锁定 .NET SDK 版本（10.0.101，允许滚动到更高的特性版本）；`dotnet test` 使用 Microsoft.Testing.Platform 运行器 |
| `Directory.Build.props` | 所有项目共用的编译设置：`net10.0`、可空引用、隐式 using |
| `Directory.Packages.props` | 集中管理第三方包版本 |
| `nuget.config` | 包源只用 nuget.org，不继承本机配置的其他包源 |
| `.editorconfig` | 代码格式 |
| `.gitattributes` | `*.sh` 固定使用 LF 换行（在容器内执行） |
| `.gitignore` | 忽略编译输出、IDE 文件、`.env`、`.env.*`（`.env.example` 除外）、`appsettings.Local.json`、`appsettings.*.Local.json` |

- **编译设置只写在 `Directory.Build.props`**：各项目的 csproj 只写 SDK 类型、项目引用和包引用。个别项目确需不同设置时，在该项目中单独覆盖，并注释原因。
- **包版本只写在 `Directory.Packages.props`**：新增包时在这里加 `<PackageVersion Include="…" Version="…" />`，项目中的 `<PackageReference>` 不写 `Version`。第三方包只加在类库上，程序项目不加（§2）。
- **四个程序都使用 `Microsoft.NET.Sdk.Web`**：Collector、Job 需要对外提供健康检查；Web SDK 自带 ASP.NET Core 共享框架，程序项目不用再引用托管相关的包。
- **编译警告不视为错误**：本地和 CI 一致，不设 `TreatWarningsAsErrors`。
- **代码格式**（`.editorconfig`，只在 IDE 中提示，不影响编译）：
  - UTF-8，4 空格缩进（JSON、XML、csproj、props、YAML 为 2 空格），文件末尾换行，去掉行尾空格（Markdown 除外）；
  - 文件级命名空间，如 `namespace Suanpan.Core.Calendar;`；
  - `using` 放在命名空间外，`System` 开头的排在最前；
  - 私有字段用 `_camelCase`，常量用 PascalCase；
  - 大括号另起一行。

---

## 4. 启动与依赖注入

- 每个模块提供一个注册扩展方法，内聚该模块的全部注册，程序按需拼装：
  `AddSuanpanData`、`AddSuanpanServices`、`AddSuanpanRedis`、`AddSuanpanEventBus`、`AddSuanpanHangfire`、`AddSuanpanCollectors`、`AddSuanpanPortalRead`。
- `AddSuanpanEventBus(configuration, "job")` 的第二个参数是程序名（crm、job、collector、portal），用作接收队列名；`AddSuanpanHangfire(configuration, runServer)` 只有 Job 传 `true`。
- **调用 `AddSuanpanServices` 的程序必须同时注册 Redis**（部分服务依赖 `IRedisCache`，漏注册会在启动时崩溃，VVPay 踩过）。

各程序的注册组合：

| 程序 | 注册内容 |
|---|---|
| Crm | MVC（全局 `OpLogActionFilter` + `AutoPermissionConvention`）、ForwardedHeaders、Data、Services、Redis、EventBus（发布端）、Hangfire（只挂面板，`runServer: false`） |
| Job | Data、Services、Redis、EventBus（发布 + 消费）、Hangfire（`runServer: true`）、任务注册表 |
| Collector | Data、Services、Redis、EventBus（发布 + 消费）、Collectors（适配器、解析器）、盯盘后台服务 |
| Portal | MVC、ForwardedHeaders、PortalRead（对外视图的只读上下文）、Redis（可选）、EventBus（只消费缓存刷新消息）、输出缓存 |

Web 程序的请求管道顺序（沿用 VVPay）：

`UseForwardedHeaders`（必须最前）→ ExceptionHandler / HSTS → HttpsRedirection → IP 白名单（仅 Crm）→ Routing → Authentication → Authorization → 静态资源（`MapStaticAssets`）→ Hangfire 面板（仅 Crm）→ Areas 路由 → 默认路由。

---

## 5. 认证与权限（Crm）

沿用 VVPay 的整套机制：

- **登录**：Cookie 认证（HttpOnly、SameSite=Lax、空闲 30 分钟滑动过期，`Auth:IdleTimeoutMinutes`，PRD BR-142）。**【与 VVPay 不同】** VVPay 为 8 小时。
- **会话失效**：每个请求校验安全戳（60 秒缓存兜底），改密码、重置、停用后旧 Cookie 立即作废。
- **二次验证**：TOTP。密码通过后先签发一个 10 分钟的待完成 Cookie（不带业务权限），TOTP 通过后才签发正式票据（sys 设计 §5.2）。**【与 VVPay 不同】** VVPay 为 5 分钟。
- **密码哈希**：PBKDF2（10 万次迭代）。
- **权限默认拒绝**：`AutoPermissionConvention` 给每个 Area 内的 action 自动加上 `{area}:{controller}:{action}` 权限要求；Controller 标 `[PermMenu]`，action 按需标 `[PermName]`；权限树通过"从代码重建"生成，不手工建权限点。
- 一个账号可以有多个角色，权限取并集（PRD BR-139）。
- **根目录页面默认要求登录**：授权的兜底策略（FallbackPolicy）要求登录，需要匿名访问的 action（登录页、错误页）显式标 `[AllowAnonymous]`。

本项目新增：

| 约定 | 依据 |
|---|---|
| 连续 5 次密码错误锁定账号 15 分钟；管理员可手动解锁；锁定和解锁都记操作日志 | PRD BR-140（VVPay 尚未实现） |
| 登录失败统一提示"用户名或密码错误"，防止账号探测 | 沿用 VVPay 前端纪律 |
| 后台只允许白名单 IP 访问 | PRD BR-141 |
| 界面按权限隐藏按钮，但权限以后端校验为准 | PRD BR-145、BR-152（实现见前端约定） |

Portal 没有登录和权限体系。

---

## 6. 数据访问

### 6.1 实体与映射

- 所有实体都写显式的 Configuration，用 `ApplyConfigurationsFromAssembly` 自动扫描（沿用 VVPay）。
- 列名一律 `snake_case`，通过 `HasColumnName` 显式指定。
- **实体不写导航属性，数据库不建物理外键**，关联列加普通索引，查询时显式 join（沿用 VVPay）。
- 每张业务表都有审计四件套 `created_by`、`created_at`、`updated_by`、`updated_at`，由审计字段拦截器自动填充，业务代码不手工赋值。机器生成的派生表例外。
  - 实体继承 `Suanpan.Core.Common.Auditing` 中的 `AuditedEntity`（四件套）或 `CreationAuditedEntity`（只有创建字段，如关联表）；实体配置中调用 `HasAuditColumns()` 或 `HasCreationAuditColumns()` 映射列。
  - `created_by`、`updated_by` 为 `varchar(64)`，存后台账号的登录名；程序自动写入时存程序名（`collector`、`job`）。操作人由 `IAuditActorProvider` 提供：Collector、Job 用固定的程序名；Crm 注册自己的实现，返回登录名。
  - 新增时只填 `created_by`、`created_at`（非空），`updated_by`、`updated_at` 为空；修改时只填修改字段，创建字段不随修改变化。
  - `ExecuteUpdateAsync`、`ExecuteSqlAsync` 不经过拦截器，需要在 `SetProperty` 或 SQL 中自行写 `updated_by`、`updated_at`。
- `SuanpanDbContext` 只扫描 `Configurations/` 下的配置；`PortalReadDbContext` 只扫描 `PublicViews/` 下的配置。`Suanpan.Data.Tests` 中的约定测试会检查：表名、视图名、列名、主键名、索引名都是 snake_case 且不超过 63 个字符；没有导航属性和外键；枚举存 `smallint`。

### 6.2 表命名

| 前缀 | 模块 | 示例 |
|---|---|---|
| `sys_` | 系统（用户、角色、权限、配置、操作日志、登录日志） | `sys_user`、`sys_op_log` |
| `ref_` | 基础数据（经济体、货币、指标、指标与数据源映射） | `ref_indicator` |
| `col_` | 采集（数据源、任务、运行记录、快照） | `col_task`、`col_snapshot` |
| `cal_` | 财经日历（事件、事件变更记录） | `cal_event` |
| `flash_` | 快讯（快讯、模板） | `flash_news` |
| `news_` | 新闻 | `news_item` |
| `cb_` | 央行利率 | `cb_bank`、`cb_rate_history` |
| `fx_` | 参考汇率 | `fx_rate` |
| `ops_` | 运维（告警、开关、质量校验记录、缓存刷新记录） | `ops_alert` |
| `v_pub_` | 前台对外视图 | `v_pub_calendar` |

Hangfire 的表放在独立的 `hangfire` schema 中。

- 主键、索引的名字由 `SuanpanDbContext` 统一生成，实体配置中不用写：主键 `pk_{表}`，唯一索引 `ux_{表}_{列}`，普通索引 `ix_{表}_{列}`（多列用下划线连接）。全部小写，在 PostgreSQL 中引用时不用加引号。
- 表名、列名、主键名、索引名都不能超过 63 个字符（PostgreSQL 的上限，超出会被截断）。
- `bigint`、`integer` 主键由 EF Core 生成为 `GENERATED BY DEFAULT AS IDENTITY` 自增列。用 `HasData` 写入带固定 id 的预置数据时，要在同一个迁移中把自增序列调到已用的最大 id 之后。

### 6.3 字段类型（PostgreSQL）

| 场景 | 类型 | 说明 |
|---|---|---|
| 时间点 | `timestamptz` | **只存 UTC**，代码中一律用 `DateTime.UtcNow`；Npgsql 会拒绝写入非 UTC 的 `DateTime` |
| 机构当地时间（如官方发布当地时间） | `timestamp`（不带时区）+ 时区列 | 只用于保留原始值，计算一律用 UTC 列 |
| 指标数值 | `numeric(18,6)` | 不用 float / double |
| 枚举 | `smallint` + `HasConversion` | |
| 结构化附加信息 | `jsonb` | 如字段锁定信息、任务参数 |
| 布尔 | `boolean` | |

### 6.4 并发与幂等

- 状态流转用条件更新：`ExecuteUpdateAsync` + `Where(旧状态)`，并检查影响行数（沿用 VVPay）。
- 需要防止并发覆盖的编辑（如日历事件）用乐观锁。
- 写入的幂等靠唯一键兜底，用 PostgreSQL 的 `INSERT ... ON CONFLICT`。**【与 VVPay 不同】** VVPay 用 MySQL 的 `INSERT IGNORE` / `ON DUPLICATE KEY`。
- 批量重算用 `Database.ExecuteSqlAsync`（插值自动参数化），按"先删后插"保证可重复执行。
- 一个操作要多次保存（如先保存主表取得 id，再写关联表），或混用 `ExecuteDeleteAsync` 和 `SaveChangesAsync` 时，放进 `SuanpanDbContext.InTransactionAsync`：全部成功才提交，外层已有事务时直接加入。
- PostgreSQL 的 `LIKE` / `ILIKE` 查询：用户输入先转义 `%`、`_`、`\`，并在 `EF.Functions.ILike` 中显式传入转义字符 `\`（不传时 Npgsql 会生成 `ESCAPE ''`，转义不起作用）。

### 6.5 EF Core 表达式的坑（沿用 VVPay 的教训）

1. `ExecuteUpdateAsync` 的 `SetProperty` 里**不要内联 `DateTime.UtcNow`**，先取到局部变量再传入。
2. 表达式树里不能用 range 运算符（如 `[..256]`），提到语句外面处理。
3. `GroupBy` 投影到带参构造的 record 无法翻译，先投影成匿名类型，落地后再转换。

### 6.6 表结构管理 **【与 VVPay 不同】**

完整规则见 **[数据库变更约定](database.md)**，这里只列要点：

- 表结构只通过 EF Core Migrations 变更，Migrations 放在 `Suanpan.Data/Migrations/`；视图、授权、特殊索引也写在迁移里，不手工改库。
- **预发布和生产环境禁止程序启动时自动迁移**；导出幂等 SQL 脚本，人工审核后用迁移专用账号执行。
- 已在生产执行过的迁移不能修改或删除。
- 每次数据库变更都必须兼容上一版程序，破坏性修改分两次发布（数据库变更约定 §5）。

### 6.7 数据库账号

| 账号 | 使用方 | 权限 |
|---|---|---|
| `suanpan_migrator` | 只用于执行迁移 | 业务表、视图、序列的所有者 |
| `suanpan_app` | Collector、Job、Crm | 业务表读写；操作日志表只有 INSERT 和 SELECT（PRD BR-148）；`hangfire` schema 内可建表 |
| `suanpan_portal` | Portal | 只能 SELECT `v_pub_*` 视图（PRD BR-121），并限制连接数（PRD NFR-014） |

账号本身（包括密码）由运维创建，不写进迁移和代码仓库；授权方式见数据库变更约定 §2。

### 6.8 前台的数据读取

- Portal 使用独立的只读上下文（`PortalReadDbContext`），只映射 `v_pub_*` 视图，所有查询使用 `AsNoTracking`。
- 对外视图只包含对外可见的数据和字段（PRD BR-117），内部字段一律不进视图。
- 前台的连接串通过配置切换，中期改连大陆只读副本时不改代码（PRD BR-119）。

---

## 7. 服务层

沿用 VVPay：

- Controller 保持精简，业务逻辑一律放进 Service。
- 管理操作返回 `ManageResult`（成功或错误，Controller 转成 ModelState 或 TempData）；分页查询返回 `PagedResult<T>`。
- 查询条件用 `record XxxQuery(...)`，列表行投影用 `record XxxRow(...)`。
- 配置键用 static class 集中定义常量，通过 `IConfigService.GetXxxAsync(key, 默认值)` 读取：键不存在时用代码默认值，`sys_config` 中有值时覆盖。默认值也定义为常量，并把配置项登记到该类的 `Definitions`（类型、默认值、分组、说明），系统配置页面才能显示和修改（参考 `SysConfigKeys`）。

---

## 8. 基础设施

### 8.1 Redis

沿用 VVPay 的三个组件，均为 Singleton：

- `IRedisCache`：JSON 序列化，带实例前缀。用 `SetIfNotExistsAsync` 做幂等标记，`IncrementAsync` 做计数。
- `IDistributedLock`：`await using` 获取锁句柄；过期时间必须大于临界区耗时；拿不到锁时返回 null，调用方直接跳过本轮。
- `IRateLimiter`：秒级固定窗口；Redis 异常时放行并告警。本项目用它控制每个数据源的请求频率（PRD BR-003）。

缓存的约定：key 用常量前缀，必须设置明确的 TTL（`IRedisCache` 的写入方法都要求传过期时间），**由写数据的一方负责让缓存失效**。

- 连接时设置 `AbortOnConnectFail = false`：Redis 暂时不可用不阻断程序启动，连上后自动恢复。
- 分布式锁、限流器通过 `TimeProvider` 取时间，测试中可以固定时间。

### 8.2 RabbitMQ

沿用 VVPay：

- 发布：`IEventBus.PublishAsync(message, delay?)`。延时投递用 TTL + 死信分桶实现，不依赖插件。
- 消费：继承 `IntegrationEventHandler<T>`，用 `AddIntegrationEventHandler<,>()` 注册。
- **消费端必须幂等**（重试必然导致重复投递）。失败后延迟重试 3 次（共处理 4 次，间隔 `RabbitMq:RetryDelaySeconds`，默认 10 秒），之后转入 `.failed` 队列等待人工处理。
- 不做 Outbox，靠消费端幂等和对账兜底。

本项目新增：

- 消息契约放在 `Suanpan.Shared/Messages/{模块}/`，这样 Portal 不引用 Services 也能消费。
- 命名：事件用过去式并以 `Event` 结尾，指令以 `Command` 结尾。
- 消息基类 `IntegrationMessage`（`Suanpan.Shared/Messages/`，含 `MessageId`、`OccurredAt`），事件和指令都继承它。**【与 VVPay 不同】** VVPay 的基类叫 `IntegrationEvent`，放在 Infrastructure。
- 交换机 `suanpan.messages`（direct），路由键为消息类型名。**每个接收程序一个队列** `suanpan.{程序}.{消息名}`：不同程序各收一份，同一程序的多个实例分摊处理；重试只回到本程序的队列。**【与 VVPay 不同】** VVPay 一种消息一个队列，所有订阅方抢同一个队列。
- 队列由接收端启动时声明。接收端从未启动过时，发给它的消息没有队列接收，会被丢弃；所以首次部署要先启动接收方（Collector、Job、Portal），再启动发送方。

当前的消息清单：

| 消息 | 类型 | 发送方 | 接收方 | 用途 |
|---|---|---|---|---|
| `CollectTaskDueCommand` | 指令 | Job | Collector | 定时采集到点（PRD FR-002） |
| `CollectTriggerCommand` | 指令 | Crm | Collector | 手动触发采集 |
| `ReparseCommand` | 指令 | Crm | Collector | 重新解析快照（PRD FR-004） |
| `EventReleasedEvent` | 事件 | Collector、Crm | Job | 公布值入库（EV-004），触发快讯生成 |
| `EventHeldForReviewEvent` | 事件 | Collector | Job | 事件进入待复核（EV-005），触发告警 |
| `PublicDataChangedEvent` | 事件 | Collector、Job、Crm | Portal | 对外数据变化，前台让对应缓存失效（PRD FR-023） |

### 8.3 Hangfire

- Server 只在 Job 中运行；Crm 只挂面板，路径 `/Sys/hangfire`，受权限码保护。
- 存储在业务库的 `hangfire` schema（连接串 `ConnectionStrings:Main`，即应用账号），表由 Hangfire 启动时自动建立和升级（数据库变更约定 §2.4）。
- 用依赖注入的 `IRecurringJobManager` 注册任务，所有周期任务集中在 Job 的任务注册表中。
- 任务模板：`[AutomaticRetry(Attempts = 0)]`（周期任务自带下一轮）+ 分布式锁防止多实例重入 + 业务幂等（重跑安全）。
- **cron 一律按 UTC 配置。** **【与 VVPay 不同】** VVPay 使用 `TimeZoneInfo.Local`。周期任务只通过 `AddOrUpdateUtc`（`Suanpan.Infrastructure.Hangfire`）注册，不直接调用 Hangfire 的 `AddOrUpdate`。
- 周期任务最小粒度为 1 分钟。秒级需求（盯盘）不走 Hangfire，见 §9。

---

## 9. 采集（Collector）

- **一个数据源一个适配器**，放在 `Sources/{经济体}/{机构}/` 下。接入新的数据源 = 新增一个适配器目录，加一行注册，主流程不改。
- **先存快照，再解析**：每次请求的原始响应先保存为快照（PRD FR-004），再交给解析器。
- **解析器写成纯函数**：输入快照内容，输出标准化记录，不访问数据库和网络，这样可以直接用保存下来的快照写单元测试。
- 每个数据源使用独立的命名 HttpClient，各自设置超时；User-Agent 带项目名和联系邮箱（PRD BR-005）。
- 请求频率受数据源上限约束，用 `IRateLimiter` 控制（PRD BR-003）；遵守 robots.txt 和数据源条款。
- **盯盘**是 Collector 中的常驻后台服务（`BackgroundService`）：每分钟扫描未来 10 分钟内需要盯盘的事件，用 Redis 分布式锁保证全局只有一个实例在盯（PRD FR-003、DEC-017）。
- 服务器必须开启 NTP 时间同步，时钟偏差超过 1 秒要告警（PRD NFR-113）。

---

## 10. 时间与时区

- 服务器时区统一设为 UTC；数据库只存 UTC。
- 代码中一律使用 `DateTime.UtcNow`，**禁止使用 `DateTime.Now`**。需要在测试中控制时间的地方，注入 `TimeProvider`（`AddSuanpanData` 已注册 `TimeProvider.System`），取 `GetUtcNow().UtcDateTime`。
- 官方发布排期按机构当地时间解析，用 IANA 时区（如 `America/New_York`）换算成 UTC，夏令时由时区库处理（PRD BR-037）。
- 转换成用户时区只在展示层做（后台按操作者时区，前台按访客时区），见前端约定。
- 解析和格式化数字一律使用 `CultureInfo.InvariantCulture`。解析用户输入的小数时不允许千分位（`NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint`），否则把小数点误写成逗号的 `0,25` 会被当成 25。

---

## 11. 日志与操作日志

- **操作日志**：后台的写操作标 `[OpLog("模块", "动作")]`，由 `OpLogActionFilter` 统一写入 `sys_op_log`，权限码自动采集（沿用 VVPay）。
  - 只记录请求参数；密码、密钥、API Key 等字段必须脱敏（PRD BR-147）。
  - 不记录字段修改前后的值；业务上需要的变更历史（改期、利率、模板版本）由各功能自己记录（PRD DEC-018）。
  - 操作日志写入失败时，不回滚业务操作，但要写错误日志并告警（PRD BR-151）。
  - 操作日志、登录日志用一条参数化的 `INSERT` 直接写入，不经过 EF 的变更跟踪，避免把业务操作中未保存的改动一起保存。
- **应用日志**：JSON 格式，带 traceId，贯穿采集 → 入库 → 快讯 → 前台缓存刷新（PRD NFR-050），保留 ≥ 180 天。
- **日志级别**：需要人处理的问题打 Error（如解析失败、缓存刷新连续失败）；依赖抖动、限流等打 Warning。
- **脱敏**：外部响应内容不整段打进日志；凭据在任何日志中都不出现明文。

---

## 12. 配置与密钥

- 基础设施连接串（PostgreSQL、Redis、RabbitMQ）和加密密钥：`appsettings.json` 只放占位值，实际值通过环境变量注入，格式为 `SUANPAN__节__键`；本地开发用 user-secrets。
  - `appsettings.json` 中的密码、连接串、密钥一律留空；不涉密的默认值（如 `Redis:InstanceName`、`Auth:IdleTimeoutMinutes`）直接写在 `appsettings.json`。
  - 各程序在 `Program.cs` 中调用 `builder.Configuration.AddEnvironmentVariables(prefix: "SUANPAN__")`。配置的优先级从低到高：`appsettings.json` → `appsettings.{环境}.json` → user-secrets（只在 Development 环境）→ 环境变量 → 命令行参数 → `SUANPAN__` 环境变量。
  - 四个程序共用一份 user-secrets：`Directory.Build.props` 给 Web SDK 项目统一设置 `UserSecretsId` 为 `suanpan-forex`。
- **仓库里不得出现任何真实密码、连接串和密钥**；CI 做密钥扫描（PRD NFR-033、NFR-134）。**【与 VVPay 不同】** VVPay 的 appsettings 中有明文密码。
- 初始管理员密码在首次部署时通过环境变量设置，不硬编码在种子数据里。
- 业务动态开关放 `sys_config` 表（60 秒缓存，变更后立即失效；键不存在时用代码默认值）。基础设施配置不放 `sys_config`，因为它自身依赖数据库。

配置键（新增配置键时补充本表）：

| 键 | 说明 | Crm | Job | Collector | Portal |
|---|---|---|---|---|---|
| `ConnectionStrings:Main` | 应用账号 `suanpan_app` 的连接串 | ✓ | ✓ | ✓ | |
| `ConnectionStrings:Migrator` | 迁移账号 `suanpan_migrator` 的连接串，只供本地和 CI 执行 `dotnet ef` 使用（数据库变更约定 §3.5），四个程序都不读取 | | | | |
| `ConnectionStrings:Portal` | 前台只读账号 `suanpan_portal` 的连接串；中期改连大陆只读副本时只改这个值（PRD BR-119） | | | | ✓ |
| `Redis:Configuration` | StackExchange.Redis 连接串（含密码） | ✓ | ✓ | ✓ | 可选 |
| `Redis:InstanceName` | 键前缀，默认 `suanpan:`；与其他项目共用 Redis 时靠它隔离 | ✓ | ✓ | ✓ | 可选 |
| `RabbitMq:Uri` | AMQP URI，虚拟主机为 `suanpan`，如 `amqp://用户:密码@主机:5672/suanpan` | ✓ | ✓ | ✓ | ✓ |
| `RabbitMq:MaxRetryCount`、`RabbitMq:RetryDelaySeconds` | 可选：处理失败后的重试次数和间隔秒数，默认 3 次、10 秒 | ✓ | ✓ | ✓ | ✓ |
| `Security:EncryptionKey` | AES-256 加密密钥，TOTP 密钥和数据源凭据共用；32 字节随机数的 Base64（如 `openssl rand -base64 32`），格式不对时启动即报错 | ✓ | | ✓ | |
| `Security:BootstrapAllowedIps` | 后台 IP 白名单的应急放行 IP，逗号分隔 | ✓ | | | |
| `Seed:AdminUsername`、`Seed:AdminPassword` | 初始管理员（sys 设计 §3.4） | ✓ | | | |
| `Auth:IdleTimeoutMinutes` | 后台会话空闲超时，默认 30 | ✓ | | | |
| `ForwardedHeaders:TrustedProxies` | 可信的反向代理，逗号分隔的 IP 或 CIDR；为空时只信任本机回环地址。只采信这些地址传来的 `X-Forwarded-For`（sys 设计 §5.5） | ✓ | | | |

---

## 13. 测试与持续集成

### 13.1 测试项目

- `Suanpan.Core.Tests`：领域规则。Sys 的密码、停用、通配规则；利多利空判定的全部分支、夏令时切换周的时区换算、数据质量校验规则（PRD NFR-133）。
- `Suanpan.Shared.Tests`：权限码生成、动态 Policy 等 Shared 中的组件。
- `Suanpan.Data.Tests`：审计拦截器（用 EF Core InMemory）、实体配置约定（命名、外键、类型）。不连接真实数据库。
- `Suanpan.Services.Tests`：业务服务。用到数据库的测试连真实的 PostgreSQL 17：Testcontainers 启动临时容器，按生产方式建好三个账号，用迁移账号执行全部迁移，测试用应用账号连接（权限与线上一致）；整个程序集共用一个容器。**运行这些测试需要 Docker**（本地 Docker Desktop，CI 用 GitHub Actions 自带的 Docker）。数据库授权的测试（如 Hangfire 用应用账号建表）放在 `Database/`。
- `Suanpan.Infrastructure.Tests`：Redis 组件（分布式锁、限流器）和事件总线（收发、重试后转入 `.failed`、延时投递）。Testcontainers 启动 Valkey 8.1 和 RabbitMQ 4.1，与线上服务端版本一致，同样需要 Docker。
- `Suanpan.Crm.Tests`：后台的请求管道（Cookie、安全戳、默认拒绝、IP 白名单、Hangfire 面板的授权、全局过滤器）和页面框架（布局、公共部件、中文校验提示）。WebApplicationFactory 在内存中启动 Crm，依赖数据库的服务和 Hangfire 存储换成假实现，不需要 Docker；登录 Cookie 用 Crm 自己的加密格式生成，经过真实的 Cookie 校验。测试用的页面放在 `Fixtures/`，只在测试中加载。
- 容器夹具在第一个用到它的测试中才启动容器。xUnit 不论筛选条件都会创建程序集夹具，在 `InitializeAsync` 中启动会让只跑单元测试时也启动容器。
- `Suanpan.Collector.Tests`：每个解析器至少有一组基于真实快照的测试样例；官方页面改版后，把新快照补进样例。
- 每个类库对应一个测试项目（`Suanpan.{类库}.Tests`），需要测试的类库还没有测试项目时，先新建再写测试。
- 测试框架为 xUnit v3，通过 Microsoft.Testing.Platform 运行（`global.json` 中设置），测试项目的 `OutputType` 为 `Exe`。

### 13.2 测什么、不测什么

按风险决定测试的多少，**不设数量目标**：不定覆盖率百分比，也不限制用例数。

| 要测 | 不测 |
|---|---|
| 领域规则的每个分支，在 Core 的单元测试里测全 | 在服务层把规则的各个分支再测一遍 |
| 安全相关的算法：用标准测试向量或已知的攻击样例（如 TOTP 用 RFC 6238 向量、密文被篡改、CIDR 主机位） | 框架和第三方库本身的行为（如 QRCoder 能否生成 PNG） |
| 服务层：每条规则一正一反，证明规则"接上了"；以及依赖数据库的行为：唯一约束、事务、缓存失效时机、授权 | 纯数据类：record、DTO、分页结果这类没有逻辑的类型 |
| 解析器：基于真实快照 | 依赖注入逐个服务：只保留一个冒烟测试（`ValidateOnBuild` 加几个关键服务） |
| 数据层约定：命名、外键、类型（约定测试自动覆盖新加入的实体） | 私有方法和内部实现细节 |

### 13.3 要避免的三类测试

每一类都有一个**判断问题**：写测试、评审、删除测试时用它来判断。

#### 低价值的测试

**判断问题：删掉这个测试，会漏掉哪一类 bug？答不出来，就是低价值。**

| 常见情形 | 例子 |
|---|---|
| 被测对象里没有我们写的逻辑：纯数据类、自动属性、只转发调用的薄包装 | 分页结果的页数计算；二维码确实输出 PNG |
| 测的是框架或第三方库本身的行为 | 测 ASP.NET Core 的授权管道能否工作 |
| 编译器已经保证的东西 | 断言非空属性不为 null |
| 在测试里把实现逻辑重算一遍再比较：失败时只能改测试，发现不了真实缺陷 | 用和实现相同的公式算出期望值 |

**例外**：薄包装涉及安全参数（加密算法、迭代次数、密钥长度）时仍然要测，因为配错的后果严重。判断标准是"有没有我们写的逻辑"乘以"出错的风险"。

#### 重复的测试

**判断问题：两个测试是否会因同一个缺陷失败、在同样的缺陷下都通过？是，就是重复。**

| 常见情形 | 处理 |
|---|---|
| 同一条规则的同一个分支在两层都测了（Core 规则测试和服务测试都测"密码少于 12 位"） | 保留最底层的（最快、最精确）；上层只留"接上了"的一正一反 |
| 同一个 Theory 里，几个输入走同一个分支、命中同一个边界 | 按等价类和边界值，每类只留一个代表 |
| 两个测试的准备和操作完全相同，只是断言分开写 | 合并 |

写法：先列出被测代码的分支和边界，每个分支、每个边界至少一个用例；同一类超过一个的，要说得出理由。

**例外**：跨层的接线测试不算重复。服务层用一正一反证明规则被调用到，和规则自身的分支测试测的是两件事。

#### 脆弱的断言

**判断问题：做一次不改变行为的修改（改提示文案、调整排序、给私有方法改名、换一种实现），这个测试会失败吗？会，就是脆弱。**

| 常见情形 | 例子 |
|---|---|
| 断言用户可见文案的原文 | `Assert.Equal("不能停用自己", result.Error)` |
| 断言实现细节或副作用：内部调用次数、私有状态、日志内容、SQL 文本、受排序影响的计数 | 重建权限树"更新了 2 行" |
| 依赖不确定的因素：当前时间、随机数、没有 ORDER BY 的集合顺序、并行测试可能共享的数据（不带条件的整表查询、固定名称）、`Thread.Sleep` 等待 | 不用 `TimeProvider` 直接取当前时间；名称不带随机后缀，并行时在唯一索引上互相等待 |
| 断言超出需求：整个对象相等（加一个字段就失败）、整段 HTML 或 JSON 比对 | 断言整行记录的全部字段相等 |

**例外**：
- 规则的单元测试里核对一次提示文案；
- 合规要求的固定文案（如前台的"不构成投资建议"）必须精确断言；
- 解析器的快照测试比对的是需求本身，不算脆弱。

### 13.4 硬性规则

违反任一条，代码评审不通过。能自动检查的在 CI 中检查（JAN-53、JAN-56）。

| 规则 | 检查方式 |
|---|---|
| 测试中不直接取当前时间（`DateTime.Now`、`DateTime.UtcNow`、`DateTimeOffset.Now`、`DateTimeOffset.UtcNow`），不用 `Thread.Sleep`、`Task.Delay` 等待；需要时间的地方注入 `TimeProvider` | CI 脚本搜索 |
| 服务层和集成测试不比对提示文案原文，只断言成功或失败以及数据的最终状态（文案在规则的单元测试里核对一次） | CI 脚本搜索 `Assert.Equal("…", ….Error)` |
| 要启动容器的测试类（连数据库、Valkey、RabbitMQ）必须标 `[Trait(TestCategory.Key, TestCategory.Integration)]`；连数据库的在各自的事务里执行、结束时回滚；名称带随机后缀；查询带条件，不依赖整表的行数或内容 | 架构测试：构造函数需要容器夹具（`PostgresFixture`、`ValkeyFixture`、`RabbitMqFixture`）的类必须带这个标签；其余靠评审 |
| 不为纯数据类、第三方库写测试；依赖注入只保留一个冒烟测试 | 评审 |
| Theory 只放有代表性的输入：每个分支或边界一个样例 | 评审 |
| 不断言没有 ORDER BY 的集合顺序 | 评审 |
| 异步调用传入 `TestContext.Current.CancellationToken` | xUnit 分析器 |
| 删除或合并已有测试时，在 PR 中写明依据（属于 §13.3 的哪一类）；拿不准时保留 | 评审 |

### 13.5 评审清单

写测试的人和评审的人，对新增或改动的每个测试回答三个问题：

1. 删掉这个测试，会漏掉哪一类 bug？（低价值）
2. 有没有更低一层的测试已经覆盖了同一个分支？（重复）
3. 做一次不改变行为的重构，它会失败吗？（脆弱）

三个问题都放进 PR 模板（JAN-53）。

### 13.6 分组与运行

- 要启动容器的测试（数据库、Valkey、RabbitMQ）标 `Category=Integration`，其余为单元测试。
- `dotnet test`：全部测试，需要 Docker；提交前和 CI 中运行。
- `dotnet test --filter-not-trait "Category=Integration"`：只跑单元测试，不需要 Docker、不启动容器；本地改代码时用。只跑单元测试时每个测试项目约 2 秒；全部测试约 30 秒，主要花在启动容器上。
- HTTP 层的测试（`Suanpan.Crm.Tests`）不启动容器，不标 Integration，只跑单元测试时也会运行。
- GitHub Actions：构建、运行全部测试、测试规则检查（§13.4）、迁移校验（数据库变更约定 §6）、依赖漏洞扫描、密钥扫描，任一项不通过都不能合并（PRD NFR-134）。

---

## 14. 部署与开发环境

- `deploy/docker-compose.yml` 用于本地联调和第一阶段部署，包含依赖服务 PostgreSQL 17、Valkey 8.1（Redis 协议）、RabbitMQ 4.1（含管理界面，虚拟主机 `suanpan`）；四个程序各有一个 Dockerfile，由 ENG-006.3 加入。
- 首次使用时把 `deploy/.env.example` 复制为 `deploy/.env` 并填写密码，`.env` 不提交。端口只绑定 `127.0.0.1`，与本机已有的服务冲突时在 `.env` 中修改。
- PostgreSQL 首次启动时，由 `deploy/postgres/init/` 中的脚本创建业务库和三个数据库账号（数据库变更约定 §2.2）。脚本只在数据卷为空时执行，修改账号或密码要先删除数据卷（`docker compose -f deploy/docker-compose.yml down -v`）。
- 开发机上已有 Redis、RabbitMQ 时，只启动 PostgreSQL（`docker compose -f deploy/docker-compose.yml up -d postgres`），复用已有的服务；与其他项目共用时，RabbitMQ 使用独立的虚拟主机 `suanpan`，Redis 的键加前缀 `suanpan:`。
- 不依赖只有境外才有的云服务，方便迁移（PRD NFR-120）。
- Windows 本地开发：编译前先停掉正在运行的 Suanpan.* 进程，否则 bin 目录下的 DLL 会被锁住（VVPay 踩过）。
- 本地连接串用 `127.0.0.1`，不用 `localhost`（VVPay 的经验）。
- 本地端口固定（`Properties/launchSettings.json`，只用 http，只绑定 `127.0.0.1`）：Crm 5100、Portal 5200、Job 5300、Collector 5400。
- 从零到本地运行的完整步骤见仓库根目录的 `README.md`。

---

## 变更记录

| 版本 | 日期 | 修改人 | 内容 |
|---|---|---|---|
| v1.0 | 2026-10-17 | vivo.li / Claude | 初版：基于 VVPay 后端框架和 PRD v1.0.0.2 的技术决策整理 |
| v1.1 | 2026-10-17 | vivo.li / Claude | 新增 §1.1 模块、§1.2 项目内目录组织（按模块和功能分文件夹，不平铺）；明确约定未覆盖的情况不参考 VVPay；系统模块名定为 `Sys`，并规定模块名不能与 .NET 根命名空间同名；§6.6、§6.7 的详细规则移到新的数据库变更约定 |
| v1.2 | 2026-10-17 | vivo.li / Claude | 新增 §3.1 工程配置（统一编译设置、集中管理包版本、包源只用 nuget.org、程序项目使用 Web SDK、警告不视为错误、代码格式）；§1 目录树补充解决方案文件和工程配置文件 |
| v1.3 | 2026-10-17 | vivo.li / Claude | §3 写明 PostgreSQL 17、Valkey 8.1（代替 Redis 服务端）、RabbitMQ 4.1（PRD DEC-022）；§3.1 补充 `.gitattributes`；§14 补充 docker-compose 的用法、数据库初始化脚本，以及复用本机已有 Redis、RabbitMQ 的做法 |
| v1.4 | 2026-10-17 | vivo.li / Claude | §12 补充配置的优先级、`SUANPAN__` 环境变量的绑定方式、共用的 user-secrets 和配置键清单（新增 `ConnectionStrings:Portal`、`Redis:InstanceName`）；§14 补充本地端口和 README |
| v1.5 | 2026-10-17 | vivo.li / Claude | 新增测试项目 `Suanpan.Data.Tests`（§1、§1.2、§13）；§6.1 写明审计字段的基类、类型、填充规则和不经过拦截器的情况；§10 补充 `TimeProvider`；§3.1、§13 补充测试运行器 |
| v1.6 | 2026-10-17 | vivo.li / Claude | 新增测试项目 `Suanpan.Shared.Tests`（§1、§1.2、§13）；§13 明确每个类库对应一个测试项目 |
| v1.7 | 2026-10-17 | vivo.li / Claude | §6.2 补充主键和索引的命名方式、63 字符上限、自增主键与预置数据；§6.1 约定测试的检查范围 |
| v1.8 | 2026-10-18 | vivo.li / Claude | §1.2 Data 目录加入 `DesignTime/`；§12 新增配置键 `ConnectionStrings:Migrator` |
| v1.9 | 2026-10-18 | vivo.li / Claude | 新增测试项目 `Suanpan.Services.Tests`（§1、§1.2、§13），服务测试连 Testcontainers 启动的 PostgreSQL；§12 写明 `Security:EncryptionKey` 的格式 |
| v1.10 | 2026-10-18 | vivo.li / Claude | §6.4 补充多次保存用 `InTransactionAsync`、`ILIKE` 的转义写法 |
| v1.11 | 2026-10-18 | vivo.li / Claude | §13 重写：拆为测试项目、测什么与不测什么、写法、分组与运行；连数据库的测试标 Integration，本地可只跑单元测试 |
| v1.12 | 2026-10-18 | vivo.li / Claude | §13 新增 13.3 要避免的三类测试（低价值、重复、脆弱：判断问题、常见情形、例外）、13.4 硬性规则、13.5 评审清单；原 13.4 改为 13.6 |
| v1.13 | 2026-10-18 | vivo.li / Claude | §7 配置项登记到 `Definitions`；§10 小数不允许千分位；§11 日志用 INSERT 直接写入 |
| v1.14 | 2026-10-18 | vivo.li / Claude | ENG-002.6 实施：§8.2 消息基类 `IntegrationMessage`、每个接收程序一个队列、重试次数的含义、首次部署顺序；§8.1 写入必须带过期时间；§8.3 Hangfire 存储位置、只用 `AddOrUpdateUtc` 注册；§12 新增 RabbitMQ 重试配置；新增测试项目 `Suanpan.Infrastructure.Tests`（§1、§1.2、§13）；§13 容器夹具延迟启动，修正"只跑单元测试不启动容器"与实际不符的问题 |
| v1.15 | 2026-10-18 | vivo.li / Claude | ENG-004.1 实施：§4 管道顺序加入 IP 白名单、静态资源用 `MapStaticAssets`；§5 改正会话时长（空闲 30 分钟、待完成 Cookie 10 分钟，原文是 VVPay 的值），新增根目录页面默认要求登录；§12 新增 `ForwardedHeaders:TrustedProxies`；新增测试项目 `Suanpan.Crm.Tests`（§1、§1.2、§13） |
| v1.16 | 2026-10-18 | vivo.li / Claude | §13.1 `Suanpan.Crm.Tests` 也覆盖布局和公共部件 |
