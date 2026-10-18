# suanpan-forex 前端技术约定

| 项目 | 内容 |
|---|---|
| 版本 | v1.5 |
| 日期 | 2026-10-17 |
| 适用范围 | 后台 Suanpan.Crm 和前台 Suanpan.Portal 的页面、视图、静态资源 |
| 依据 | data-hub PRD v1.0.0.2（DEC-014、NFR-131、§10.10）；web-portal PRD v1.0.0.2（DEC-005 ~ DEC-007、NFR-120 ~ NFR-124）；VVPay `doc/frontend.md` |
| 相关约定 | [后端技术约定](backend.md) |

> 本文是写页面时必须遵守的约定。标有 **【与 VVPay 不同】** 的条目，是本项目有意偏离 VVPay 做法的地方。
> 约定与代码不一致时，以本文为准修正代码；确需调整约定的，先改本文（走评审），再改代码。
> **本文没有覆盖到的情况，不要参考 VVPay 或其他项目自行决定**，先提出问题，确认后补充进本文再实现。

---

## 1. 总体原则

| | 后台（Crm） | 前台（Portal） |
|---|---|---|
| 使用者 | 内部运营人员 | 公众访客、搜索引擎 |
| 渲染方式 | Razor 服务端渲染，传统表单提交 + 整页刷新为主 | Razor 服务端渲染 + 输出缓存 + CDN 缓存 |
| UI 基础 | Tabler 1.4 + Tabler Icons | 单独设计，不用 Tabler（见 §8.1） |
| 局部交互 | HTMX，只用于少数页面（§4） | HTMX 做局部刷新，Alpine.js 做计算器（§10） |
| 表单校验 | jQuery + jquery-validation-unobtrusive | 计算器在浏览器本地校验 |
| 语言 | 只有中文 | 中文、英文 |
| 时区 | 操作者时区（默认 Asia/Shanghai） | 访客时区（可切换） |

两边共同遵守：

- **不使用 React、Vue，也不使用任何前端构建工具**（不需要 npm 构建、打包）。
- **全部静态资源自托管**，放在各程序的 `wwwroot/lib/` 下，禁止引用公共 CDN（部署环境可能无外网，大陆访问也不稳定）。
- 不使用 Google Fonts、Google Analytics、reCAPTCHA 等在大陆不可用的服务（web-portal NFR-120）。

---

# 第一部分：后台（Crm）

## 2. 技术栈

| 组件 | 位置 | 用途 |
|---|---|---|
| Tabler 1.4.0（含 Bootstrap 5 体系） | `wwwroot/lib/tabler/` | 卡片、表格、徽章、表单、弹窗、下拉菜单；脚本的全局对象为 `tabler`（如 `tabler.Modal`） |
| Tabler Icons 3.46.0 | `wwwroot/lib/tabler-icons/` | 图标字体，写法为 `<i class="ti ti-{name}">`；只放 woff2 字体 |
| jQuery 3.7.1 + jquery-validation 1.21.0 + jquery-validation-unobtrusive 4.0.0 | `wwwroot/lib/jquery/`、`jquery-validation/`、`jquery-validation-unobtrusive/` | 表单客户端校验，自动沿用模型上的 DataAnnotations |
| HTMX 2.0.11 | `wwwroot/lib/htmx/` | 局部刷新，只用于 §4 列出的页面 |
| Chart.js | `wwwroot/lib/chartjs/` | 运营看板图表（只在看板页面加载） |
| site.js / site.css | `wwwroot/js/`、`wwwroot/css/` | 公共脚本和少量覆写样式 |

- 后台**不用 Alpine.js**：弹窗、下拉菜单用 Tabler 自带的脚本就够了。
- `wwwroot/lib/` 下每个库只放用到的 min 文件和 LICENSE。升级时整体替换，同时更新本表和 data-hub PRD §13.2.4 的许可证登记。
- **【与 VVPay 不同】** VVPay 把 HTMX 和 Alpine.js 列为"预留"，实际没有引入；本项目引入 HTMX，但只用在 §4 的页面。

## 3. 布局与视图组织

### 3.1 布局（_Layout.cshtml）

沿用 VVPay：Tabler 垂直导航模板，左侧深色侧边栏 + 顶栏（用户菜单）+ 内容区 + 页脚。

- **菜单由权限数据驱动**：布局注入 `IMenuService`，按当前用户的权限渲染"目录 → 菜单"两级树。新页面不需要改布局：Controller 标 `[PermMenu]`，从代码重建权限树后，菜单自动出现。
- **消息统一在布局展示**：页面只写 `TempData["Message"]`（成功提示）或 `TempData["Error"]`（错误提示），不负责展示。
- **顶栏用户菜单**：修改密码 / 二步验证 / 退出。退出用 POST 表单，防止 CSRF。
- **布局统一加载**：`tabler.min.js`、`jquery.min.js`、`htmx.min.js`、`site.js`；页面级脚本写在 `@section Scripts` 中。
- **布局还包括**：HTMX 出错时的错误提示区（site.js 填写）、`<confirm-form>` 共用的确认弹窗；页脚注明页面时间按哪个时区显示。
- **账号页面用 `_LayoutAccount`**：登录、登录过程中的改密码和绑定 TOTP、错误页。没有侧边栏和菜单，不读任何业务数据，所以数据库不可用时错误页仍能显示。已登录后主动改密码、重新绑定 TOTP 仍用 `_Layout`。

### 3.2 Areas（栏目）

每个模块对应一个 Area，Area 名与后端约定 §1.1 的模块目录名一致；权限码格式为 `{area}:{controller}:{action}`（自动生成）。

| Area | 栏目 | 主要页面 | 对应 PRD |
|---|---|---|---|
| Sys | 系统管理 | 用户、角色、权限、系统配置、操作日志、登录日志、任务面板 | FR-028、FR-029 |
| Reference | 基础数据 | 经济体、货币、指标字典 | FR-005、FR-006 |
| Collect | 数据采集 | 数据源、采集任务、运行记录、快照与重新解析、盯盘状态 | FR-001 ~ FR-004 |
| Calendar | 财经日历 | 日历事件、预期值录入、锁定冲突、历史回填 | FR-007 ~ FR-012 |
| Flash | 快讯 | 快讯模板、待审快讯、快讯列表、定时汇总 | FR-013 ~ FR-017 |
| Market | 市场数据 | 央行利率、参考汇率 | FR-018、FR-022 |
| News | 新闻 | 新闻源、待审新闻、敏感词库 | FR-019 ~ FR-021 |
| Publish | 前台发布 | 前台缓存刷新、前台冻结开关 | FR-023、FR-024 |
| Ops | 运维 | 告警、质量校验规则、运营看板 | FR-025 ~ FR-027 |

- 根目录（不属于任何 Area）只放自助和匿名功能：登录（Account）、首页（Home）。
- 跨栏目的链接必须显式写 `asp-area`。

### 3.3 文件组织

按模块和功能分文件夹，不平铺（与后端约定 §1.2 一致）：

```
Suanpan.Crm/
├─ Areas/{模块}/
│  ├─ Controllers/XxxController.cs      [Area("{模块}")] + [PermMenu]
│  └─ Views/
│     ├─ _ViewImports.cshtml
│     ├─ _ViewStart.cshtml               Layout = "_Layout"
│     └─ {Controller}/
│        ├─ Index.cshtml                 列表页
│        ├─ _Form.cshtml                 新增和编辑共用的表单
│        ├─ Detail.cshtml                详情页
│        ├─ _Row.cshtml                  HTMX 局部刷新用的行（如有）
│        └─ _List.cshtml                 HTMX 局部刷新用的列表区块（如有）
├─ Controllers/                          只放根级的 Account、Home，以及开发环境的公共部件示例页 DevComponents
├─ Models/{模块}/                         ViewModel，每个 ViewModel 一个文件；公共部件的参数放 Models/Shared/
├─ TagHelpers/                           公共标签助手（§6）
├─ Filters/                              过滤器（如 DevelopmentOnly）
├─ Auth/                                 权限约定、授权处理
├─ Mvc/                                  MVC 辅助（如 Request.IsHtmx()）
├─ Views/Shared/                         布局、公共局部视图（_Pager、_EmptyState 等）
└─ wwwroot/
   ├─ lib/                               第三方库（自托管）
   ├─ css/
   └─ js/
      ├─ site.js                         公共脚本
      └─ {模块}/                          页面脚本
```

**【与 VVPay 不同】** VVPay 的 ViewModel 按业务域合并在一个文件里（如 `PayViewModels.cs`）；本项目按模块分文件夹，每个 ViewModel 一个文件。

- 列表 ViewModel：筛选字段 + `PagedResult<T> Result`。
- 表单 ViewModel：独立的 `XxxEditViewModel`，带 DataAnnotations 校验和 `[Display]` 中文名。
- 默认值（如日期范围默认本周）在 Controller 里设置，不在视图里计算。

## 4. HTMX 使用约定

### 4.1 只用于这些页面

| 页面 | 用法 | 对应 PRD |
|---|---|---|
| 预期值录入清单 | 行内提交，只刷新当前行，然后自动聚焦下一条 | FR-010、BR-059 |
| 待审快讯 | 列表每 5 秒局部刷新；通过、拒绝、修改只刷新这一行 | FR-016 |
| 盯盘状态 | 每 5 秒局部刷新 | FR-003 |
| 告警列表 | 每 30 秒局部刷新；确认、关闭只刷新这一行 | FR-025 |
| 长任务（重新解析、手动触发采集、手动刷新前台缓存） | 提交后转为后台执行，返回进度区块，每 2 秒刷新直到完成 | FR-002、FR-004、FR-023 |

其余页面一律使用传统表单提交 + 整页刷新。新增 HTMX 页面前，先在本表中登记。

### 4.2 写法

- 同一个 action 根据请求来源返回不同内容：HTMX 请求（请求头 `HX-Request: true`）返回局部视图，普通请求返回完整页面。用公共扩展方法 `Request.IsHtmx()` 判断。
- **HTMX 请求只返回 HTML 片段，不返回 JSON。**
- **不启用 `hx-boost`**，保持普通页面的整页刷新行为。
- **防伪令牌**：布局在 `<body>` 上设置 `hx-headers`，把防伪令牌放进 `RequestVerificationToken` 请求头；POST action 照常标 `[ValidateAntiForgeryToken]`。
- **定时刷新在标签页不可见时暂停**，写法如 `hx-trigger="every 5s [!document.hidden]"`。
- **出错处理**：在 site.js 中统一监听 `htmx:responseError`，在页面顶部显示错误提示。
- **会话超时和无权限**：HTMX 请求未登录（如会话超时）时，服务端返回 401 并带 `HX-Redirect`，htmx 自动整页跳转到登录页；无权限返回 403，按出错处理提示（sys 设计 §5.2）。
- **表单校验**：HTMX 插入的表单，要在 `htmx.onLoad` 中重新执行 `$.validator.unobtrusive.parse`，否则客户端校验不生效。
- 写操作照常标 `[OpLog]`，和整页提交的写操作完全一样。

## 5. 页面写法

### 5.1 列表页

沿用 VVPay 的列表页模板：

- **筛选区**：`<form method="get">`，布局用 `row g-2 align-items-end`，控件一律用 `-sm` 尺寸；提供查询、重置按钮，需要时加导出链接（带上全部筛选参数）。
- **表格**：`.card` 包 `.table table-hover table-sm align-middle card-table`；宽表加 `.table-responsive` 和 `text-nowrap`；数值右对齐，用 `text-end font-monospace`；编码类内容用 `<code class="small">`。
- **分页**：使用公共部件 `_Pager`（§6），自动保留筛选参数。
- **空状态**：没有数据时显示提示，需要时附带新增入口（公共部件 `_EmptyState`）。

### 5.2 表单页

- `asp-for` + `asp-validation-summary` + 字段级 `<span asp-validation-for>`；下拉框用 `asp-items`，选项在 Controller 中准备。
- **校验提示用中文默认提示**：ViewModel 只标 `[Display(Name = "中文名")]`；`Required`、`StringLength`、`MaxLength`、`MinLength`、`Range`、`Compare`、`EmailAddress`、`Phone`、`Url`、`RegularExpression` 没写 `ErrorMessage` 时，由 `Suanpan.Crm/Mvc/ChineseValidationMessages` 统一填中文提示（如"请填写名称"），int、bool 这类值类型的隐式必填和模型绑定错误也是中文。服务端和客户端显示同一句话。只有特殊场景才在特性上单独写 `ErrorMessage`。
- 字段说明用 `.form-text`；复杂字段单独占一行，不要把说明挤在输入框正下方影响校验提示。
- 布尔值用 `form-check form-switch`。
- **表单页必须引用 `_ValidationScriptsPartial`**，确保客户端校验实际生效。**【与 VVPay 不同】** VVPay 的页面没有引用这个 partial，客户端校验实际没有生效。

### 5.3 写操作三件套

1. `[HttpPost]` + `[ValidateAntiForgeryToken]`。
2. 危险操作（删除、撤回、停用、切换开关）用公共部件 `<confirm-form>`（§6），提示文案写清后果。
3. `[OpLog("模块", "动作")]`，必要时加 `[PermName("中文名")]`。

成功：写 `TempData["Message"]` 后重定向（PRG 模式）。失败：`ModelState.AddModelError` 回填表单，或写 `TempData["Error"]` 后重定向。

## 6. 公共部件

**【与 VVPay 不同】** VVPay 要求新列表页照抄模板，筛选、分页、徽章、确认在每页重复。本项目把它们抽成公共部件：

| 部件 | 形式 | 用途 |
|---|---|---|
| `_Pager` | 局部视图 | "共 N 条 · 第 x/y 页" + 上一页、下一页，自动保留筛选参数 |
| `_EmptyState` | 局部视图 | 无数据时的提示和可选的操作入口 |
| `<status-badge>` | 标签助手 | 按枚举值输出统一颜色的徽章（颜色见 §7.1） |
| `<confirm-form>` | 标签助手 | 危险操作：POST 表单 + 防伪令牌 + 二次确认 |
| `asp-permission` | 标签助手（属性） | 当前用户没有指定权限码时不输出该元素，如 `<a asp-permission="calendar:event:edit">`（data-hub BR-152） |
| `<utc-time>` | 标签助手 | 把 UTC 时间按操作者时区显示，悬停显示 UTC 原值（§7.2） |

`asp-permission` 只是体验优化，权限仍以后端校验为准（data-hub BR-145）。

用法（开发环境打开 `/Dev/Components` 可以看到全部部件的实际效果）：

```cshtml
@* 分页：放在列表卡片的表格之后；参数是任意 PagedResult<T> *@
<partial name="_Pager" model="Model.Result" />

@* 空状态：没有数据时代替表格；操作入口可省略 *@
<partial name="_EmptyState" model='new EmptyStateModel("还没有数据源", "新增数据源", Url.Action("Create"))' />

@* 状态徽章：枚举值的文字和颜色在 TagHelpers/StatusBadges 中登记；不对应枚举时直接指定 tone 和 text *@
<status-badge value="@row.Status" />
<status-badge tone="Failure" text="失败" />

@* 危险操作：message 写清后果；表单里可以放其他随表单提交的字段 *@
<confirm-form asp-action="Disable" asp-route-id="@row.Id" message="停用后该账号立即退出登录" button-text="停用" />

@* 时间：库里的 UTC 时间按操作者时区显示，悬停显示 UTC 原值；format 可省略，默认 yyyy-MM-dd HH:mm *@
<utc-time value="@row.CreatedAt" format="yyyy-MM-dd HH:mm:ss" />
```

- 新增需要显示为徽章的状态枚举时，在 `StatusBadges` 中登记它的每个值；测试会检查登记过的枚举每个值都有文字和颜色。
- `<confirm-form>` 的地址生成不出来时直接报错，避免表单退回到提交当前页面。

## 7. 展示规则

### 7.1 状态颜色

| 语义 | Tabler 样式 | 用于 |
|---|---|---|
| 成功 / 完成 | `bg-green-lt` | 已公布、已发布、成功、启用 |
| 进行中 | `bg-blue-lt` | 盯盘中、运行中 |
| 需要关注 | `bg-yellow-lt` | 待复核、待审、待确认、部分成功 |
| 异常 | `bg-orange-lt` | 延迟、限流 |
| 失败 / 终止 | `bg-red-lt` | 失败、已撤回、已拒绝、已下架 |
| 中性 | `bg-secondary-lt` | 待公布、取消、停用 |

### 7.2 时间、数字、外部内容

- **时间**：数据库存 UTC，页面一律通过 `<utc-time>` 按操作者时区显示（默认 Asia/Shanghai，data-hub BR-048）。需要录入当地时间的地方（如 CSV 导入），必须明确标出时区。
- **数字**：解析和格式化用 `CultureInfo.InvariantCulture`；指标数值按指标字典的小数位和单位显示。
- **外部内容**（快照、新闻标题和摘要）：一律按纯文本转义输出，**禁止使用 `Html.Raw`**；快照用 `<pre>` 显示源码，不渲染（data-hub BR-025）。
- **CSV 导出**：UTF-8 带 BOM，保证 Excel 打开中文不乱码；时间按操作者时区，表头写明时区；以 `=`、`+`、`-`、`@` 开头的单元格前加单引号，防止在 Excel 中被当作公式执行（CSV 注入）。统一用 `Suanpan.Services.Common.Csv.CsvWriter` 写出。

## 8. 安全纪律

沿用 VVPay：

- 隐藏按钮只是体验优化，真正的防线永远是服务端权限（自动绑定、默认拒绝）。
- 所有 POST 都必须带防伪令牌；删除等写操作必须用表单 + 确认，绝不用 GET 链接。
- 登录失败统一提示"用户名或密码错误"。
- 敏感值（如数据源的 API Key）只在显式点击"查看"后按权限返回，并记操作日志；页面不留存明文。

---

# 第二部分：前台（Portal）

## 9. 技术栈

| 组件 | 位置 | 用途 |
|---|---|---|
| ASP.NET Core MVC + Razor | — | 服务端渲染全部内容（web-portal BR-041） |
| Bootstrap 5（自托管）+ 自定义主题 | `wwwroot/lib/bootstrap/`、`wwwroot/css/` | 基础样式。**建议，待设计稿确认**；不用 Tabler |
| HTMX | `wwwroot/lib/htmx/` | 快讯流、日历区块的局部刷新 |
| Alpine.js | `wwwroot/lib/alpinejs/` | 计算器、时区切换等页面内交互 |
| 计算公式脚本 | `wwwroot/js/calc/` | 计算器公式，纯函数，与界面分离 |

### 9.1 视觉

- 前台样式单独设计，不沿用后台的 Tabler 布局（web-portal D-005）。
- 字体用系统字体或自托管的可商用开源字体（如 SIL OFL 许可），不用 Google Fonts。
- 利好、利空不能只靠颜色区分，必须同时配文字或符号（如 ▲ 利好美元 / ▼ 利空美元），满足 WCAG 2.1 AA。

### 9.2 目录组织

按功能分文件夹，不平铺：

```
Suanpan.Portal/
├─ Controllers/                 按页面类型，一个功能一个控制器：Home、Calendar、Indicator、Flash、
│                               CentralBank、Tools、MarketHours、News
├─ Views/
│  ├─ {控制器}/                  页面视图和该页面的局部视图
│  └─ Shared/                   布局、公共部件（_Footer、_NotAdviceBadge 等，§16）
├─ Models/{功能}/                ViewModel，每个 ViewModel 一个文件
├─ Caching/                     输出缓存策略、缓存失效的消息处理器（§14）
├─ Localization/                多语言路由
├─ Resources/                   界面文案的 resx 文件（中、英）
├─ Seo/                         sitemap、结构化数据（§13）
└─ wwwroot/
   ├─ lib/                      第三方库（自托管）
   ├─ css/
   └─ js/
      ├─ site.js                公共脚本（时间显示、局部刷新出错处理）
      ├─ calc/                  计算器公式（纯函数）及其测试
      └─ {功能}/                 页面脚本（如 tools/ 下的计算器组件）
```

## 10. 交互约定

### 10.1 HTMX（局部刷新）

| 区块 | 刷新频率 | 对应 PRD |
|---|---|---|
| 快讯流（快讯页、首页快讯区） | 每 10 秒 | web-portal FR-006、BR-046 |
| 今日日历区块 | 每 60 秒 | web-portal FR-003、FR-004 |

- 写法与后台一致：只返回 HTML 片段，不启用 `hx-boost`，标签页不可见时暂停刷新。
- 连续失败 3 次后显示"数据更新中断"，恢复后自动消失（web-portal BR-047）；在公共脚本中统一用 `htmx:responseError` 计数实现。

### 10.2 Alpine.js（计算器）

- 每个计算器是一个 Alpine 组件，负责输入、校验和展示；**计算公式写在 `wwwroot/js/calc/` 下的纯函数里**，组件只调用公式。
- 计算只在浏览器本地完成，输入不发往服务器（web-portal BR-025）。
- 公式用 Node 自带的测试运行器（`node --test`）跑测试用例，每个计算器至少 5 组（web-portal FR-008 验收标准）。这只用于测试，不引入构建流程。
- 需要汇率时，页面渲染时把当天的参考汇率和日期写进页面，组件读取使用；用户可以手动覆盖（web-portal BR-026）。

### 10.3 浏览器本地偏好（P1）

- 语言、时区、日历筛选、计算器常用参数存在 `localStorage`（web-portal FR-013）。
- 所有读写都包在 try/catch 中；无法使用本地存储时，页面按默认值正常工作。

## 11. 路由与多语言

- 中英文使用独立的 URL 前缀：`/zh/...`、`/en/...`；根路径 `/` 按浏览器语言跳转，并作为 `x-default`（web-portal BR-001 ~ BR-003）。
- URL 全小写、用短横线连接、保持稳定，示例：

| 页面 | URL |
|---|---|
| 财经日历 | `/zh/calendar?view=week&country=us&importance=high` |
| 指标详情 | `/zh/indicator/us-nfp` |
| 快讯 | `/zh/flash`，按日归档 `/zh/flash/2026-10-17` |
| 央行利率 | `/zh/central-banks` |
| 计算器 | `/zh/tools/position-size` |
| 交易时段 | `/zh/market-hours` |

- 筛选条件都放在查询参数里，复制链接即可还原（可深链，web-portal BR-009）。
- 界面文案放在 resx 资源文件中（中、英各一份）；指标名、快讯等数据文本来自数据库的中英文字段。
- 切换语言时停留在同一内容的对应语言页面。

## 12. 时间显示

- 服务端输出 `<time datetime="2026-10-17T12:30:00Z">2026-10-17 12:30 UTC</time>`，由公共脚本按访客时区（或手动选择的时区）改写显示。
- **关闭 JavaScript 时仍显示 UTC 时间**，不能出现空白（web-portal BR-007）。
- 所有显示的时间都带时区标识，如 UTC+8（web-portal BR-006）。

## 13. SEO

- 主要内容都在服务端生成的 HTML 中，不依赖 JavaScript 渲染。
- 每个页面由 Controller 设置 title、description、canonical 和 hreflang，中英文分别撰写，title 不重复（web-portal BR-042）。
- `sitemap.xml` 按语言由程序动态生成，自动包含新的指标页和汇总页；提供 `robots.txt`。
- 按页面类型输出 schema.org 的 JSON-LD 结构化数据（具体类型在技术方案中确定）。

## 14. 缓存

- 页面使用 ASP.NET Core 输出缓存，按页面类型设置策略和标签，缓存最长有效期与 data-hub BR-118 一致：

| 标签 | 页面 | 最长有效期 |
|---|---|---|
| `flash` | 快讯页、首页快讯区 | 10 秒 |
| `calendar` | 日历页、今日日历区块 | 60 秒 |
| `indicator:{编码}` | 指标详情页 | 10 分钟 |
| `cb`、`fx` | 央行利率页、计算器页 | 10 分钟 |

- 收到 `PublicDataChangedEvent` 后，按标签让缓存失效（`EvictByTagAsync`），同时刷新 CDN 缓存（data-hub FR-023）。
- 响应头的 `Cache-Control` 与上表一致；HTMX 局部刷新的接口同样使用短时缓存。
- Portal 部署多个实例时，输出缓存改存 Redis，保证各实例同时失效。

## 15. 数据读取

- 只通过 `PortalReadDbContext` 查询 `v_pub_*` 对外视图，所有查询用 `AsNoTracking`（见后端约定 §6.8）。
- 不引用 Services 中的写操作；前台没有任何写数据库的功能。
- 数据库不可用时：已缓存的页面继续提供；未缓存的页面显示"数据暂时无法获取"（data-hub ERR-PUB-002）。

## 16. 合规展示

| 公共部件 | 内容 | 依据 |
|---|---|---|
| `_Footer` | 风险提示、免责声明、数据来源说明、联系与投诉邮箱、隐私说明、备案号位置（迁回大陆后显示） | web-portal BR-037 |
| `_NotAdviceBadge` | 在利多利空结论和日历着色旁显示"不构成投资建议" | web-portal BR-021、data-hub BR-080 |
| `_MachineTranslatedBadge` | 机器翻译的新闻显示"机器翻译" | web-portal BR-004 |

- 全站不出现开户、入金、交易引导和经纪商推广内容，中英文站一致（web-portal BR-038）。
- 风险提示和免责声明的文案以法务定稿为准（web-portal Q-007）。

## 17. 性能

- 大陆 4G 网络下首屏 LCP 的 P75 ≤ 2.5 秒；首屏加载的 JavaScript ≤ 200 KB（gzip 后）（web-portal NFR-001、NFR-002）。
- 脚本一律 `defer` 加载；只有用到 Alpine.js 的页面才加载 Alpine.js。
- 图片使用 WebP，并写明宽高，避免布局抖动。

---

## 变更记录

| 版本 | 日期 | 修改人 | 内容 |
|---|---|---|---|
| v1.0 | 2026-10-17 | vivo.li / Claude | 初版：基于 VVPay 前端框架、data-hub 与 web-portal PRD v1.0.0.2 的决策整理 |
| v1.1 | 2026-10-17 | vivo.li / Claude | §3.2 新增 Publish Area，Area 名与模块名统一，系统管理的 Area 名为 `Sys`；§3.3 后台目录按模块分文件夹；新增 §9.2 前台目录组织；明确约定未覆盖的情况不参考 VVPay |
| v1.2 | 2026-10-18 | vivo.li / Claude | §7.2 CSV 导出补充时区和 CSV 注入防护 |
| v1.3 | 2026-10-18 | vivo.li / Claude | §3.3 目录加入 `Mvc/`；§4.2 补充 HTMX 请求的未登录和无权限处理 |
| v1.4 | 2026-10-18 | vivo.li / Claude | ENG-004.2 实施：§2 写明各库的版本和目录、只放 min 文件和 LICENSE；§3.1 布局的错误提示区和确认弹窗；§3.3 开发环境示例页、Models/Shared；§5.2 中文默认校验提示；§6 公共部件的用法 |
| v1.5 | 2026-10-18 | vivo.li / Claude | §3.1 账号页面使用 `_LayoutAccount` |
