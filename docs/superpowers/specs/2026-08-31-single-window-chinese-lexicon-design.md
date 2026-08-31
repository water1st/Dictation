# 单窗口导航与中文释义架构修正规格

**状态：** 已完成对话设计确认，等待书面规格复核

**适用范围：** 当前统一 Import Inbox 切片及后续全部 WPF 业务页面、日语与英语运行时词库

## 1. 修正背景

当前未提交的 Task 2 把查词和导入收件箱实现为独立模态窗口，同时沿用了 JMdict 英文 gloss 和 Open English WordNet 英文 definition 作为用户可见释义。这两点均偏离产品目标：产品必须始终在一个业务窗口内操作，且日语和英语的题面、查词结果与导入快照必须使用中文释义。

本规格覆盖旧计划中关于 `LexiconLookupWindow`、`ImportInboxWindow`、窗口工厂、`ShowDialog()`、英文用户可见释义及其测试夹具的约定。旧计划保留为历史记录，但后续实现不得继续遵循这些已被覆盖的内容。

## 2. 明确目标与非目标

目标：

- WPF 进程只创建一个承载业务内容的 `HomeWindow`。
- 首页、查词、导入收件箱、词条详情以及后续学习、复习、五十音、统计和设置均为互不引用的 `UserControl + ViewModel` 页面。
- 前进、返回、页面状态保留和转场动画由 Presentation 的统一壳层管理。
- 日语最终显示 JMdict 义项对应的简体中文；英语优先显示与 WordNet 义项对齐的简体中文，并以宽泛英中释义补足覆盖。
- 运行时仍只查询两个不可变 SQLite 文件，不依赖网络或原始词库。

非目标：

- 不采用 WPF `Frame/Page`、URI 导航或浏览器式导航日志。
- 不把所有页面合并进一个巨型 ViewModel，也不让页面直接引用其它页面或主窗口。
- 不提供中文反向查词，不在运行时翻译或下载词库。
- 不为单一导航实现增加无扩展价值的接口、消息总线或第三方 UI 框架。

## 3. 单窗口导航壳层

### 3.1 组件边界

`HomeWindow` 是唯一业务 `Window`，只负责窗口生命周期、顶部返回区域、页面承载区和弹层承载区。页面承载区使用 `ContentControl`；ViewModel 与 `UserControl` 通过 WPF `DataTemplate` 映射。

一个具体的 `NavigationService` 集中维护：

- 当前页面及其标题；
- 是否允许返回；
- 前进或返回方向；
- 按访问顺序保存的页面状态栈；
- 页面被移出栈时需要释放的 DI 作用域。

每个页面只依赖自己的业务 Service、DTO 和必要的导航操作。页面不得访问 `HomeWindow`、`ContentControl`、其它 View/UserControl 或动画对象。语义导航操作由 `NavigationService` 集中解释，页面不自行构造目标页面。

### 3.2 导航和状态

从首页进入子页面时固定当前语言并压入导航栈；只有回到首页后才能切换日语或英语。返回上一页必须恢复该页原有的查询文本、选择项、列表滚动位置、筛选和勾选状态。页面被显式关闭、被新的根导航替换或应用退出时释放其作用域；不保留前进历史。

收件箱采用列表页与详情页两个独立页面。列表进入详情使用同一导航栈，返回后不重新查询或丢失批量选择。主页静态入口、页面内部命令和返回按钮最终都调用同一个导航服务。

### 3.3 动画与弹层

壳层统一提供约 200 毫秒的滑动加淡入淡出：前进时旧页向左退出、新页从右进入；返回时方向相反。动画只属于 Presentation，不影响 ViewModel 状态或业务命令。

普通确认、错误、加载和选择提示使用 `HomeWindow` 内的遮罩弹层，不能使用业务 `MessageBox` 或第二个 WPF `Window`。Windows 原生打开/保存文件选择器是平台输入适配器，允许作为唯一例外；隐藏的 TTS Host 和 Updater 不是业务 UI 窗口。

## 4. 中文释义构建

### 4.1 日语

JMdict 继续作为词头、读音、词性、义项、常用度和活用关系的规范来源。构建器额外读取 Tomoshi `zh_defs`，以 JMdict `entry_id` 和义项序号精确关联 `zh-CN` gloss；不得按文本相似度猜测关系，也不得把英文 gloss 当作中文缺失时的显示回退。

运行库为每个日语学习义项保存有序中文释义及来源。Tomoshi 没有对应中文时，该义项仍可被日文正向检索，但 Service 返回空中文集合和明确的“暂无中文释义”状态。

### 4.2 英语

Open English WordNet 继续负责词元、词性、Sense、Synset、ILI、直接派生关系和现有确定性词形。构建器必须保留每个 Synset 的 ILI。

Chinese Open WordNet 2.0 通过 ILI 与 Open English WordNet 对齐。存在对齐时，每个 WordNet Sense 形成一个精确学习义项，中文同义词按确定顺序去重后成为该义项的中文释义。

ECDICT 只用于中文覆盖补全，不取代既有英语词形规则。对于同一英语词元和词性中未得到任何精确中文映射的剩余 Sense，构建器至多生成一个宽泛学习义项：按词性选取并清洗 ECDICT `translation`，使用稳定的合成标识 `ecdict:{normalized-lemma}:{part-of-speech}`。不得把同一条宽泛翻译伪装成多个精确 WordNet Sense。

若 Chinese Open WordNet 和 ECDICT 均无中文，该词元仍可英文正向检索，但只返回“暂无中文释义”状态。英文 definition 可以保留为构建审计数据，但不能进入面向 UI 的 `Meanings`。

### 4.3 运行时模型与用户修改

构建结果仍为 `Resources/Lexicons/JapaneseLexicon.db` 和 `Resources/Lexicons/EnglishLexicon.db`。每个可学习结果都具有稳定来源标识、词元/组标识、词性、中文释义、释义来源和精确或宽泛标记；DataAccess 返回 PO，Service 映射为 DTO，Presentation 不解释来源表。

查词 UI 只显示中文释义。缺失中文时显示状态提示并提供中文编辑框；在释义仍为空时禁止送入学习库或复习库。用户修改后的中文只写入 Import Inbox 快照，确认后复制到用户学习数据，绝不回写不可变词库。MCP 也必须经过相同校验和 Inbox 流程。

## 5. 数据流与错误行为

构建时数据流：

1. 解析 JMdict 或 Open English WordNet 的规范结构。
2. 通过 JMdict `entry_id + sense ordinal` 或 WordNet ILI 加入精确中文。
3. 英语没有精确中文的词元/词性使用 ECDICT 生成至多一个宽泛义项。
4. 规范化、去重、稳定排序并写入只读 SQLite。
5. 执行覆盖率、完整性、许可证、输入哈希和代表性词条门禁。

运行时数据流：

1. 用户只用日文或英文正向检索。
2. DataAccessor 只读查询相应运行库。
3. Service 返回中文意义、词性和整组词形。
4. 用户选中义项并可修改中文快照。
5. Import Inbox 统一决定学习、复习或丢弃。

词库缺失、schema 不符或损坏时显示可读中文错误但不阻止主窗口启动。单项中文缺失不是数据库错误，不能回退英文，也不能把占位文本保存成真实释义。

## 6. 固定资源与合规

构建清单必须固定并校验以下输入；原始归档和上游数据库不进入发布 ZIP，只发布加工后的运行库和适用许可证/归属：

| 资源 | 固定版本 | 已核验大小 | SHA-256 | 许可处理 |
| --- | --- | ---: | --- | --- |
| Tomoshi 日中数据 | `v2026-08-12` | 压缩 77,463,170 bytes；原库 570,077,184 bytes | 压缩 `CD4E6B710FA4FCA0672471B64BF251256DDE7EBA807B9BEE2AB88B3FC71554C1`；原库 `C935FFD8E030F496C251B38819BF63005729D2DBC98BA0AF8C55DE507A455290` | `zh_defs` 为 CC BY-SA 4.0，发布保留归属与许可 |
| Chinese Open WordNet | `2.0` | 1,705,740 bytes | `7D07AF60A6CED0CEDC4CA114D0B60A796D3F138DF0F3BE21F6322E53D004E91C` | 保留归档内 Chinese Open WordNet 许可、版权和免责声明 |
| ECDICT SQLite | `1.0.28` | 216,765,132 bytes | `EA01F76A3B3351021CE47077E89234465CC9441C8793054495320D06C0C3F3F6` | 按上游 MIT 文件与数据来源说明保留归属；仅提取所需字段 |

本地核验显示 Chinese Open WordNet 包含 42,300 个唯一 ILI；当前 Open English WordNet 输入包含 104,314 个唯一 ILI，交集为 38,867，精确概念覆盖约 37.26%。因此 ECDICT 是必要的覆盖补全层，而不是 WordNet 义项的假精确翻译。

## 7. 测试与验收

实施仍遵循项目严格 TDD：测试项目必须先编译成功，再通过临时生产代码 mutation 证明目标测试真实失败；冻结测试后只修正生产实现直至全绿。

导航验收至少覆盖：

- 应用业务流程始终只有一个 `HomeWindow`；
- 首页、查词、收件箱列表和详情均为 `UserControl`；
- 前进/返回方向正确，返回后页面状态保留；
- 页面不直接引用其它页面或主窗口；
- 普通业务弹层不创建第二个 Window；
- 日语/英语只能在首页切换。

词库验收至少覆盖：

- `食べる`、`行く`、`掃除` 的中文义项与 JMdict Sense 对齐；
- `go/went/gone/going`、`do/does/did/done/doing`、`child/children`、`good/better/best` 仍为完整整组；
- 有 ILI 匹配时使用精确中文，缺少匹配时只生成一个 ECDICT 宽泛义项；
- 没有中文时不显示英文、不允许以占位文本进入学习或复习；
- 用户修改中文后仅改变 Inbox/用户数据；
- 两个运行库通过 `PRAGMA integrity_check`、schema/版本、确定性构建和真实资源覆盖率门禁。

最终还需执行 Presentation 聚焦测试、LexiconBuilder/DataAccess/Service 聚焦测试、全量测试、解决方案构建、真实 WPF 单窗口冒烟和源码范围 `.db` 扫描。当前未提交的多窗口 Task 2 只有在按本规格修正并通过上述验收后才能提交。
