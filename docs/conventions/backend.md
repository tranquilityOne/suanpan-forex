# suanpan-forex 后端技术约定

| 项目 | 内容 |
|---|---|
| 版本 | v1.0 |
| 日期 | 2026-10-17 |
| 适用范围 | 解决方案中的全部 .NET 项目：Collector、Job、Crm、Portal 四个程序和共享类库 |
| 依据 | data-hub PRD v1.0.0.2（§7.15 技术约束、§10.6 决策日志、§10.10 技术基础）；VVPay `doc/architecture.md`、`doc/backend.md` |
| 相关约定 | [前端技术约定](frontend.md) |

> 本文是写代码时必须遵守的约定，记录"怎么做"；"为什么这样定"见 PRD 的决策日志。
> 标有 **【与 VVPay 不同】** 的条目，是本项目有意偏离 VVPay 做法的地方，迁移 VVPay 代码时要特别注意。
> 约定与代码不一致时，以本文为准修正代码；确需调整约定的，先改本文（走评审），再改代码。

---

## 1. 解决方案结构（单体）

一个仓库、一个解决方案，四个可以分开部署的程序共享同一套类库（PRD DEC-019）。

```
suanpan-forex/
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
│  └─ Suanpan.Collector.Tests      解析器测试（用保存的官方页面快照当样例）
└─ deploy/                         Dockerfile、docker-compose
```

| 程序 | 类型 | 职责 | 部署（第一阶段 → 中期） |
|---|---|---|---|
| Collector | 后台程序（Generic Host，只暴露健康检查） | 盯盘、采集执行、重新解析 | 香港 → 香港（长期） |
| Job | 后台程序（Generic Host，只暴露健康检查） | Hangfire Server、快讯生成、告警、消息消费 | 香港 → 香港 |
| Crm | Web 程序（MVC + Areas） | 运营后台；挂载 Hangfire 面板 | 香港 → 香港 |
| Portal | Web 程序（MVC） | 前台站点 | 香港 → 大陆（连大陆只读副本） |

**【与 VVPay 不同】** VVPay 的 Admin、Worker 对应这里的 Crm、Job；Collector 和 Portal 是新增的程序；VVPay 的 Adapters 类库对应 Collector 程序内部的适配器目录。

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
| 数据库 | PostgreSQL | **【与 VVPay 不同】** PRD DEC-009 |
| 表结构管理 | EF Core Migrations | **【与 VVPay 不同】** VVPay 为手写 SQL，见 §6.6 |
| 定时任务 | Hangfire + Hangfire.PostgreSql | Server 只在 Job 中运行 |
| 缓存 / 锁 / 限流 | Redis（StackExchange.Redis） | 沿用 VVPay |
| 跨进程消息 | RabbitMQ（RabbitMQ.Client） | 沿用 VVPay 的事件总线 |
| 校验 | DataAnnotations | 后台表单；本项目没有对外 API |
| 权限 | 自建 RBAC + `IAuthorizationHandler` | 不用 ASP.NET Identity |
| 日志 | 内置 `ILogger`，JSON 格式输出 | Serilog 为可选项 |
| 测试 | xUnit | |

引入新的第三方包前，先确认许可证（PRD §13.2.4），并登记到 SBOM。

---

## 4. 启动与依赖注入

- 每个模块提供一个注册扩展方法，内聚该模块的全部注册，程序按需拼装：
  `AddSuanpanData`、`AddSuanpanServices`、`AddSuanpanRedis`、`AddSuanpanEventBus`、`AddSuanpanHangfire`、`AddSuanpanCollectors`、`AddSuanpanPortalRead`。
- **调用 `AddSuanpanServices` 的程序必须同时注册 Redis**（部分服务依赖 `IRedisCache`，漏注册会在启动时崩溃，VVPay 踩过）。

各程序的注册组合：

| 程序 | 注册内容 |
|---|---|
| Crm | MVC（全局 `OpLogActionFilter` + `AutoPermissionConvention`）、ForwardedHeaders、Data、Services、Redis、EventBus（发布端）、Hangfire（只挂面板，`runServer: false`） |
| Job | Data、Services、Redis、EventBus（发布 + 消费）、Hangfire（`runServer: true`）、任务注册表 |
| Collector | Data、Services、Redis、EventBus（发布 + 消费）、Collectors（适配器、解析器）、盯盘后台服务 |
| Portal | MVC、ForwardedHeaders、PortalRead（对外视图的只读上下文）、Redis（可选）、EventBus（只消费缓存刷新消息）、输出缓存 |

Web 程序的请求管道顺序（沿用 VVPay）：

`UseForwardedHeaders`（必须最前）→ ExceptionHandler / HSTS → HttpsRedirection → Routing → Authentication → Authorization → 静态资源 → Hangfire 面板（仅 Crm）→ Areas 路由 → 默认路由。

---

## 5. 认证与权限（Crm）

沿用 VVPay 的整套机制：

- **登录**：Cookie 认证（HttpOnly、SameSite=Lax、8 小时滑动过期）。
- **会话失效**：每个请求校验安全戳（60 秒缓存兜底），改密码、重置、停用后旧 Cookie 立即作废。
- **二次验证**：TOTP。密码通过后先签发一个 5 分钟的半登录 Cookie（不带业务权限），TOTP 通过后才签发正式票据。
- **密码哈希**：PBKDF2（10 万次迭代）。
- **权限默认拒绝**：`AutoPermissionConvention` 给每个 Area 内的 action 自动加上 `{area}:{controller}:{action}` 权限要求；Controller 标 `[PermMenu]`，action 按需标 `[PermName]`；权限树通过"从代码重建"生成，不手工建权限点。
- 一个账号可以有多个角色，权限取并集（PRD BR-139）。

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

### 6.5 EF Core 表达式的坑（沿用 VVPay 的教训）

1. `ExecuteUpdateAsync` 的 `SetProperty` 里**不要内联 `DateTime.UtcNow`**，先取到局部变量再传入。
2. 表达式树里不能用 range 运算符（如 `[..256]`），提到语句外面处理。
3. `GroupBy` 投影到带参构造的 record 无法翻译，先投影成匿名类型，落地后再转换。

### 6.6 表结构管理 **【与 VVPay 不同】**

- 表结构只通过 EF Core Migrations 变更，Migrations 放在 `Suanpan.Data`。
- 新增迁移：`dotnet ef migrations add <PascalCase 描述>`。
- 上线：先用 `dotnet ef migrations script --idempotent` 导出可重复执行的 SQL，人工审核后，用迁移专用账号执行。
- **生产环境禁止在程序启动时自动迁移。**
- 对外视图（`v_pub_*`）和数据库账号授权也写在迁移里（用 `migrationBuilder.Sql`），不手工在库里改。

### 6.7 数据库账号

| 账号 | 使用方 | 权限 |
|---|---|---|
| `suanpan_migrator` | 上线执行迁移 | DDL |
| `suanpan_app` | Collector、Job、Crm | 业务表读写；操作日志表只有 INSERT 和 SELECT（PRD BR-148） |
| `suanpan_portal` | Portal | 只能 SELECT `v_pub_*` 视图（PRD BR-121），并限制连接数（PRD NFR-014） |

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
- 配置键用 static class 集中定义常量，通过 `IConfigService.GetXxxAsync(key, 默认值)` 读取：键不存在时用代码默认值，`sys_config` 中有值时覆盖。

---

## 8. 基础设施

### 8.1 Redis

沿用 VVPay 的三个组件，均为 Singleton：

- `IRedisCache`：JSON 序列化，带实例前缀。用 `SetIfNotExistsAsync` 做幂等标记，`IncrementAsync` 做计数。
- `IDistributedLock`：`await using` 获取锁句柄；过期时间必须大于临界区耗时；拿不到锁时返回 null，调用方直接跳过本轮。
- `IRateLimiter`：秒级固定窗口；Redis 异常时放行并告警。本项目用它控制每个数据源的请求频率（PRD BR-003）。

缓存的约定：key 用常量前缀，必须设置明确的 TTL，**由写数据的一方负责让缓存失效**。

### 8.2 RabbitMQ

沿用 VVPay：

- 发布：`IEventBus.PublishAsync(message, delay?)`。延时投递用 TTL + 死信分桶实现，不依赖插件。
- 消费：继承 `IntegrationEventHandler<T>`，用 `AddIntegrationEventHandler<,>()` 注册。
- **消费端必须幂等**（重试必然导致重复投递）。失败自动延迟重试 3 次，之后进入 `.failed` 死信队列。
- 不做 Outbox，靠消费端幂等和对账兜底。

本项目新增：

- 消息契约放在 `Suanpan.Shared/Messages`，这样 Portal 不引用 Services 也能消费。
- 命名：事件用过去式并以 `Event` 结尾，指令以 `Command` 结尾。

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

- Server 只在 Job 中运行；Crm 只挂面板，路径 `/System/hangfire`，受权限码保护。
- 用依赖注入的 `IRecurringJobManager` 注册任务，所有周期任务集中在 Job 的任务注册表中。
- 任务模板：`[AutomaticRetry(Attempts = 0)]`（周期任务自带下一轮）+ 分布式锁防止多实例重入 + 业务幂等（重跑安全）。
- **cron 一律按 UTC 配置。** **【与 VVPay 不同】** VVPay 使用 `TimeZoneInfo.Local`。
- 周期任务最小粒度为 1 分钟。秒级需求（盯盘）不走 Hangfire，见 §9。

---

## 9. 采集（Collector）

- **一个数据源一个适配器**（参考 VVPay.Adapters 的模式）。接入新的数据源 = 新增一个适配器类，加一行注册，主流程不改。
- **先存快照，再解析**：每次请求的原始响应先保存为快照（PRD FR-004），再交给解析器。
- **解析器写成纯函数**：输入快照内容，输出标准化记录，不访问数据库和网络，这样可以直接用保存下来的快照写单元测试。
- 每个数据源使用独立的命名 HttpClient，各自设置超时；User-Agent 带项目名和联系邮箱（PRD BR-005）。
- 请求频率受数据源上限约束，用 `IRateLimiter` 控制（PRD BR-003）；遵守 robots.txt 和数据源条款。
- **盯盘**是 Collector 中的常驻后台服务（`BackgroundService`）：每分钟扫描未来 10 分钟内需要盯盘的事件，用 Redis 分布式锁保证全局只有一个实例在盯（PRD FR-003、DEC-017）。
- 服务器必须开启 NTP 时间同步，时钟偏差超过 1 秒要告警（PRD NFR-113）。

---

## 10. 时间与时区

- 服务器时区统一设为 UTC；数据库只存 UTC。
- 代码中一律使用 `DateTime.UtcNow`，**禁止使用 `DateTime.Now`**。
- 官方发布排期按机构当地时间解析，用 IANA 时区（如 `America/New_York`）换算成 UTC，夏令时由时区库处理（PRD BR-037）。
- 转换成用户时区只在展示层做（后台按操作者时区，前台按访客时区），见前端约定。
- 解析和格式化数字一律使用 `CultureInfo.InvariantCulture`。

---

## 11. 日志与操作日志

- **操作日志**：后台的写操作标 `[OpLog("模块", "动作")]`，由 `OpLogActionFilter` 统一写入 `sys_op_log`，权限码自动采集（沿用 VVPay）。
  - 只记录请求参数；密码、密钥、API Key 等字段必须脱敏（PRD BR-147）。
  - 不记录字段修改前后的值；业务上需要的变更历史（改期、利率、模板版本）由各功能自己记录（PRD DEC-018）。
  - 操作日志写入失败时，不回滚业务操作，但要写错误日志并告警（PRD BR-151）。
- **应用日志**：JSON 格式，带 traceId，贯穿采集 → 入库 → 快讯 → 前台缓存刷新（PRD NFR-050），保留 ≥ 180 天。
- **日志级别**：需要人处理的问题打 Error（如解析失败、缓存刷新连续失败）；依赖抖动、限流等打 Warning。
- **脱敏**：外部响应内容不整段打进日志；凭据在任何日志中都不出现明文。

---

## 12. 配置与密钥

- 基础设施连接串（PostgreSQL、Redis、RabbitMQ）和加密密钥：`appsettings.json` 只放占位值，实际值通过环境变量注入，格式为 `SUANPAN__节__键`；本地开发用 user-secrets。
- **仓库里不得出现任何真实密码、连接串和密钥**；CI 做密钥扫描（PRD NFR-033、NFR-134）。**【与 VVPay 不同】** VVPay 的 appsettings 中有明文密码。
- 初始管理员密码在首次部署时通过环境变量设置，不硬编码在种子数据里。
- 业务动态开关放 `sys_config` 表（60 秒缓存，变更后立即失效；键不存在时用代码默认值）。基础设施配置不放 `sys_config`，因为它自身依赖数据库。

---

## 13. 测试与持续集成

- `Suanpan.Core.Tests`：利多利空判定的全部分支、夏令时切换周的时区换算、数据质量校验规则（PRD NFR-133）。
- `Suanpan.Collector.Tests`：每个解析器至少有一组基于真实快照的测试样例；官方页面改版后，把新快照补进样例。
- 测试数据要带独立标记，测试结束后清理恢复原状（沿用 VVPay 的做法）。
- GitHub Actions：构建、运行测试、依赖漏洞扫描、密钥扫描，任一项不通过都不能合并（PRD NFR-134）。

---

## 14. 部署与开发环境

- 每个程序一个 Dockerfile；`deploy/docker-compose.yml` 包含 PostgreSQL、Redis、RabbitMQ 和四个程序，用于本地联调和第一阶段部署。
- 不依赖只有境外才有的云服务，方便迁移（PRD NFR-120）。
- Windows 本地开发：编译前先停掉正在运行的 Suanpan.* 进程，否则 bin 目录下的 DLL 会被锁住（VVPay 踩过）。
- 本地连接串用 `127.0.0.1`，不用 `localhost`（VVPay 的经验）。

---

## 变更记录

| 版本 | 日期 | 修改人 | 内容 |
|---|---|---|---|
| v1.0 | 2026-10-17 | vivo.li / Claude | 初版：基于 VVPay 后端框架和 PRD v1.0.0.2 的技术决策整理 |
