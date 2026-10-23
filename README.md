# suanpan-forex

外汇财经工具与资讯网站。第一阶段：从各国官方机构采集经济数据，形成财经日历、自动中英文快讯、央行利率表和交易计算器；后台供运营人员管理，前台面向中文和英文用户。

单体解决方案，包含四个可分开部署的程序：

| 程序 | 职责 |
|---|---|
| Suanpan.Crm | 运营后台 |
| Suanpan.Portal | 前台站点（只读数据库账号） |
| Suanpan.Job | 定时任务、快讯生成、告警 |
| Suanpan.Collector | 盯盘、数据采集 |

> 当前处于项目骨架阶段：四个程序都能启动，但还没有页面和功能。

## 环境要求

- .NET SDK 10.0.101 或更高的 10.0 特性版本（见 `global.json`）
- Docker Desktop（含 Docker Compose v2）
- Git

## 本地启动

### 1. 启动依赖服务

复制环境变量模板，然后在 `deploy/.env` 中给每个 `PASSWORD` 项填一个随机字符串（只用于本机，`.env` 不会被提交）：

```bash
cp deploy/.env.example deploy/.env
```

启动 PostgreSQL、Redis（Valkey）、RabbitMQ：

```bash
docker compose -f deploy/docker-compose.yml up -d
```

确认三项服务都是 `healthy`：

```bash
docker compose -f deploy/docker-compose.yml ps
```

PostgreSQL 首次启动时会自动创建业务库 `suanpan` 和三个账号：`suanpan_migrator`（执行迁移）、`suanpan_app`（Crm、Job、Collector）、`suanpan_portal`（Portal）。

#### 本机已有 Redis、RabbitMQ 时

只启动 PostgreSQL：

```bash
docker compose -f deploy/docker-compose.yml up -d postgres
```

在已有的 RabbitMQ 中为本项目建独立的虚拟主机和账号（把 `<容器名>`、`<密码>` 换成实际值）：

```bash
docker exec <容器名> rabbitmqctl add_vhost suanpan
docker exec <容器名> rabbitmqctl add_user suanpan <密码>
docker exec <容器名> rabbitmqctl set_permissions -p suanpan suanpan ".*" ".*" ".*"
```

已有的 Redis 不需要额外设置，本项目的键都带 `suanpan:` 前缀。

### 2. 配置本地密钥

四个程序共用一份 user-secrets，在任意一个程序上设置即可，下面都用 Crm。`ConnectionStrings:Migrator` 只供 `dotnet ef` 执行迁移，程序不读取。尖括号中的密码取自 `deploy/.env`；复用本机已有的服务时，填那个服务的密码。

```bash
dotnet user-secrets set "ConnectionStrings:Migrator" "Host=127.0.0.1;Port=5432;Database=suanpan;Username=suanpan_migrator;Password=<SUANPAN_MIGRATOR_PASSWORD>" --project src/Suanpan.Crm
dotnet user-secrets set "ConnectionStrings:Main" "Host=127.0.0.1;Port=5432;Database=suanpan;Username=suanpan_app;Password=<SUANPAN_APP_PASSWORD>" --project src/Suanpan.Crm
dotnet user-secrets set "ConnectionStrings:Portal" "Host=127.0.0.1;Port=5432;Database=suanpan;Username=suanpan_portal;Password=<SUANPAN_PORTAL_PASSWORD>" --project src/Suanpan.Crm
dotnet user-secrets set "Redis:Configuration" "127.0.0.1:6379,password=<REDIS_PASSWORD>" --project src/Suanpan.Crm
dotnet user-secrets set "RabbitMq:Uri" "amqp://suanpan:<RABBITMQ_PASSWORD>@127.0.0.1:5672/suanpan" --project src/Suanpan.Crm
```

查看已设置了哪些键：

```bash
dotnet user-secrets list --project src/Suanpan.Crm
```

运行 Crm 还需要下面两项（完整的配置键见[后端约定 §12](docs/conventions/backend.md)）：

| 键 | 用途 | 何时需要 |
|---|---|---|
| `Security:EncryptionKey` | 加密 TOTP 密钥和数据源凭据；32 字节随机数的 Base64，用 `openssl rand -base64 32` 生成 | 必填，Crm 启动和登录时用到 |
| `Seed:AdminUsername`、`Seed:AdminPassword` | 初始管理员的登录名和初始密码：库里还没有任何账号时，Crm 启动时用它创建（待激活），首次登录须改密码并绑定二次验证 | 首次启动 Crm 之前 |

其余可选配置：

| 键 | 用途 | 何时需要 |
|---|---|---|
| `Security:BootstrapAllowedIps` | 后台 IP 白名单的应急放行 IP，逗号分隔 | 可选 |
| `ForwardedHeaders:TrustedProxies` | 可信的反向代理，逗号分隔的 IP 或 CIDR；本地直接访问时不用设置 | 部署到反向代理后面时 |
| `Auth:SkipTotpInDevelopment` | 设为 `true` 后，已绑定二次验证的账号登录时不用输入验证码；只在开发环境生效，其他环境忽略 | 可选，本地开发 |

### 3. 编译和运行

```bash
dotnet build
```

每个程序在单独的终端中运行，例如：

```bash
dotnet run --project src/Suanpan.Crm
```

| 程序 | 本地地址 |
|---|---|
| Crm | http://127.0.0.1:5100 |
| Portal | http://127.0.0.1:5200 |
| Job | http://127.0.0.1:5300 |
| Collector | http://127.0.0.1:5400 |

Crm 在开发环境下可以打开 http://127.0.0.1:5100/Dev/Components 查看后台的布局和公共部件（不需要登录，其他环境不可用）。

首次登录后台：用 `Seed:AdminUsername`、`Seed:AdminPassword` 登录，按提示设置新密码，再用手机上的验证器 App（如 Microsoft Authenticator）扫码绑定二次验证。之后每次登录都要输入验证码；本地开发嫌麻烦时，可以设置 `Auth:SkipTotpInDevelopment`：

```bash
dotnet user-secrets set "Auth:SkipTotpInDevelopment" "true" --project src/Suanpan.Crm
```

设置后已绑定的账号输完密码直接进入后台，登录日志记为"跳过 TOTP"；首次登录的改密码、绑定二次验证照常进行。登录时的验证码防重放依赖 Redis，Redis 连不上时无法登录。

运行全部测试（集成测试会用 Docker 启动临时的 PostgreSQL、Valkey、RabbitMQ，需要 Docker 在运行）：

```bash
dotnet test
```

只跑单元测试（不需要 Docker）：

```bash
dotnet test --filter-not-trait "Category=Integration"
```

### 4. 数据库迁移

表结构由 EF Core Migrations 管理。首次使用先安装仓库固定版本的 `dotnet-ef`：

```bash
dotnet tool restore
```

用迁移账号（上面设置的 `ConnectionStrings:Migrator`）把本地库更新到最新：

```bash
dotnet ef database update --context SuanpanDbContext --project src/Suanpan.Data --startup-project src/Suanpan.Data
```

完成后库里有 9 张 `sys_` 表和 4 个预置角色。编写迁移的规则见[数据库变更约定](docs/conventions/database.md)。

## 配置说明

- 配置的优先级从低到高：`appsettings.json` → `appsettings.{环境}.json` → user-secrets（只在 Development 环境）→ 环境变量 → 命令行参数 → `SUANPAN__` 开头的环境变量。
- `appsettings.json` 中的密码、连接串、密钥都留空。预发布和生产环境通过环境变量 `SUANPAN__节__键` 注入，如 `SUANPAN__ConnectionStrings__Main`。
- 仓库中不能出现任何真实密码、连接串和密钥。

## 常见问题

- **Windows 下编译报 DLL 被占用**：先停掉正在运行的 Suanpan.* 进程。
- **连接串的主机名**：用 `127.0.0.1`，不用 `localhost`。
- **改了 `deploy/.env` 中的数据库密码不生效**：初始化脚本只在首次启动时执行。删除数据卷后重新启动即可，但会清空本地数据库：`docker compose -f deploy/docker-compose.yml down -v`。

## 文档

| 文档 | 内容 |
|---|---|
| [data-hub PRD](docs/product/data-hub/prd.md) | 采集与后台的需求 |
| [web-portal PRD](docs/product/web-portal/prd.md) | 前台的需求 |
| [后端技术约定](docs/conventions/backend.md) | 解决方案结构、依赖方向、配置与密钥 |
| [前端技术约定](docs/conventions/frontend.md) | 后台和前台的页面写法 |
| [数据库变更约定](docs/conventions/database.md) | 迁移的编写、校验和上线 |
| [CLAUDE.md](CLAUDE.md) | AI 辅助开发的入口和硬性规则 |
