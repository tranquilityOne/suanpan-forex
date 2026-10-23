# reference 模块（基础数据）技术设计

| 项目 | 内容 |
|---|---|
| 版本 | v0.10 |
| 日期 | 2026-10-23 |
| 状态 | 已评审（2026-10-22，vivo.li） |
| 覆盖 | FR-005 经济体与货币管理、FR-006 指标字典管理（指标与数据源的映射只定表结构） |
| 依据 | data-hub PRD v1.0.0.15；web-portal PRD（FR-005 指标详情页，BR-015、BR-017）；后端约定 v1.23；前端约定 v1.9；数据库变更约定 |

> 本文只写"怎么实现"。需求、业务规则和验收标准以 PRD 为准，这里引用编号，不复述。
> 实现偏离本文时，在同一个 PR 里更新本文。

---

## 1. 范围

**包含**：货币、经济体、指标字典三类主数据的表结构、领域规则、服务和后台页面；预置的 9 种货币和 10 个经济体；首批指标（PRD §10.9）的导入方式；指标与数据源映射的表结构。

**不包含**：
- 指标与数据源映射的页面和服务：映射要选择已存在的数据源，数据源由采集模块（FR-001）提供，等 FR-001 完成后再做（§2.1 已定表结构）。
- 主源失败时改用备源（FR-006 验收第 3 条）：属于采集执行，在采集模块实现。
- 盯盘范围（FR-003）、预告快讯（FR-015）、前台缓存刷新（FR-023）：它们读取本模块的数据，见 §4。
- 质量校验（FR-026）：本模块只存校验参数（`range_min`、`range_max`、`sigma_n`），校验逻辑在 FR-026 实现。
- 央行（FR-018）、指标历史回填（FR-012）。
- 前台对外视图（`v_pub_*`）：由发布模块（FR-023）定义。

| 需求 | 本文章节 |
|---|---|
| FR-005 经济体与货币（BR-026 ~ BR-028） | §2、§3.1 ~ §3.3、§3.6、§6 |
| FR-006 指标字典（BR-029 ~ BR-035） | §2、§3.1、§3.4 ~ §3.6、§4、§6 |
| PRD §8.4 初始化数据 | §2.4 |

---

## 2. 数据设计

表名前缀 `ref_`（后端约定 §6.2）。实体不写导航属性，不建物理外键，关联列加普通索引（后端约定 §6.1）。中英文成对的列（`name_zh` / `name_en` 等）在实体中合成一个双语文本属性，见 §3.7。

### 2.1 表清单

货币、经济体、指标用编码作主键（PRD §8.1.2）：其他模块（日历事件、快讯模板、央行）按编码引用，编码创建后不可修改（§3.1）。

**`ref_currency`（货币）** —— 带审计四件套

| 列 | 类型 | 可空 | 说明 |
|---|---|---|---|
| `code` | `char(3)` | 否 | 主键，ISO 4217，大写，如 `USD` |
| `name_zh` | `varchar(32)` | 否 | 如"美元" |
| `name_en` | `varchar(64)` | 否 | 如"US Dollar" |
| `is_enabled` | `boolean` | 否 | 停用后不能再被新选用（§3.5） |

**`ref_economy`（经济体）** —— 带审计四件套

| 列 | 类型 | 可空 | 说明 |
|---|---|---|---|
| `code` | `char(2)` | 否 | 主键，ISO 3166-1 alpha-2，欧元区为 `EA`（BR-026） |
| `name_zh` | `varchar(32)` | 否 | |
| `name_en` | `varchar(64)` | 否 | |
| `currency_code` | `char(3)` | 否 | 关联货币（BR-027） |
| `timezone` | `varchar(64)` | 否 | IANA 时区，如 `America/New_York`（BR-027）；本经济体指标的默认发布时间按这个时区解释 |
| `sort` | `smallint` | 否 | 列表和下拉框中的显示顺序 |
| `is_enabled` | `boolean` | 否 | |

**`ref_indicator`（指标）** —— 带审计四件套

| 列 | 类型 | 可空 | 说明 |
|---|---|---|---|
| `code` | `varchar(32)` | 否 | 主键，如 `US.NFP`、`US.CPI.YOY`（BR-029） |
| `economy_code` | `char(2)` | 否 | 创建后不可修改：编码以经济体开头 |
| `currency_code` | `char(3)` | 否 | 新增时默认取经济体的货币，可以改（如德国指标对应 EUR） |
| `name_zh`、`name_en` | `varchar(64)`、`varchar(128)` | 否 | 全称 |
| `short_name_zh`、`short_name_en` | `varchar(32)`、`varchar(64)` | 否 | 简称，日历和快讯中使用 |
| `category` | `smallint` | 否 | 类别（§3.6） |
| `value_type` | `smallint` | 否 | 数值 / 区间（BR-032） |
| `unit` | `smallint` | 否 | 单位（§3.6） |
| `decimals` | `smallint` | 否 | 小数位，0 ~ 6（数值列为 `numeric(18,6)`） |
| `frequency` | `smallint` | 否 | 发布频率 |
| `importance` | `smallint` | 否 | 重要性：高 / 中 / 低 |
| `direction` | `smallint` | 否 | 影响方向（BR-031） |
| `flat_tolerance` | `numeric(18,6)` | 否 | 持平容差，默认 0（BR-074） |
| `range_min`、`range_max` | `numeric(18,6)` | 是 | 合理上下限（BR-133，FR-026 使用） |
| `sigma_n` | `numeric(18,6)` | 是 | 偏离几倍标准差算异常，为空时用默认值 4（BR-133，FR-026 使用） |
| `agency_zh`、`agency_en` | `varchar(64)`、`varchar(128)` | 否 | 发布机构，如"美国劳工统计局" / "Bureau of Labor Statistics"；前台中英文指标详情页都要显示（PRD §8.1.2） |
| `default_release_time` | `time` | 否 | 默认发布时间，机构当地时间，时区取经济体的 `timezone` |
| `description_zh`、`description_en` | `text` | 是 | 说明（定义、计算方式、为什么重要），前台指标详情页使用；为空时前台不展示（BR-033） |
| `is_enabled` | `boolean` | 否 | |

**`ref_indicator_source`（指标与数据源的映射）** —— 带审计四件套；本文只定结构，建表随 FR-001 之后的子任务一起做

| 列 | 类型 | 可空 | 说明 |
|---|---|---|---|
| `id` | `bigint` | 否 | 主键，自增 |
| `indicator_code` | `varchar(32)` | 否 | |
| `source_id` | `bigint` | 否 | 数据源 id（采集模块的数据源表） |
| `external_id` | `varchar(128)` | 否 | 数据源中的标识，如 BLS 的序列 ID |
| `parser_key` | `varchar(64)` | 否 | 解析器标识，对应 Collector 中的解析器 |
| `priority` | `smallint` | 否 | 1 为主源，2 起为备源，数字越小越优先（BR-034） |

### 2.2 索引

| 表 | 索引 | 用途 |
|---|---|---|
| `ref_economy` | `ix_ref_economy_currency_code` | 删除货币前检查引用 |
| `ref_indicator` | `ix_ref_indicator_economy_code`、`ix_ref_indicator_currency_code` | 按经济体筛选；删除前检查引用 |
| `ref_indicator_source` | `ux_ref_indicator_source_indicator_code_source_id` | 一个指标对同一数据源只映射一次（PRD §8.1.2） |
| | `ux_ref_indicator_source_indicator_code_priority` | 同一指标的优先级不重复，主备顺序明确 |
| | `ix_ref_indicator_source_source_id` | 按数据源查映射 |

指标总量在几百条以内，列表的其他筛选条件（类别、重要性、状态）不建索引。

### 2.3 数据库授权

`ref_` 表的读写权限来自第一个迁移中设置的默认授权（sys 设计 §3.3），不需要逐表授权。前台账号 `suanpan_portal` 对 `ref_` 表没有权限，前台通过发布模块定义的对外视图读取。

### 2.4 初始化数据

| 数据 | 方式 | 说明 |
|---|---|---|
| 9 种货币 | 随迁移写入：在迁移中手写 `InsertData`。不用 `HasData`：它会丢掉复合类型（中英文文本）的值（数据库变更约定 §3.4） | USD、EUR、GBP、JPY、CNY、CHF、CAD、AUD、NZD |
| 10 个经济体 | 同上 | 见下表；`created_by` 为 `migration`，`created_at` 为固定时间 |
| 首批指标（50 个，清单见 [reference-indicators.md](reference-indicators.md)，2026-10-23 确认） | 只执行一次的数据迁移 `ImportReferenceInitialIndicators`（§8 第 2 项）；编码已存在时跳过，`Down` 只删本迁移写入的行 | 用 `INSERT ... ON CONFLICT (code) DO NOTHING`，不用 `HasData`：指标上线后会在后台修改，`HasData` 的种子数据一变，迁移就会覆盖后台的修改 |

预置经济体（BR-026、BR-027，PRD FR-005；德国单列为 DE，PRD Q-008 已确认）：

| 编码 | 中文 | 英文 | 货币 | 时区 | 顺序 |
|---|---|---|---|---|---|
| US | 美国 | United States | USD | America/New_York | 10 |
| EA | 欧元区 | Euro Area | EUR | Europe/Brussels | 20 |
| DE | 德国 | Germany | EUR | Europe/Berlin | 30 |
| GB | 英国 | United Kingdom | GBP | Europe/London | 40 |
| JP | 日本 | Japan | JPY | Asia/Tokyo | 50 |
| CN | 中国 | China | CNY | Asia/Shanghai | 60 |
| CH | 瑞士 | Switzerland | CHF | Europe/Zurich | 70 |
| CA | 加拿大 | Canada | CAD | America/Toronto | 80 |
| AU | 澳大利亚 | Australia | AUD | Australia/Sydney | 90 |
| NZ | 新西兰 | New Zealand | NZD | Pacific/Auckland | 100 |

欧元区的时区取 `Europe/Brussels`：欧盟统计局、欧洲央行所在地都使用中欧时间，夏令时规则相同，取哪个城市结果一样。

---

## 3. 领域规则（`Suanpan.Core/Reference/Rules/`）

### 3.1 编码（`ReferenceCodeRule`）

- 货币编码：3 个大写字母（ISO 4217）。经济体编码：2 个大写字母（ISO 3166-1 alpha-2，欧元区 `EA`）。只校验格式，不内置 ISO 清单。
- 指标编码：`{经济体}.{简称}`，简称由大写字母和数字组成，可以有多段，如 `US.CPI.YOY`；总长不超过 32；必须以所属经济体的编码开头（BR-029）。
- **编码创建后不可修改**，货币、经济体、指标都一样（BR-026、BR-029）：其他模块按编码引用；前台指标详情页的地址由指标编码生成（web-portal BR-015：地址稳定），规则为转小写、`.` 换成 `-`，如 `US.NFP` → `us-nfp`，由 `ReferenceCodeRule.ToSlug` 提供。
- 指标的所属经济体同样不可修改（编码以经济体开头，BR-029）。录错时：没有被引用的直接删除后重建，已被引用的停用后重建。

### 3.2 时区（`TimeZoneRule`）

- 合法的时区：能用 `TimeZoneInfo.TryFindSystemTimeZoneById` 找到的 IANA 名称（ERR-REF-003）。.NET 在 Windows 上也能识别 IANA 名称，开发环境和 Linux 部署环境行为一致。
- 页面下拉框的选项：取系统时区，转成 IANA 名称后去重排序，按 `/` 前的大区分组。Linux 上是完整的 IANA 列表；Windows 开发环境只有一百多个主要时区，够开发使用。
- 部署环境需要时区数据：Linux 上 .NET 从系统的 IANA 时区库（tzdata）读取，编写 Dockerfile（JAN-52）时要用带 tzdata 的基础镜像。
- 时区换算（当地时间 → UTC，含夏令时）不在本模块，见 ENG-008.3（JAN-61）。

### 3.3 经济体与货币的保存

- 新增：编码格式正确、不重复（ERR-REF-001）；经济体的货币必须存在且启用；时区合法。
- 修改：编码不可改；改货币时，新货币必须存在且启用。

### 3.4 指标的保存（`IndicatorRule`）

- 新增：编码格式正确、以经济体开头、不重复（ERR-IND-001）；经济体和货币必须存在且启用；必填字段齐全（BR-030）；小数位 0 ~ 6；持平容差 ≥ 0；上下限都填时下限 ≤ 上限；标准差倍数 `sigma_n` 可以不填，填写时必须大于 0（JAN-77 实现）。
- 修改：编码和经济体不可改。**影响方向变化时必须填写理由**（BR-031，ERR-IND-002）：表单上有"修改理由"字段，服务端判断方向是否变化；理由由操作日志过滤器写入 `sys_op_log.reason`（sys 设计 §7.1 按名为 `Reason` 的属性采集），审计日志可查（FR-006 验收第 2 条）。不另建方向变更历史表（PRD DEC-018：操作日志只记请求参数）。
- 数值类型为"区间"的指标，日历事件同时存上下限（BR-032），本表只记类型。
- 实现：`IndicatorRule.Validate(Indicator)` 校验上面的字段规则，返回第一条错误 `IndicatorRuleError(Field, Message)`，`Field` 为表单字段名（中英文字段为属性名加 `Zh` / `En`，如 `NameZh`），服务转成 `ManageResult.Fail(Message, Field)`；`IndicatorRule.ValidateDirectionChange(原方向, 新方向, 理由)` 判断 BR-031。编码格式和前缀由 `ReferenceCodeRule.ValidateIndicatorCode(编码, 经济体)` 校验。编码是否重复、经济体和货币是否存在且启用要查库，在服务中校验。
- `sigma_n` 为空时取 `IndicatorRule.DefaultSigmaN`（4）。

### 3.5 启停与删除（BR-028）

- 停用的货币、经济体、指标不能再被新选用：下拉框只列出启用的；服务端同样校验。已有的引用不受影响。
- 停用的指标不再生成新的日历事件、不再盯盘：由日历、采集模块读取 `is_enabled` 实现。
- **删除只允许没有被引用的数据**；被引用时拒绝，并列出引用方（ERR-REF-002）。引用检查放在各服务的私有方法中，后续模块新增引用表时在这里补充：

| 被引用的数据 | 当前检查 | 后续模块加入后补充 |
|---|---|---|
| 货币 | 经济体、指标 | 央行（`cb_bank`）、参考汇率 |
| 经济体 | 指标 | 央行 |
| 指标 | —（引用它的表都还没建） | 指标与数据源的映射（JAN-79）、日历事件、快讯模板、央行（决议指标） |

指标按 BR-153 处理：被引用的只能停用，没有被引用的可以删除。

### 3.6 枚举（`Suanpan.Core/Reference/Enums/`）

存为 `smallint`（后端约定 §6.3），取值从 1 开始连续编号，已有的值不能改；中英文显示名由 `ReferenceDisplayNames.Get(枚举值)` 统一提供，后台和快讯共用，每个枚举值都要登记（单元测试检查）。英文显示名随首批指标确认（2026-10-23）。重要性的取值为低 1、中 2、高 3，可以按大小比较。

| 枚举 | 取值 |
|---|---|
| `IndicatorCategory` 类别 | 就业、通胀、增长、消费、贸易、利率、景气、其他 |
| `IndicatorValueType` 数值类型 | 数值、区间 |
| `ReleaseFrequency` 发布频率 | 周、月、季、不定期 |
| `Importance` 重要性 | 高、中、低 |
| `ImpactDirection` 影响方向 | 越高越利好本币、越高越利空本币、无方向 |
| `IndicatorUnit` 单位 | 百分比（%）、指数（不带单位）、人、千人、亿美元、千个（不是人数的数量，如 JOLTS 职位空缺）；随首批指标确认（§8 第 3 项） |

单位决定数值的存储口径：公布值按指标的单位和小数位标准化存储（BR-043），解析时原始单位与字典不一致即解析失败（ERR-PARSE-006）；对外显示的格式（如"千人"在中文页面显示为"万人"）由快讯和前台按 NFR-072 处理。单位清单按首批指标整理，以后有新单位时在代码中增加枚举值。

### 3.7 中英文文本（`Suanpan.Core/Common/Localization/`）

存储：每种语言一列，如 `name_zh`、`name_en`（后端约定 §6.3）。不用 JSON：语言固定为简体中文和英文（web-portal NFR-070），分列能由数据库直接约束必填和长度。

代码：
- 中英文成对的字段在实体中用一个 `BilingualText` 属性表示，含 `Zh`、`En` 两个属性，以及按语言取值的 `Get(ContentLanguage)`；语言枚举 `ContentLanguage` 取值为中文、英文。
- EF Core 用 `ComplexProperty` 把它映射成两列，列名在实体配置中显式指定；必填的字段（如名称）两列都 `NOT NULL`，可空的字段（如说明）两列都可空。
- 其他模块和后台页面按语言取文字时都调用 `Get`，不直接读某一种语言的属性；后台表单仍是每种语言一个输入框。前台通过对外视图读取，视图的列由发布模块（FR-023）定义。
- 本模块用到的字段：货币和经济体的名称；指标的名称、简称、发布机构、说明；枚举的中英文显示名（`ReferenceDisplayNames` 返回 `BilingualText`）。
- 可空的字段（指标说明）在实体中为 `BilingualText?`，用 `HasOptionalBilingualColumns` 映射：两种语言都没填时为 null，两列都存 NULL；只填一种时另一种存空串，前台按"没有说明"处理（BR-033）。

以后增加语言时：
- 繁体中文（web-portal Q-002，v1.1）一般由简体在显示时自动转换，不需要另存，不改表。
- 需要人工维护的新语言：把这些字段的存储改成 `jsonb`（按语言存），`BilingualText` 内部改成按语言的字典，`Get` 的调用方不变。表都不大，按数据库变更约定分两次发布：先新增 `jsonb` 列、回填并切换读写，再删除旧列。

---

## 4. 与其他模块的关系

| 模块 | 关系 |
|---|---|
| 采集（Collect） | 指标与数据源的映射（§2.1）；主源失败改用备源；盯盘计划每次生成时直接读取指标的重要性和启用状态，不缓存，所以重要性修改后下一次盯盘计划立即生效（BR-035、FR-006 验收第 4 条，由采集模块的设计保证） |
| 日历（Calendar） | 日历事件按指标编码引用；排期时间按经济体时区和默认发布时间换算（ENG-008.3） |
| 快讯（Flash） | 利多利空判定用影响方向和持平容差（BR-074，ENG-008.2）；预告快讯按重要性筛选（BR-035，读库时即生效） |
| 发布（Publish） | 前台读取经济体、指标的对外字段；重要性、名称、说明等对外字段变化时，需要通知前台刷新缓存（BR-035）。消息 `PublicDataChangedEvent` 的契约由 FR-023 定义，本模块在 FR-023 实现时接入，M1 不发送 |
| 央行（CentralBank） | 央行引用货币、经济体和决议指标 |

本模块第一阶段不做缓存：指标只有几百条，各模块按需查库。需要时按 sys 设计 §6.5 的方式增加。

---

## 5. 代码结构与服务接口

### 5.1 目录

```
Suanpan.Core/Common/Localization/   BilingualText、ContentLanguage（跨模块共用，§3.7）

Suanpan.Core/Reference/
├─ Entities/      Currency、Economy、Indicator、IndicatorSource
├─ Enums/         IndicatorCategory、IndicatorValueType、IndicatorUnit、ReleaseFrequency、Importance、ImpactDirection
└─ Rules/         ReferenceCodeRule、TimeZoneRule、IndicatorRule、IndicatorRuleError、ReferenceDisplayNames（枚举的中英文显示名）

Suanpan.Data/
├─ SuanpanDbContext.Reference.cs
└─ Configurations/Reference/      各表的实体配置；货币、经济体的 HasData

Suanpan.Services/Reference/
├─ Currencies/    ICurrencyService、CurrencyService、Models/
├─ Economies/     IEconomyService、EconomyService、Models/
└─ Indicators/    IIndicatorService、IndicatorService、Models/

Suanpan.Crm/
├─ Areas/Reference/   Controllers/（Economy、Currency、Indicator）、Views/
└─ Models/Reference/  各页面的 ViewModel
```

### 5.2 主要接口

写操作都返回 `ManageResult`（`Suanpan.Shared.Mvc`），失败时带面向用户的提示，与 sys 模块一致；错误属于某个输入项时带上字段名（`Field`），页面把提示显示在该输入框下（ERR-REF-001、ERR-REF-003）。编码在服务中先去空白、转大写再校验，页面输入小写也可以。

| 接口 | 方法 | 说明 |
|---|---|---|
| `ICurrencyService` | `GetListAsync`、`GetAsync(code)`、`GetOptionsAsync`、`CreateAsync`、`UpdateAsync`、`SetEnabledAsync(code, enabled)`、`DeleteAsync(code)` | `GetOptionsAsync` 只返回启用的，供下拉框使用 |
| `IEconomyService` | 同上，另有 `GetNextSortAsync` | 列表按 `sort` 排序，带各经济体的指标数；新增页的显示顺序默认取当前最大值加 10 |
| `IIndicatorService` | `QueryAsync(IndicatorQuery)`、`GetAsync(code)`、`CreateAsync`、`UpdateAsync`、`SetEnabledAsync`、`DeleteAsync` | `IndicatorQuery`：经济体、类别、重要性、启用状态、关键字（编码或名称包含），分页；`UpdateAsync` 的输入带 `Reason` |

删除被拒时，`ManageResult` 的提示中列出引用方，如"被以下数据引用，不能删除，可以停用：指标 US.NFP、US.CPI.YOY"（每类最多列 10 个，其余给出总数），由 `Services/Reference/Common/ReferenceUsage` 拼写。

---

## 6. 后台页面（Area：Reference）

栏目"基础数据"已在栏目清单中（sys 设计 §6.2，图标 `database`，排序 40）。所有写操作都标 `[OpLog("基础数据", "...")]`；本模块不使用 HTMX。

| 页面 | 路由 | 权限码 | 说明 |
|---|---|---|---|
| 经济体 | `/Reference/Economy` | `reference:economy:index` | 编码、名称、货币、时区、状态 |
| 新增 / 编辑经济体 | `/Reference/Economy/Create`、`/Edit/{code}` | `reference:economy:create` / `edit` | 编辑时编码只读；时区为分组下拉框 |
| 启用 / 停用 | POST | `reference:economy:set_status` | 二次确认 |
| 删除 | POST | `reference:economy:delete` | 二次确认；被引用时提示引用方 |
| 货币 | `/Reference/Currency` 及新增、编辑、启停、删除 | `reference:currency:index` / `create` / `edit` / `set_status` / `delete` | 同经济体 |
| 指标字典 | `/Reference/Indicator` | `reference:indicator:index` | 筛选：经济体、类别、重要性、状态、关键字；列：编码、简称、经济体、重要性、影响方向、频率、状态 |
| 指标详情 | `/Reference/Indicator/Detail/{code}` | `reference:indicator:detail` | 只读，显示全部字段（含说明），供没有编辑权限的角色查看 |
| 新增 / 编辑指标 | `/Reference/Indicator/Create`、`/Edit/{code}` | `reference:indicator:create` / `edit` | 字段分组：基本信息、数值、发布、说明、质量校验参数。新增时货币默认取经济体的货币；编辑时编码、经济体只读。修改影响方向时"修改理由"必填 |
| 启用 / 停用、删除指标 | POST | `reference:indicator:set_status` / `delete` | 二次确认 |
| 指标的数据源映射 | 待 FR-001 完成后设计 | `reference:indicator_source:*` | 管理员、采集工程师可编辑（PRD §13.1.2） |

菜单排序：经济体 10、货币 20、指标字典 30。列表页、表单页的写法按前端约定 §5，操作按钮按权限隐藏（`asp-permission`）。

权限码的第一段是 Area 名转成的 snake_case（`Reference` → `reference`，sys 设计 §4.4），不是表名前缀 `ref_`。路由中的 `{code}` 不区分大小写。指标详情的 action 名为 `Detail`（前端约定 §3.3 的详情页命名，默认显示名"详情"），权限码为 `reference:indicator:detail`。

货币列表显示使用该货币的经济体数和指标数，经济体列表显示指标数，大于 0 时删除按钮置灰并提示"可以停用"。编辑经济体、指标时，货币下拉框保留当前使用的货币（即使已停用，标"已停用"），其余只列启用的。

指标表单（`wwwroot/js/reference/indicator-edit.js`）：新增时选了经济体，货币默认设为经济体的货币；编辑时影响方向一改变，"修改理由"就标为必填，是否必填仍以服务端判断为准。修改理由的属性名为 `Reason`，由操作日志过滤器写入原因列。保存后回到详情页。下拉框选项放在可空的 `Options` 对象里，绑定时为空，操作日志的请求参数中不会出现选项。

### 6.1 权限码与预置角色

按 PRD §13.1.2，补充到 `PresetRoleGrants`（sys 设计 §4.5）：

| 角色 | 通配规则 |
|---|---|
| 采集工程师 `engineer` | `reference:*:index`、`reference:*:detail`、`reference:indicator_source:*` |
| 编辑 `editor` | `reference:*:index`、`reference:*:detail`、`reference:economy:*`、`reference:currency:*`、`reference:indicator:*` |
| 只读 `viewer` | `reference:*:index`、`reference:*:detail` |

---

## 7. 错误码

| 错误码 | 场景 | 实现 |
|---|---|---|
| ERR-REF-001 | 经济体或货币编码重复 | 服务返回失败，表单在编码字段提示 |
| ERR-REF-002 | 删除被引用的经济体或货币 | 拒绝删除，提示中列出引用方（§5.2） |
| ERR-REF-003 | 时区非法 | 服务校验（§3.2），表单在时区字段提示；页面只提供下拉选择 |
| ERR-IND-001 | 指标编码重复 | 同 ERR-REF-001 |
| ERR-IND-002 | 修改影响方向未填理由 | 表单在"修改理由"字段提示 |
| ERR-IND-003 | 映射的数据源已停用 | 警告但不阻止保存；随数据源映射一起实现 |

---

## 8. 评审结论

2026-10-21 vivo.li 确认，均按初稿的建议执行：

| # | 问题 | 结论 | 落实 |
|---|---|---|---|
| 1 | 德国单列为 DE 还是归入欧元区（PRD Q-008） | 单列 DE，货币 EUR | PRD Q-008 改为已确认；预置数据见 §2.4 |
| 2 | 首批指标怎么导入（PRD §8.4："编辑确认后导入"） | 编辑在表格中确认 §10.9 拆分后的约 50 个指标（含单位、小数位、影响方向、默认发布时间），开发据此写一个只执行一次的数据迁移；以后的增改在后台进行。不做 CSV 导入页面 | §2.4；已完成（JAN-78） |
| 3 | 单位清单（§3.6）是否够用 | 先按初稿实现，编辑确认首批指标时一并确认；不够用时在代码中增加枚举值 | §3.6；确认时增加"千个"（JAN-78） |
| 4 | 发布机构分中英文两个字段 | 分开：前台中英文指标详情页都要显示发布机构（web-portal FR-005） | PRD §8.1.2 改为 `agency_zh`、`agency_en` |
| 5 | 没有被引用的指标能否删除 | 可以；被引用的只能停用，与 BR-028 一致 | PRD 新增 BR-153 |
| 6 | 编码和指标的所属经济体创建后能否修改 | 不能（§3.1）：其他模块按编码引用，前台地址由编码生成 | PRD BR-026、BR-029 补充 |

---

## 9. 测试要点

| 验收标准 / 规则 | 测试方式 |
|---|---|
| FR-005 #1 初始化后有 10 个经济体及其货币 | 集成测试：执行迁移后查询，编码、货币、时区与 §2.4 一致 |
| FR-005 #2 编码唯一；时区只能从 IANA 列表选 | 单元测试 `ReferenceCodeRule`、`TimeZoneRule`；集成测试：重复编码、非法时区保存失败 |
| FR-005 #3 被引用的不能删除 | 集成测试：有指标引用的经济体、货币删除失败，提示中有引用方；未被引用的可以删除 |
| FR-006 #1 首批指标录入并经编辑确认 | 集成测试：导入的 50 个指标都符合编码规则和指标规则、经济体和货币是预置的；内容是否正确由编辑在后台逐条复核 |
| FR-006 #2 修改影响方向必须填理由，审计日志可查 | 单元测试 `IndicatorRule`；集成测试：方向变化且理由为空时失败；Crm 管道测试：带理由修改后，操作日志的 `reason` 为该理由 |
| FR-006 #3 主备数据源 | 采集模块实现，M2 验证 |
| FR-006 #4 重要性改为"高"后进入盯盘计划 | 采集模块实现，M2 验证 |
| BR-029 指标编码格式 | 单元测试：合法、非法、前缀与经济体不符 |
| 停用的数据不能被新选用 | 集成测试：选用停用的货币或经济体保存失败 |
| 权限码 | `AreaCatalogTests` 的已登记权限码中加入本模块的权限码；预置角色的通配规则测试 |

---

## 10. 实施拆分

评审通过后按 FR 拆成 6 个子任务，一个子任务对应一个 PR。

| 子任务 | 工单 | 内容 | 依赖 |
|---|---|---|---|
| FR-005.1 货币与经济体：领域模型与数据 | JAN-74 | 公共类型 `BilingualText`、`ContentLanguage`；货币、经济体实体；编码、时区规则及单元测试；实体配置；迁移（两张表、9 种货币和 10 个经济体的预置数据） | — |
| FR-005.2 货币与经济体：服务与后台页面 | JAN-75 | 两个服务、引用检查；经济体、货币页面；权限码登记、预置角色的通配规则；集成测试、管道测试 | FR-005.1 |
| FR-006.1 指标字典：领域模型与数据 | JAN-76 | 指标实体、枚举及中英文显示名、指标规则及单元测试；实体配置；迁移（`ref_indicator`） | FR-005.1 |
| FR-006.2 指标字典：服务与后台页面 | JAN-77 | 指标服务（方向变更理由、引用检查）；列表、详情、表单页面；集成测试、管道测试 | FR-006.1、FR-005.2 |
| FR-006.3 首批指标导入 | JAN-78 | 整理 §10.9 拆分后的清单交编辑确认（含单位清单），确认后写一次性数据迁移导入（§8 第 2、3 项） | FR-006.2；编辑确认清单 |
| FR-006.4 指标与数据源的映射 | JAN-79 | 建表、服务、页面；映射到停用的数据源时警告（ERR-IND-003） | FR-006.2；FR-001（JAN-15） |

---

## 变更记录

| 版本 | 日期 | 修改人 | 内容 |
|---|---|---|---|
| v0.1 | 2026-10-21 | vivo.li / Claude | 初稿：FR-005、FR-006 的表结构、规则、服务、页面；数据源映射只定表结构 |
| v0.2 | 2026-10-21 | vivo.li / Claude | 评审：§8 改为评审结论（6 项均按建议执行），同步 PRD v1.0.0.15（Q-008、BR-026、BR-029、BR-153、§8.1.2 发布机构） |
| v0.3 | 2026-10-22 | vivo.li / Claude | §3.7 中英文文本：存储仍按两列，代码中用 `BilingualText` 封装，以后增加语言时只改存储；§2、§5.1 同步 |
| v0.4 | 2026-10-22 | vivo.li / Claude | 评审通过；§10 按 FR 拆成 6 个子任务（原按层拆分的前三项各涉及两个 FR，Linear 子任务只能挂在一个父工单下），回填 Linear 工单号 |
| v0.5 | 2026-10-22 | vivo.li / Claude | FR-005.1 实施：§2.4 货币、经济体的预置数据改为在迁移中手写 `InsertData`（`HasData` 会丢掉中英文文本）；§3.2 部署环境需要 tzdata |
| v0.6 | 2026-10-23 | vivo.li / Claude | FR-005.2 实施：§6、§6.1 权限码前缀由 `ref:` 更正为 `reference:`（按 Area 名自动生成）；§5.2 失败提示带字段名、删除提示措辞、`GetNextSortAsync`；§6 补充删除按钮、货币下拉框的处理 |
| v0.7 | 2026-10-23 | vivo.li / Claude | FR-006.1 实施：§3.4 `IndicatorRule` 的接口和返回的字段名；§3.6 枚举编号、显示名的登记和英文名待确认；§3.7 可空的说明的存储方式 |
| v0.8 | 2026-10-23 | vivo.li / Claude | §3.4 补充：`sigma_n` 填写时必须大于 0（vivo.li 确认，JAN-77 实现） |
| v0.9 | 2026-10-23 | vivo.li / Claude | FR-006.2 实施：§6、§6.1 指标详情的 action 和权限码按前端约定改为 `Detail` / `reference:indicator:detail`；§3.5 指标暂无引用表；§6 补充列表的引用数、指标表单的脚本和修改理由；§5.2 经济体列表带指标数 |
| v0.10 | 2026-10-23 | vivo.li / Claude | FR-006.3 实施：§2.4 首批指标清单确认（[reference-indicators.md](reference-indicators.md)），导入迁移 `ImportReferenceInitialIndicators`；§3.6 单位增加"千个"，英文显示名确认；§8、§9 同步 |
