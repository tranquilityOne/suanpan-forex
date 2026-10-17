# suanpan-forex

外汇财经工具与资讯网站。第一阶段内容：从各国官方机构采集经济数据，形成财经日历、自动中英文快讯、央行利率表和交易计算器；后台供运营人员管理，前台面向中文（以大陆为主）和英文用户，不做会员。

**当前阶段**：需求与约定已定稿到初稿，代码尚未创建。项目骨架由工程任务 ENG-001 开始搭建（见 PRD §10.5）。

## 文档索引

| 要了解 | 看哪里 |
|---|---|
| 要做什么（需求、业务规则、验收标准） | `docs/product/data-hub/prd.md`（采集与后台）、`docs/product/web-portal/prd.md`（前台） |
| 全局怎么写（后端） | `docs/conventions/backend.md` |
| 全局怎么写（前端，含后台和前台） | `docs/conventions/frontend.md` |
| 改数据库、上线 SQL | `docs/conventions/database.md` |
| 某个模块怎么实现 | `docs/design/<模块>.md`（按里程碑陆续补充） |
| 为什么这样定 | data-hub PRD §10.6 决策日志 |
| 工程任务与里程碑 | data-hub PRD §10.5 |
| 技术基础、从 VVPay 迁移哪些模块（仅用于 ENG-002） | data-hub PRD §10.10 |

PRD 很长（data-hub 约 2900 行），**不要整份读**，按编号检索：如 `#### FR-014`、`| BR-074 `、`| DEC-021 `。

编号含义：FR 功能需求、BR 业务规则、NFR 非功能需求、ENG 工程任务、DEC 决策、ASS 假设、RISK 风险、Q 待澄清问题、EV 系统事件、ERR 错误码。

## 解决方案结构

单体解决方案，四个可分开部署的程序共享类库（详见 `docs/conventions/backend.md` §1–2）：

```
src/Suanpan.Core            领域规则，零依赖
src/Suanpan.Shared          鉴权基础组件、操作日志特性、消息契约
src/Suanpan.Data            DbContext、Migrations、对外视图
src/Suanpan.Infrastructure  Redis、RabbitMQ、Hangfire
src/Suanpan.Services        业务逻辑
src/Suanpan.Collector       程序：盯盘、采集、重新解析
src/Suanpan.Job             程序：Hangfire 定时任务、快讯生成、告警
src/Suanpan.Crm             程序：后台
src/Suanpan.Portal          程序：前台（只读数据库）
tests/                      Suanpan.Core.Tests、Suanpan.Collector.Tests
deploy/                     Dockerfile、docker-compose
```

## 常用命令

> 以下命令在 ENG-001（项目骨架）和 ENG-006（本地开发环境）完成后生效；与实际不符时以项目为准，并更新本节。

```bash
docker compose -f deploy/docker-compose.yml up -d     # 启动 PostgreSQL、Redis、RabbitMQ
dotnet build                                          # 编译整个解决方案
dotnet test                                           # 运行全部测试
dotnet run --project src/Suanpan.Crm                  # 运行后台（其他程序同理）
dotnet ef migrations add <PascalCase描述> --project src/Suanpan.Data --startup-project src/Suanpan.Crm
dotnet ef migrations has-pending-model-changes --project src/Suanpan.Data --startup-project src/Suanpan.Crm
dotnet ef migrations script <上次上线的迁移> --idempotent -o deploy/sql/<版本>.sql --project src/Suanpan.Data --startup-project src/Suanpan.Crm
node --test src/Suanpan.Portal/wwwroot/js/calc        # 前台计算器公式测试
```

## 硬性规则

违反以下任一条，代码评审不通过。完整说明见两份技术约定。

**架构与目录**
1. 依赖只能单向向下：程序 → Services → Data / Infrastructure → Shared → Core。**Portal 不能引用 Services**，只能通过只读上下文查询 `v_pub_*` 视图。
2. 程序项目不直接引用第三方 NuGet 包；不用 `IRepository<T>` 包装 EF Core，不用 MediatR。
3. **每个项目内部都按模块、功能或同类分文件夹，不平铺。** 模块名全项目统一：`Sys`、`Reference`、`Collect`、`Calendar`、`Flash`、`Market`、`News`、`Publish`、`Ops`，目录名、命名空间、后台 Area 名都用同一个名字（系统模块叫 `Sys` 而不是 `System`，避免与 .NET 的 `System` 命名空间冲突；后台显示名仍为"系统管理"）；命名空间与目录一致；不建 Utils、Helpers 这类杂项目录；一个文件夹超过约 15 个文件时继续拆分。各项目的目录模板见后端约定 §1.1–1.2、前端约定 §3.3 和 §9.2。

**数据**
4. **时间只存 UTC**：一律用 `DateTime.UtcNow`，**禁止 `DateTime.Now`**；`ExecuteUpdateAsync` 的 `SetProperty` 里不内联时间，先取局部变量。
5. 表结构只通过 EF Core Migrations 变更；**预发布和生产环境禁止启动时自动迁移**，上线用导出的幂等 SQL；**已上线的迁移不能修改或删除**；每次变更必须兼容上一版程序，改名、删列这类破坏性修改分两次发布。完整流程见 `docs/conventions/database.md`。
6. 实体不写导航属性，数据库不建物理外键；列名 snake_case；表名按模块加前缀（`sys_`、`ref_`、`col_`、`cal_`、`flash_`、`news_`、`cb_`、`fx_`、`ops_`，对外视图 `v_pub_`）。
7. 指标数值用 `numeric(18,6)` / `decimal`，不用 float、double。

**后台**
8. 写操作：`[HttpPost]` + `[ValidateAntiForgeryToken]` + `[OpLog("模块","动作")]`。操作日志只记请求参数，凭据必须脱敏，不记录字段修改前后的值。
9. 权限默认拒绝，由代码自动生成，不手工建权限点；界面隐藏按钮只是体验优化，权限以后端校验为准。
10. 外部内容（快照、新闻）一律转义输出，**禁止 `Html.Raw`**。

**基础设施**
11. RabbitMQ 消费端必须幂等；消息契约放 `Suanpan.Shared/Messages/{模块}`；事件以 `Event` 结尾（过去式），指令以 `Command` 结尾。
12. Hangfire 只在 Job 中运行，cron 按 UTC 配置；秒级任务（盯盘）不用 Hangfire。
13. **仓库中不得出现任何真实密码、连接串或密钥**；真实值通过环境变量 `SUANPAN__节__键` 或 user-secrets 注入。

**采集**
14. 先存快照，再解析；解析器写成纯函数（输入快照内容，输出记录），并配基于真实快照的测试。
15. 只采集官方机构和许可数据源；请求频率不超过数据源上限，遵守 robots.txt；不采集 Forex Factory、Investing 等商业网站。

**前端**
16. 不使用 React、Vue 和前端构建工具；静态资源全部自托管，不用公共 CDN 和 Google 的服务。
17. HTMX 只用于前端约定 §4.1、§10.1 登记过的页面，新增前先登记；后台不用 Alpine.js。

## 遇到不确定时

- **需求不清**：先查 PRD 中对应的 FR / BR。PRD 没有写的，**问人，不要自行发明业务规则**。
- **约定没覆盖**：**不要参考 VVPay 或其他项目自行决定**，先提出问题，确认后补充进 `docs/conventions/`，再实现。
- **发现需求本身有问题**：先改 PRD（按 skr-prd 的修订规则），再改代码，不在代码里私自改需求。

## 完成标准

- 编译通过，全部测试通过；新增的业务逻辑有测试，新增的解析器有快照测试。
- 满足工单的验收标准；提交说明和 PR 描述引用对应的 FR / ENG 编号。
- 有表结构变更时，迁移文件和模型快照一起提交，CI 的迁移校验通过。
- 实现偏离模块设计时，在同一个 PR 里更新 `docs/design/` 下的文档；改动了全局写法时，更新 `docs/conventions/`。
- 新增第三方依赖前确认许可证，并登记到 data-hub PRD §13.2.4。

## 协作与工具

- 工单在 Linear：工作区 janehui，项目 suanpan-forex，编号前缀 JAN。父任务与 PRD 的 FR / ENG 编号一一对应，对照表见 data-hub PRD §10.5。本项目**不使用 Seeker / skr**。
- 子任务在对应模块的设计文档评审通过后再拆，一个子任务对应一个 PR；提交说明和 PR 标题带上 JAN 编号。
- 修改 PRD 时遵循 skr-prd 的规则：文档版本 `X.Y.Z.R`，初稿阶段只动第 4 位；每次修改在 §2 修订记录和 §11 变更影响分析中各记一条。
- 提交说明格式：`类型: 描述`，类型如 `docs`、`feat`、`fix`、`refactor`、`test`、`chore`。分支策略待定。

## 本地开发注意

- Windows 下编译前先停掉正在运行的 Suanpan.* 进程，否则 bin 目录的 DLL 会被锁住。
- 本地连接串用 `127.0.0.1`，不用 `localhost`。
