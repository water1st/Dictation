# Dictation 重构权威规格

## 1. 范围与目标

目标是 `.NET 10` WPF 的离线日语、英语学习与智能复习应用。当前已建立 WPF 单窗口、Core 业务层和 DataAccess 持久化层；旧 WinForms、Edge/System TTS 与独立 Services 项目已在替代链路通过测试和启动冒烟后移除。剩余功能继续按可运行、可验证的垂直切片实现。

最终结构为传统三层：`Presentation`（WPF）、`Core`（用例编排）、`DataAccess`（Dapper/SQLite CRUD）；另有隐藏的 `Dictation.TtsHost`、待实现的 `Dictation.Updater` 和构建时使用的 `Dictation.LexiconBuilder`。WPF 与 MCP 必须调用 Core 内同一组业务服务，不得绕过业务层。

## 2. 架构与运行时边界

- WPF、Core、DataAccess 和可选 MCP Hosted Service 属于同一个 WPF Generic Host 与 DI 容器。入口为 `[STAThread] Program.Main`，`App` 不使用 `StartupUri`。`Dictation.TtsHost` 是由该宿主生命周期钩子启动和停止的单独隐藏进程，通过命名管道通信；它不建立第二套业务层或数据库访问链路。
- Presentation 只处理 WPF、ViewModel、绑定和输入适配；Core 只编排业务；DataAccess 只执行 CRUD/SQL。DTO、业务模型、持久化对象（PO）分离，跨层使用简单 Mapper。
- 设置配置集中在 `Dictation.Core.Configuration`：`ApplicationSettingsDocument` 只保存持久化字符串，Microsoft `IConfiguration.GetValue<T>` 负责读取可选的 `CONFIG/settings.json`，内部 `IConfiguration.SetValueAsync<T>` 负责一次完整文档的原子写回并调用 `IConfigurationRoot.Reload()`。微软提供器没有官方 JSON 写回 API，因此只补这个最小缺口；SettingsService 调用对应 Mapper 转换文档与 DTO，拒绝未知业务值，默认值由 SettingsService 产生。DataAccess 不承担读写设置。
- 项目依赖为 Presentation → Core → DataAccess、TtsHost → Core；DataAccess 不得反向引用 Core。`AddDictationCore()` 是唯一业务注册入口，不保留独立 Services 项目或转发兼容层。
- 接口依据实际能力边界、跨模块复用、实现替换和依赖隔离需求设置，不能只按实现数量判断；无实际抽象价值的 Service、DataAccessor 或算法组件仍以具体类注入。接口按调用方需要的能力拆分，同一实现可实现多个接口，DI 别名保留同一实例与生命周期；能力复用优先组合而非继承。禁止 DDD、CQRS、EF、`Repository`/`Repository<T>` 和无实际价值的抽象。
- 简单不等于合并不相关职责。纯映射放对应 Mapper，查询、业务规则与事务仍归所属层；一文件一个主要类型，允许真正私有的附属嵌套类型及框架分部类型。实现与辅助类型采用最小必要可见性，补齐中文类型职责、方法、参数及必要返回值 XML 注释，复杂流程解释原因和约束。具体准则以仓库 `AGENTS.md` 为准。
- 每次数据库操作独立创建连接。SQLite 迁移器维护 `SchemaMigrations`；升级先备份，迁移在事务中执行，失败回滚并阻止应用启动。
- 单例使用命名互斥体和命名管道：第二进程静默退出，不提示、不转交参数；第一进程若最小化则还原并置前。

## 3. 文件、数据与发布归属

应用根目录启动时并列创建 `CONFIG/`、`DATA/UserData.db`、`LOGS/`、`BACKUPS/`，确有需要才创建 `CACHE/`。它们不是 `CONFIG/` 的子目录，也不进入更新 ZIP，更新时不得覆盖。不可变词库和模型位于发布根的 `Resources/`。用户记录需保存导入时必要的词库元数据快照，不对只读词库建立外键。

首次启动默认语言为日语，主题为 System；此后始终记住用户最后选择的语言，主题可继续改为浅色或深色。

发布采用自包含 `win-x64` 全量 ZIP 和 SHA-256，不提供安装程序、签名、增量更新或运行时在线词库/数据更新。启动异步检查 GitHub Release；有新版本时在工具栏显示非模态更新按钮，用户点击后由隐藏 updater 关闭 WPF、保留运行数据目录、替换程序和 Resources、失败回滚、重启并迁移。

`.github/workflows/dotnet-desktop.yml` 在 `master` push 时先执行 restore/build/test，再自动创建 GitHub Release；PR 只验证不发 Release。`VERSION` 只保存 `major.minor`，发布版本为 `major.minor.github.run_number`，Release 同时提供自包含 ZIP 和 SHA-256 文件。

## 4. 词条、词组与导入

词条模型为 `word group → POS + sense → forms`。输入任意词形即导入该整组；重复导入无效。不同义项是不同学习词条，同一轮完全相同的听写题去重；短语是独立固定组，不与任何变形组合。直接相关的 noun/verb/adj/adv 可以同组，但不递归扩张；词集可以重叠。

整组删除会从用户学习库、复习库移除，并删除该组全部学习进度、FSRS 状态、错题和历史，不存在“归档”。仅从某个自定义词集移除时只删除 membership，不删除词条或其它集合中的 membership。

手动输入、TXT、拖放、启动参数和 MCP 均先进入统一 Import Inbox。用户在 Inbox 界面逐批确认送入学习库、复习库或丢弃；MCP 不能替用户确认。只按源语言正向查词，禁止中文反向查词。日语以 JMdict 生成结构和词形，并按 `entry_id + sense ordinal` 从 Tomoshi 加入精确中文；英语以 Open English WordNet 生成结构和词形，通过 Chinese Open WordNet ILI 加入精确中文，再由 ECDICT 为未覆盖的 lemma+POS 提供至多一个宽泛中文。JMdict 英文 gloss 与 OEWN definition/example 只供审计，绝不进入 UI `Meanings` 或作为缺失中文的回退；纯 ASCII 英文文本不得写入学习释义。

运行时词库固定为 schema v2，由 `scripts/Build-Lexicons.ps1` 使用显式输入、解压和输出目录离线生成。构建必须校验固定哈希、COW/OEWN ILI 覆盖、代表词形、中文释义、`PRAGMA integrity_check` 与两次确定性输出；原始 JMdict、Tomoshi、OEWN、COW、ECDICT 输入不进入发布 ZIP。

## 5. 日语内容与出题

日语只接受日文检索。JMdict/规则关联普通形、礼貌形、`て` 形、`た` 形等为同组；元数据包含词性、一类/二类动词、自动词/他动词，以及常用汉字、罕见汉字、无汉字分类。罕见汉字按无汉字处理，不安排汉字练习；不适用汉字的题目自动排除。

日语看义默写或听写的题面同时给出意思、要求的词形和汉字/假名要求，例如“吃／礼貌形／假名”；食べる的礼貌形听写实际播放 `たべます`。启用普通形和礼貌形时，汉字/假名与题型动态配对；只启用一种时，汉字和假名各覆盖一次。所有启用渠道都要覆盖，弱项优先、同级随机，不生成完整笛卡尔积。

五十音是日语词汇之外的独立入口，只做听写、独立 FSRS 和每日目标（全量或 X 个）。平假名/片假名至少启用一项，清音/浊音与半浊音/拗音范围至少启用一项。选择全量时，所有启用项目各出一次并重试错题；完成计入日语打卡。

## 6. 英语内容与出题

英语只接受英文检索。`go/goes/went/gone/going`、`child/children`、`good/better/best` 等完整词形均属于一个组；直接派生的 noun/verb/adj/adv 可以作为同组中的独立词性题目。默认显示并发音英式形式；词库确认的美式拼写也接受。语法标签默认显示“原形、第三人称单数、过去式、过去分词、现在分词”等名称，不泄露目标英文形式；设置允许用 `do`、`go` 等用户选择的示例词作为标签转义字符。

英语默写默认是无拼写检查、无自动补全、无自动纠错的键盘 TextBox。日语默认手写；两种语言都可在设置中指定默认输入方式，并在每道题临时切换。

学习和复习两种模式都提供本轮浏览。日语浏览显示单词、意思、词性、一类/二类动词、自动词/他动词等属性；英语浏览显示单词、POS、义项和本轮启用词形。两种语言都可在浏览中用喇叭播放相应目标形式，不能因为浏览而改变答题或进度。

## 7. 学习、复习与错题

日语和英语的计划、词库、进度、错误、统计完全分开。首页才能切换语言，记住上次语言；学习/复习中不能切换。每日新学目标与每轮抽取数独立设置，首页按钮显示如“复习 0/20”。学习支持随机、顺序、手选；复习按风险优先，题序随机；一组不论词形多少只计一个词。未开始的新词额度不结转，已开始词组和逾期复习保留。

每个启用形式需在间隔 3–7 题后连续答对 2 次才掌握；错误只重置该形式，后来启用的新形式进入补充队列。复习使用 FSRS，目标保持率 90%；正确为 `Good`，错误为 `Again`。一次会话首次错误即使稍后重答正确仍保持 `Again`。判错立即显示错误与正确答案，稍后重新作答，结算再次汇总；采用分层关联复习，先补弱词形，后续会话再抽查同组其它形式。

看义默写和听写、目标词形、日文汉字/假名要求采用同一动态覆盖规则：覆盖所有启用渠道，弱项优先、同级随机，不做完整组合。TTS 或音频失败不得判错。

## 8. 手写与 OCR

OCR 使用 Windows Ink 和本地 PP-OCRv5 的混合 Provider，并用预期答案约束识别。高置信结果自动判定；低置信时先要求重写或展示候选，不先泄露答案。判错后允许人工申诉，保留原始笔迹和 OCR 诊断。

手写输入从一个空格开始，末尾自动增加空格，不通过格子数量泄露答案长度；日语可切换半透明米字格，支持鼠标、触控和笔。日语默认手写、英语默认键盘，两者都按语言设置默认方式且每道题可切换；英语键盘明确禁用拼写检查、自动补全和自动纠错。

人工复核同时提供 List 和 Detail：List 展示批量概览，Detail 展示题面、答案、笔迹、OCR 候选、置信度、音频和手动覆盖。手动覆盖要重算最终学习/FSRS/错题状态，但保留原 OCR 判定供审计。

## 9. TTS 与外部接口

单个隐藏 `Dictation.TtsHost` 通过命名管道提供音频；日语使用 Piper Plus CSS10，英语使用 Sherpa-ONNX Kokoro v1.0 英式发音，模型懒加载，后台服务不显示黑框，WPF 退出时关闭。界面只显示喇叭图标，播放实际目标词形。TTS Host 只在内存中生成并传输 WAV 字节，客户端播放后释放；产品代码不得持久化音频、写入 `.wav` 或建立音频缓存。

MCP 是同一 Generic Host 中按设置启停的可选 Hosted Service。仅监听 `127.0.0.1:<固定且可配置的高位端口>/mcp`，不使用 HTTPS、token 或 CORS，并严格校验 Host/Origin；端口冲突只显示状态，不影响 WPF。设置页原子注册/注销 Codex MCP 配置。工具包括 capability/schema、离线查询、整组暂存导入、批次状态和诊断；所有写入经过 Core 业务服务，不能确认 Inbox 或直写数据库。

## 10. 统计、主题与视觉

首页使用现代、轻量、有空气感的游戏化风格，不做 ERP 式后台。主题默认跟随系统，也可手动浅色/深色。日历按语言独立打卡：日语为白圈红心，英语为圆形米字旗；每个图标约占日历格四分之一，完成该语言当日启用计划才点亮。报表展示已学数量、易错词及记忆/到期趋势。

## 11. 质量、分支与明确非目标

每个业务切片先写覆盖正常、边界、非法、异常和状态变化的测试，临时制造针对性实现错误并运行测试确认会失败，随后修正为绿；第一个中文提交完成最小实现。再打通 WPF/ViewModel→Service→DTO/Mapper→DataAccessor/PO→SQLite 或 Provider，所有测试、构建和冒烟通过后做第二个中文提交。分支必须以 `codex/` 开头，不直接在 `master` 开发。

本规格不要求一次性重写全部代码，不引入分布式读写分离、在线同步、复杂记忆自评按钮、中文反向词典查询、安装程序、代码签名、增量更新、运行时在线词库更新或 CUDA 依赖。已移除的旧 WinForms、Edge TTS 和 System TTS 不得重新作为兼容路径引入。
