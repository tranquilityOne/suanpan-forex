# 首批指标清单

| 项目 | 内容 |
|---|---|
| 版本 | v0.3 |
| 日期 | 2026-10-29 |
| 状态 | 已确认（2026-10-23，vivo.li：清单和 §2 的问题都按建议执行）；§2 第 5 项、第 9 项随 PRD v1.0.0.23 修改（2026-10-29） |
| 工单 | JAN-78（FR-006.3 首批指标导入） |
| 依据 | data-hub PRD §10.9（首批高重要性指标清单）、§8.4；基础数据设计 §2.4、§3.1、§3.6、§8 第 2、3 项；BR-029 ~ BR-031、BR-043 |

> 已据此写成只执行一次的数据迁移 `ImportReferenceInitialIndicators`（`INSERT ... ON CONFLICT (code) DO NOTHING`）。以后的增改在后台进行，不再改本表。
> 导入后编辑在后台逐条复核影响方向（FR-006 验收第 1 条）。

---

## 1. 拆分和填写规则

- **拆分**：PRD §10.9 中同一行写了两个口径的（年率 / 月率、制造业 / 服务业、1 年期 / 5 年期），拆成两个指标；初值和终值是不同的指标，本批只导入清单中写明的初值（BR-029）。拆分后共 **50 个**。
- **编码**：`{经济体}.{简称}`，简称只用大写字母和数字（基础数据设计 §3.1）。本表统一的写法：

  | 含义 | 编码写法 | 例 |
  |---|---|---|
  | 央行政策利率 | `.RATE` | `US.RATE` |
  | 年率 / 月率 / 季率 | `.YOY` / `.MOM` / `.QOQ` | `US.CPI.YOY` |
  | 核心 | 指标名前加 `CORE` | `US.CORECPI.YOY` |
  | 初值（含 Flash、Advance、Preliminary） | 末尾加 `.PRELIM` | `EA.HICP.YOY.PRELIM` |
  | 失业率 | `.UNEMP` | `US.UNEMP` |

  编码创建后不可修改，前台指标详情页的地址由它生成（如 `us-cpi-yoy`），请重点看一遍。
- **统一取值**（不在表中逐行列出）：
  - 重要性：全部为"高"（本批就是高重要性清单）。
  - 货币：取经济体的货币；德国为 EUR。
  - 持平容差：0（BR-074 的默认值），需要时编辑在后台调整。
  - 合理上下限、标准差倍数：不填，质量校验（FR-026）上线前再补。
  - 中英文说明：不导入，编辑导入后在后台补充（不得复制其他网站的文字，PRD §13.2.1）。
- **默认发布时间**：机构当地时间，按经济体的时区解释（美国 America/New_York、欧元区 Europe/Brussels、德国 Europe/Berlin、英国 Europe/London、日本 Asia/Tokyo、中国 Asia/Shanghai），夏令时由系统换算。实际排期以各机构的发布日程为准，这里只是默认值。
- **数值按单位和小数位存储**（BR-043）：如非农 256（千人），CPI 2.9（%）。

---

## 2. 确认的问题

| # | 问题 | 结论 |
|---|---|---|
| 1 | **单位清单**是否够用（§3）。JOLTS 职位空缺是"岗位数"，不是人数 | 增加单位"千个"（`IndicatorUnit.ThousandUnits`），JOLTS 用它 |
| 2 | 初请失业金人数用"千人"（存 220）还是"人"（存 220000） | "千人"，与非农一致 |
| 3 | 英国"平均每周工资（3 个月年率）"取含奖金还是不含奖金 | 含奖金 |
| 4 | 英国 GDP 季率 PRD 没写初值：取首次公布的季率（First Estimate）还是终值 | 取首次公布，编码 `GB.GDP.QOQ` |
| 5 | 没有固定公布时间的指标：日本央行利率决议、中国贸易帐 | 默认时间填 12:00、11:00，每次按实际排期改日历事件。**v0.3 起**：排期只来自数据源（PRD DEC-024），不再人工改时间；官方日程只给日期时事件为"仅日期"，不盯盘，默认时间只作参考 |
| 6 | 中国 LPR 的公布时间是 9:00 还是 9:30 | 9:00：中国人民银行公告，自 2024-07-22 起由 9:15 改为 9:00 |
| 7 | 枚举的英文显示名（§4）是否合适 | 按 §4 |
| 8 | 编码写法（§1）是否认可 | 按 §1 |
| 9 | 非政府机构的 7 个指标（`US.ISM.MFG`、`US.ISM.SVC`、`US.UMCSI.PRELIM`、`EA.PMI.MFG.PRELIM`、`EA.PMI.SVC.PRELIM`、`DE.IFO`、`DE.ZEW`）怎么维护 | 暂不纳入日历（PRD Q-004、DEC-024）：在指标字典中停用，由日历的 FR-007.1（JAN-87）用数据迁移 `DisableNonOfficialIndicators` 执行（已经停用的不动）；法务确认授权后（v1.1）再启用并接入数据源。v0.3 新增 |

---

## 3. 单位清单

| 单位 | 中文显示 | 英文显示 | 本批使用的指标 |
|---|---|---|---|
| 百分比 | 百分比（%） | Percent (%) | 利率、通胀、增长率、失业率、零售销售等 |
| 指数 | 指数 | Index | PMI、信心指数、景气指数、短观 |
| 人 | 人 | Persons | 本批未用 |
| 千人 | 千人 | Thousand Persons | 非农、初请失业金 |
| 亿美元 | 亿美元 | USD 100 Million | 中国贸易帐 |
| 千个 | 千个 | Thousand Units | JOLTS 职位空缺 |

对外显示的格式（如"千人"在中文页面显示为"万人"）由快讯和前台按 NFR-072 处理，不影响存储。

---

## 4. 枚举的中英文显示名

| 枚举 | 取值（中文 / 英文） |
|---|---|
| 类别 | 就业 Employment、通胀 Inflation、增长 Growth、消费 Consumption、贸易 Trade、利率 Interest Rate、景气 Sentiment、其他 Other |
| 数值类型 | 数值 Value、区间 Range |
| 发布频率 | 周 Weekly、月 Monthly、季 Quarterly、不定期 Irregular |
| 重要性 | 高 High、中 Medium、低 Low |
| 影响方向 | 越高越利好本币 Higher is bullish for the currency、越高越利空本币 Higher is bearish for the currency、无方向 No direction |

---

## 5. 指标清单

列说明：数值类型除标注"区间"的以外都是"数值"；影响方向"利好"指越高越利好本币，"利空"指越高越利空本币；发布时间为机构当地时间。

### 5.1 美国（18 个，货币 USD，时区 America/New_York）

| # | 编码 | 名称（中 / 英） | 简称（中 / 英） | 类别 | 单位 | 小数位 | 频率 | 方向 | 发布机构 | 发布时间 |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `US.RATE` | 美联储利率决议（联邦基金利率目标区间）/ Fed Interest Rate Decision (Federal Funds Target Range) | 美联储利率决议 / Fed Rate Decision | 利率 | % | 2（**区间**） | 不定期 | 利好 | 美联储 / Federal Reserve | 14:00 |
| 2 | `US.NFP` | 非农就业人数变化 / Nonfarm Payrolls | 非农就业人数 / Nonfarm Payrolls | 就业 | 千人 | 0 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 3 | `US.UNEMP` | 失业率 / Unemployment Rate | 失业率 / Unemployment Rate | 就业 | % | 1 | 月 | 利空 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 4 | `US.AHE.MOM` | 平均时薪月率 / Average Hourly Earnings MoM | 平均时薪月率 / Avg Hourly Earnings MoM | 就业 | % | 1 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 5 | `US.CPI.YOY` | CPI 年率 / CPI YoY | CPI 年率 / CPI YoY | 通胀 | % | 1 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 6 | `US.CPI.MOM` | CPI 月率 / CPI MoM | CPI 月率 / CPI MoM | 通胀 | % | 1 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 7 | `US.CORECPI.YOY` | 核心 CPI 年率 / Core CPI YoY | 核心 CPI 年率 / Core CPI YoY | 通胀 | % | 1 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 8 | `US.CORECPI.MOM` | 核心 CPI 月率 / Core CPI MoM | 核心 CPI 月率 / Core CPI MoM | 通胀 | % | 1 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 9 | `US.PPI.MOM` | PPI 月率 / PPI MoM | PPI 月率 / PPI MoM | 通胀 | % | 1 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 08:30 |
| 10 | `US.COREPCE.YOY` | 核心 PCE 物价指数年率 / Core PCE Price Index YoY | 核心 PCE 年率 / Core PCE YoY | 通胀 | % | 1 | 月 | 利好 | 美国经济分析局 / Bureau of Economic Analysis | 08:30 |
| 11 | `US.COREPCE.MOM` | 核心 PCE 物价指数月率 / Core PCE Price Index MoM | 核心 PCE 月率 / Core PCE MoM | 通胀 | % | 1 | 月 | 利好 | 美国经济分析局 / Bureau of Economic Analysis | 08:30 |
| 12 | `US.GDP.QOQ.PRELIM` | GDP 年化季率（初值）/ GDP Annualized QoQ (Advance) | GDP 季率初值 / GDP QoQ Advance | 增长 | % | 1 | 季 | 利好 | 美国经济分析局 / Bureau of Economic Analysis | 08:30 |
| 13 | `US.RETAIL.MOM` | 零售销售月率 / Retail Sales MoM | 零售销售月率 / Retail Sales MoM | 消费 | % | 1 | 月 | 利好 | 美国人口普查局 / U.S. Census Bureau | 08:30 |
| 14 | `US.CLAIMS` | 初请失业金人数 / Initial Jobless Claims | 初请失业金人数 / Initial Jobless Claims | 就业 | 千人 | 0 | 周 | 利空 | 美国劳工部 / U.S. Department of Labor | 08:30 |
| 15 | `US.JOLTS` | JOLTS 职位空缺 / JOLTS Job Openings | JOLTS 职位空缺 / JOLTS Job Openings | 就业 | 千个 | 0 | 月 | 利好 | 美国劳工统计局 / Bureau of Labor Statistics | 10:00 |
| 16 | `US.ISM.MFG` | ISM 制造业 PMI / ISM Manufacturing PMI | ISM 制造业 PMI / ISM Manufacturing PMI | 景气 | 指数 | 1 | 月 | 利好 | 美国供应管理协会 / Institute for Supply Management | 10:00 |
| 17 | `US.ISM.SVC` | ISM 非制造业 PMI / ISM Services PMI | ISM 非制造业 PMI / ISM Services PMI | 景气 | 指数 | 1 | 月 | 利好 | 美国供应管理协会 / Institute for Supply Management | 10:00 |
| 18 | `US.UMCSI.PRELIM` | 密歇根大学消费者信心指数（初值）/ University of Michigan Consumer Sentiment (Preliminary) | 密歇根消费者信心初值 / UMich Sentiment Prelim | 景气 | 指数 | 1 | 月 | 利好 | 密歇根大学 / University of Michigan | 10:00 |

### 5.2 欧元区（7 个，货币 EUR，时区 Europe/Brussels）

| # | 编码 | 名称（中 / 英） | 简称（中 / 英） | 类别 | 单位 | 小数位 | 频率 | 方向 | 发布机构 | 发布时间 |
|---|---|---|---|---|---|---|---|---|---|---|
| 19 | `EA.RATE` | 欧洲央行利率决议（存款便利利率）/ ECB Interest Rate Decision (Deposit Facility Rate) | 欧洲央行利率决议 / ECB Rate Decision | 利率 | % | 2 | 不定期 | 利好 | 欧洲央行 / European Central Bank | 14:15 |
| 20 | `EA.HICP.YOY.PRELIM` | HICP 年率（初值）/ HICP YoY (Flash) | HICP 年率初值 / HICP YoY Flash | 通胀 | % | 1 | 月 | 利好 | 欧盟统计局 / Eurostat | 11:00 |
| 21 | `EA.COREHICP.YOY.PRELIM` | 核心 HICP 年率（初值）/ Core HICP YoY (Flash) | 核心 HICP 年率初值 / Core HICP YoY Flash | 通胀 | % | 1 | 月 | 利好 | 欧盟统计局 / Eurostat | 11:00 |
| 22 | `EA.GDP.QOQ.PRELIM` | GDP 季率（初值）/ GDP QoQ (Preliminary Flash) | GDP 季率初值 / GDP QoQ Flash | 增长 | % | 1 | 季 | 利好 | 欧盟统计局 / Eurostat | 11:00 |
| 23 | `EA.UNEMP` | 失业率 / Unemployment Rate | 失业率 / Unemployment Rate | 就业 | % | 1 | 月 | 利空 | 欧盟统计局 / Eurostat | 11:00 |
| 24 | `EA.PMI.MFG.PRELIM` | HCOB 制造业 PMI（初值）/ HCOB Manufacturing PMI (Flash) | 制造业 PMI 初值 / Manufacturing PMI Flash | 景气 | 指数 | 1 | 月 | 利好 | 标普全球 / S&P Global | 10:00 |
| 25 | `EA.PMI.SVC.PRELIM` | HCOB 服务业 PMI（初值）/ HCOB Services PMI (Flash) | 服务业 PMI 初值 / Services PMI Flash | 景气 | 指数 | 1 | 月 | 利好 | 标普全球 / S&P Global | 10:00 |

### 5.3 德国（3 个，货币 EUR，时区 Europe/Berlin）

| # | 编码 | 名称（中 / 英） | 简称（中 / 英） | 类别 | 单位 | 小数位 | 频率 | 方向 | 发布机构 | 发布时间 |
|---|---|---|---|---|---|---|---|---|---|---|
| 26 | `DE.CPI.MOM.PRELIM` | CPI 月率（初值）/ CPI MoM (Preliminary) | CPI 月率初值 / CPI MoM Prelim | 通胀 | % | 1 | 月 | 利好 | 德国联邦统计局 / Destatis | 14:00 |
| 27 | `DE.IFO` | ifo 商业景气指数 / ifo Business Climate Index | ifo 商业景气 / ifo Business Climate | 景气 | 指数 | 1 | 月 | 利好 | ifo 经济研究所 / ifo Institute | 10:00 |
| 28 | `DE.ZEW` | ZEW 经济景气指数 / ZEW Economic Sentiment | ZEW 经济景气 / ZEW Economic Sentiment | 景气 | 指数 | 1 | 月 | 利好 | 欧洲经济研究中心 / ZEW | 11:00 |

### 5.4 英国（8 个，货币 GBP，时区 Europe/London）

| # | 编码 | 名称（中 / 英） | 简称（中 / 英） | 类别 | 单位 | 小数位 | 频率 | 方向 | 发布机构 | 发布时间 |
|---|---|---|---|---|---|---|---|---|---|---|
| 29 | `GB.RATE` | 英国央行利率决议 / BoE Interest Rate Decision | 英国央行利率决议 / BoE Rate Decision | 利率 | % | 2 | 不定期 | 利好 | 英国央行 / Bank of England | 12:00 |
| 30 | `GB.CPI.YOY` | CPI 年率 / CPI YoY | CPI 年率 / CPI YoY | 通胀 | % | 1 | 月 | 利好 | 英国国家统计局 / Office for National Statistics | 07:00 |
| 31 | `GB.CORECPI.YOY` | 核心 CPI 年率 / Core CPI YoY | 核心 CPI 年率 / Core CPI YoY | 通胀 | % | 1 | 月 | 利好 | 英国国家统计局 / Office for National Statistics | 07:00 |
| 32 | `GB.GDP.MOM` | GDP 月率 / GDP MoM | GDP 月率 / GDP MoM | 增长 | % | 1 | 月 | 利好 | 英国国家统计局 / Office for National Statistics | 07:00 |
| 33 | `GB.GDP.QOQ` | GDP 季率（首次公布）/ GDP QoQ (First Estimate) | GDP 季率 / GDP QoQ | 增长 | % | 1 | 季 | 利好 | 英国国家统计局 / Office for National Statistics | 07:00 |
| 34 | `GB.UNEMP` | 失业率 / ILO Unemployment Rate | 失业率 / Unemployment Rate | 就业 | % | 1 | 月 | 利空 | 英国国家统计局 / Office for National Statistics | 07:00 |
| 35 | `GB.AWE.3MYOY` | 平均每周工资（含奖金，3 个月年率）/ Average Earnings Including Bonus (3M YoY) | 平均每周工资 / Avg Earnings 3M YoY | 就业 | % | 1 | 月 | 利好 | 英国国家统计局 / Office for National Statistics | 07:00 |
| 36 | `GB.RETAIL.MOM` | 零售销售月率 / Retail Sales MoM | 零售销售月率 / Retail Sales MoM | 消费 | % | 1 | 月 | 利好 | 英国国家统计局 / Office for National Statistics | 07:00 |

### 5.5 日本（5 个，货币 JPY，时区 Asia/Tokyo）

| # | 编码 | 名称（中 / 英） | 简称（中 / 英） | 类别 | 单位 | 小数位 | 频率 | 方向 | 发布机构 | 发布时间 |
|---|---|---|---|---|---|---|---|---|---|---|
| 37 | `JP.RATE` | 日本央行利率决议 / BoJ Interest Rate Decision | 日本央行利率决议 / BoJ Rate Decision | 利率 | % | 2 | 不定期 | 利好 | 日本央行 / Bank of Japan | 12:00 |
| 38 | `JP.CORECPI.YOY` | 全国核心 CPI 年率 / National Core CPI YoY | 全国核心 CPI 年率 / National Core CPI YoY | 通胀 | % | 1 | 月 | 利好 | 日本总务省统计局 / Statistics Bureau of Japan | 08:30 |
| 39 | `JP.TOKYO.CORECPI.YOY` | 东京核心 CPI 年率 / Tokyo Core CPI YoY | 东京核心 CPI 年率 / Tokyo Core CPI YoY | 通胀 | % | 1 | 月 | 利好 | 日本总务省统计局 / Statistics Bureau of Japan | 08:30 |
| 40 | `JP.GDP.QOQ.PRELIM` | GDP 季率（初值）/ GDP QoQ (Preliminary) | GDP 季率初值 / GDP QoQ Prelim | 增长 | % | 1 | 季 | 利好 | 日本内阁府 / Cabinet Office | 08:50 |
| 41 | `JP.TANKAN.MFG` | 短观大型制造业景气判断指数 / Tankan Large Manufacturers Index | 短观大型制造业指数 / Tankan Large Mfg Index | 景气 | 指数 | 0 | 季 | 利好 | 日本央行 / Bank of Japan | 08:50 |

### 5.6 中国（9 个，货币 CNY，时区 Asia/Shanghai）

| # | 编码 | 名称（中 / 英） | 简称（中 / 英） | 类别 | 单位 | 小数位 | 频率 | 方向 | 发布机构 | 发布时间 |
|---|---|---|---|---|---|---|---|---|---|---|
| 42 | `CN.GDP.YOY` | GDP 年率 / GDP YoY | GDP 年率 / GDP YoY | 增长 | % | 1 | 季 | 利好 | 国家统计局 / National Bureau of Statistics | 10:00 |
| 43 | `CN.CPI.YOY` | CPI 年率 / CPI YoY | CPI 年率 / CPI YoY | 通胀 | % | 1 | 月 | 利好 | 国家统计局 / National Bureau of Statistics | 09:30 |
| 44 | `CN.PPI.YOY` | PPI 年率 / PPI YoY | PPI 年率 / PPI YoY | 通胀 | % | 1 | 月 | 利好 | 国家统计局 / National Bureau of Statistics | 09:30 |
| 45 | `CN.IP.YOY` | 规模以上工业增加值年率 / Industrial Production YoY | 工业增加值年率 / Industrial Production YoY | 增长 | % | 1 | 月 | 利好 | 国家统计局 / National Bureau of Statistics | 10:00 |
| 46 | `CN.RETAIL.YOY` | 社会消费品零售总额年率 / Retail Sales YoY | 社零年率 / Retail Sales YoY | 消费 | % | 1 | 月 | 利好 | 国家统计局 / National Bureau of Statistics | 10:00 |
| 47 | `CN.PMI.MFG` | 官方制造业 PMI / NBS Manufacturing PMI | 官方制造业 PMI / NBS Manufacturing PMI | 景气 | 指数 | 1 | 月 | 利好 | 国家统计局 / National Bureau of Statistics | 09:30 |
| 48 | `CN.TRADE` | 贸易帐（美元计）/ Trade Balance (USD) | 贸易帐 / Trade Balance | 贸易 | 亿美元 | 1 | 月 | 利好 | 海关总署 / General Administration of Customs | 11:00 |
| 49 | `CN.LPR.1Y` | 1 年期贷款市场报价利率 / 1-Year Loan Prime Rate | 1 年期 LPR / 1Y LPR | 利率 | % | 2 | 月 | 利好 | 全国银行间同业拆借中心 / National Interbank Funding Center | 09:00 |
| 50 | `CN.LPR.5Y` | 5 年期以上贷款市场报价利率 / 5-Year Loan Prime Rate | 5 年期以上 LPR / 5Y LPR | 利率 | % | 2 | 月 | 利好 | 全国银行间同业拆借中心 / National Interbank Funding Center | 09:00 |

---

## 变更记录

| 版本 | 日期 | 修改人 | 内容 |
|---|---|---|---|
| v0.1 | 2026-10-23 | vivo.li / Claude | 初稿：按 PRD §10.9 拆成 50 个指标，列出待确认问题 |
| v0.2 | 2026-10-23 | vivo.li / Claude | 确认：§2 的问题都按建议执行；单位增加"千个"给 JOLTS；英国平均每周工资取含奖金；英国 GDP 季率取首次公布；LPR 公布时间核实为 9:00 |
| v0.3 | 2026-10-29 | vivo.li / Claude | 同步 PRD v1.0.0.23（DEC-024：排期只来自数据源）：§2 第 5 项不再人工改时间；新增第 9 项，7 个非政府机构的指标暂不纳入日历、停用 |
