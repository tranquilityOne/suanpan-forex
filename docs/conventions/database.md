# suanpan-forex 数据库变更约定

| 项目 | 内容 |
|---|---|
| 版本 | v1.1 |
| 日期 | 2026-10-17 |
| 适用范围 | PostgreSQL 中表结构、视图、索引、授权、预置数据的编写、校验与上线 |
| 依据 | data-hub PRD：DEC-009（PostgreSQL）、DEC-015（EF Core Migrations）、NFR-132、NFR-090、BR-119、BR-121、BR-148 |
| 相关约定 | [后端技术约定](backend.md) §6 数据访问 |

> 本文是变更数据库时必须遵守的约定。
> 约定与实际做法不一致时，以本文为准；确需调整的，先改本文（走评审），再执行。
> **本文没有覆盖到的情况，不要参考 VVPay 或其他项目自行决定**，先提出问题，确认后补充进本文再执行。

---

## 1. 基本原则

1. **表结构只通过 EF Core Migrations 变更。** 不在任何环境中手工改表；视图、授权、特殊索引也写在迁移里。
2. **生产和预发布环境禁止程序启动时自动迁移，也禁止直接执行 `dotnet ef database update`。** 一律导出 SQL 脚本，经人工审核后执行。
3. **已在生产环境执行过的迁移，不能修改或删除**；要改就新增一个迁移。
4. **每次数据库变更都必须兼容上一版程序**（§5），因为 4 个程序分开部署，发布期间新旧版本会同时运行。
5. **执行前必须备份，先在预发布环境执行同一份脚本。**

---

## 2. 环境与账号

### 2.1 各环境的执行方式

| 环境 | 执行方式 | 执行账号 |
|---|---|---|
| 本地开发 | 允许 `dotnet ef database update` | 本地账号 |
| CI | 临时数据库：从空库执行全部迁移，再执行两遍导出的幂等脚本（§6） | CI 临时账号 |
| 预发布 | 导出的 SQL 脚本，用 psql 执行 | `suanpan_migrator` |
| 生产 | 与预发布**同一份**脚本，用 psql 执行 | `suanpan_migrator` |

### 2.2 数据库账号

| 账号 | 用途 | 权限 |
|---|---|---|
| `suanpan_migrator` | 只用于执行迁移 | 业务表、视图、序列的所有者 |
| `suanpan_app` | Collector、Job、Crm | 业务表读写；`sys_op_log`、`sys_login_log` 只有 INSERT 和 SELECT（BR-148）；`hangfire` schema 内可建表 |
| `suanpan_portal` | Portal | 只能 SELECT `v_pub_*` 视图（BR-121） |

- **账号本身（包括密码）由运维在建库时创建，不写进迁移，也不进代码仓库。** 迁移只负责授权。
- 本地开发环境的业务库和三个账号由 `deploy/postgres/init/` 中的脚本在 PostgreSQL 首次启动时创建，密码来自 `deploy/.env`（后端约定 §14）。本地业务库的所有者是 `suanpan_migrator`，只有这三个账号能连接业务库。
- 业务表放在 `public` schema；Hangfire 的表放在独立的 `hangfire` schema。

### 2.3 默认授权

第一个迁移中设置默认授权，之后迁移账号新建的表和序列会自动授予应用账号，不需要每个迁移重复写：

```sql
ALTER DEFAULT PRIVILEGES FOR ROLE suanpan_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO suanpan_app;
ALTER DEFAULT PRIVILEGES FOR ROLE suanpan_migrator IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO suanpan_app;
```

以下特殊授权，必须在创建该对象的同一个迁移里写明：

| 对象 | 授权 |
|---|---|
| `sys_op_log`、`sys_login_log` | `REVOKE UPDATE, DELETE ... FROM suanpan_app` |
| `v_pub_*` 视图 | `GRANT SELECT ... TO suanpan_portal`（前台账号不使用默认授权） |

### 2.4 Hangfire 的表

- `hangfire` schema 由迁移创建（空 schema），并授予 `suanpan_app` 在该 schema 内的 `CREATE`、`USAGE` 权限。
- Hangfire 启动时在自己的 schema 内建表和升级，不需要业务 schema 的建表权限。
- Hangfire 的表不纳入 EF Core Migrations 管理。

---

## 3. 编写迁移

### 3.1 新增迁移

```bash
dotnet ef migrations add <名称> --project src/Suanpan.Data --startup-project src/Suanpan.Crm
```

- **名称**用 PascalCase，"动词 + 对象"，说清改了什么：`AddCalendarEvent`、`AddIndicatorFlatTolerance`、`RenameFlashTitle`、`CreatePublicCalendarView`、`SeedPresetRoles`。
- **一个功能一个迁移**；不要把不相关的变更塞进同一个迁移。
- 迁移文件和模型快照（`SuanpanDbContextModelSnapshot.cs`）**一起提交**，PR 中必须审核迁移代码，不能只看实体类的改动。
- 迁移文件放在 `Suanpan.Data/Migrations/`，由工具生成，不手工分目录。

### 3.2 修改迁移

- **还没在生产执行过**的迁移：可以删除后重新生成，或合并成一个。
- **已在生产执行过**的迁移：不能修改、删除、改名；要修正，新增一个迁移。
- 判断是否已上线：看生产库 `__EFMigrationsHistory` 的记录，或 `deploy/sql/` 中已发布的脚本。

### 3.3 手写 SQL

以下情况在迁移中用 `migrationBuilder.Sql(...)` 手写 SQL：

| 场景 | 要求 |
|---|---|
| 对外视图 `v_pub_*` | 用 `CREATE OR REPLACE VIEW`；`Down` 中 `DROP VIEW`；同一迁移中授权给 `suanpan_portal` |
| 授权 | 见 §2.3 |
| 大表建索引 | 用 `CREATE INDEX CONCURRENTLY`，必须加 `suppressTransaction: true`（这种语句不能在事务中执行） |
| 少量数据修正、回填 | 写成可重复执行的 SQL（带条件判断）；数据量大时不放在迁移里，见 §4 |

每个迁移都要写 `Down`。确实无法回退的（如删除数据），在 `Down` 中抛出异常并注释原因。

### 3.4 预置数据

| 类型 | 方式 | 示例 |
|---|---|---|
| 固定不变、与环境无关的数据 | 实体配置中的 `HasData`，随迁移写入 | 4 个预置角色 |
| 依赖环境变量的数据 | 程序启动时的种子服务，只在表为空时执行 | 初始管理员 |
| 运营维护的数据 | 后台录入或 CSV 导入，不进迁移 | 指标字典、数据源 |

---

## 4. PostgreSQL 变更规则

审核迁移时逐条对照：

| 变更 | 风险 | 正确做法 |
|---|---|---|
| 改列名、改表名 | EF 有时识别不出改名，会生成"删除 + 新增"，数据丢失 | 在迁移中显式写 `RenameColumn` / `RenameTable`，审核时确认没有生成 Drop |
| 给已有数据的表加非空列 | 没有默认值时，已有数据导致语句失败 | 先加可空列 → 回填数据 → 下一个迁移再改为非空；或者直接给常量默认值 |
| 改列类型 | 需要重写整张表，期间锁表 | 大表不直接改类型：新增列 → 回填 → 切换代码 → 删除旧列（按 §5 分两次发布） |
| 大表建索引 | 普通建索引会阻塞写入 | `CREATE INDEX CONCURRENTLY`，见 §3.3 |
| 大批量回填数据 | 长事务锁表，迁移执行时间不可控 | 不放在迁移里，改为一次性后台任务分批执行，发布说明中写明 |
| 删列、删表 | 不可恢复 | 只能在"收缩"阶段做（§5）；PR 必须注明理由，并由第二人审核 |
| 枚举（smallint）取值 | 已有取值被改含义会导致历史数据错乱 | 只能新增取值，不能修改或复用已有取值的含义 |
| 外键 | 违反约定 | 不建物理外键（后端约定 §6.1），只建普通索引 |

---

## 5. 向后兼容：分两次发布

发布期间，新旧两个版本的程序会同时访问数据库。所以**一次发布中的数据库变更，必须让上一版程序也能正常运行**。破坏性的修改按"扩展 → 收缩"分两次发布：

| 阶段 | 数据库变更 | 程序 |
|---|---|---|
| 第一次发布（扩展） | 只做新增：新表、新列（可空或带默认值）、新索引、新视图 | 新代码同时兼容新旧结构；必要时回填数据 |
| 第二次发布（收缩） | 删除旧列、旧表，或把新列改为非空 | 确认已没有任何程序使用旧结构 |

示例：把 `flash_news.title` 改名为 `title_zh`

1. **第一次发布：** 新增 `title_zh` 列并回填；代码改为读写 `title_zh`，同时继续写 `title`。
2. **第二次发布：** 所有程序都已升级后，删除 `title` 列。

只新增、不影响旧代码的变更（如新增一张表），一次发布即可。

---

## 6. CI 校验

每个 PR 自动执行，任一项不通过不能合并：

1. `dotnet ef migrations has-pending-model-changes`：确认没有"改了实体却忘了生成迁移"。
2. 在临时 PostgreSQL 中从空库执行全部迁移，确认成功。
3. 导出全量幂等脚本（`--idempotent`），在同一个库上再执行两遍，确认重复执行不报错。
4. 检查本次新增的迁移：出现 `DropTable`、`DropColumn` 时，要求 PR 描述中注明理由（第二人审核）。

---

## 7. 发布流程

### 7.1 步骤

| # | 步骤 | 说明 |
|---|---|---|
| 1 | 确定起点 | 查生产库 `__EFMigrationsHistory` 的最后一条，作为"上次上线的迁移" |
| 2 | 导出脚本 | 导出本次脚本和回滚脚本，放进 `deploy/sql/`，随发布提交 |
| 3 | 审核脚本 | 按 §4 逐条检查；确认符合 §5 的兼容要求；确认授权语句齐全 |
| 4 | 备份 | 执行前做数据库快照或 `pg_dump` |
| 5 | 预发布执行 | 用同一份脚本执行，部署程序，冒烟测试通过 |
| 6 | 生产执行 | 用 `suanpan_migrator` 执行，遇错立即停止 |
| 7 | 核对 | `__EFMigrationsHistory` 的最后一条应为本次最新的迁移 |
| 8 | 部署程序 | 因为满足向后兼容，Collector、Job、Crm、Portal 的部署顺序不限 |
| 9 | 验证与记录 | 冒烟测试；在发布记录中写明执行的脚本、执行人、时间 |

### 7.2 命令

```bash
# 导出本次要执行的脚本（从上次上线的迁移到最新），幂等
dotnet ef migrations script <上次上线的迁移> --idempotent -o deploy/sql/v0.2.0.sql --project src/Suanpan.Data --startup-project src/Suanpan.Crm

# 导出回滚脚本（从本次最新的迁移回到上次上线的迁移）
dotnet ef migrations script <本次最新的迁移> <上次上线的迁移> -o deploy/sql/v0.2.0.rollback.sql --project src/Suanpan.Data --startup-project src/Suanpan.Crm

# 执行（连接串中的密码通过环境变量 PGPASSWORD 或 .pgpass 提供，不写在命令里）
psql "host=<主机> dbname=<库名> user=suanpan_migrator" -v ON_ERROR_STOP=1 -f deploy/sql/v0.2.0.sql
```

- 导出的脚本按迁移分段提交事务。脚本中含有 `CONCURRENTLY` 语句时，不要用 psql 的 `--single-transaction` 参数。
- 首次上线时没有"上次上线的迁移"，省略起点参数，导出全部迁移。

### 7.3 脚本存放

```
deploy/sql/
├─ v0.1.0.sql
├─ v0.1.0.rollback.sql
├─ v0.2.0.sql
└─ v0.2.0.rollback.sql
```

文件名与产品版本号一致（data-hub PRD §10.1），已发布的脚本不再修改。

---

## 8. 回滚与故障处理

- **优先往前修**：执行失败或上线后发现问题，优先新增一个修复迁移，重新走发布流程。
- **回滚脚本**：只在确认不会丢失数据时使用（例如本次只新增了表或列，还没有写入重要数据）。
- **从备份恢复**：最后手段，会丢失备份之后的数据，需要负责人批准。
- 任何回滚或恢复，都要在发布记录中写明原因和过程。

---

## 9. 只读副本（中期）

前台迁回大陆后，会在大陆部署 PostgreSQL 只读副本（BR-119）。选择复制方式时要注意：

| 复制方式 | 表结构变更 | 要求 |
|---|---|---|
| 物理流复制 | 自动同步到副本 | 无额外步骤 |
| 逻辑复制 | **不会**自动同步 | 每次发布时，在副本上也执行同一份脚本中与视图、表结构相关的部分；`v_pub_*` 视图和 `suanpan_portal` 的授权在副本上必须存在 |

具体方案在迁回大陆前确定，确定后补充本节。

---

## 10. 后续自动化

流程稳定后，可以改用 EF Core 的迁移包（`dotnet ef migrations bundle`）：在 CI/CD 中打包成可执行文件，加人工审批步骤后执行。第一阶段仍使用"导出 SQL + 人工执行"，改用迁移包前先修订本文。

---

## 变更记录

| 版本 | 日期 | 修改人 | 内容 |
|---|---|---|---|
| v1.0 | 2026-10-17 | vivo.li / Claude | 初版 |
| v1.1 | 2026-10-17 | vivo.li / Claude | §2.2 补充本地开发环境的建库和建账号方式 |
