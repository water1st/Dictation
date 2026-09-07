# 设计原则与代码组织整改计划

> 用户已确认本计划：先修订 AGENTS.md，再串行修改代码；完成后从学习模式 Task 5 中断点继续原 GOAL，不另建目标。

**Goal:** 全项目按职责划分、能力接口隔离、组合优先、最小可见性、一文件一主要类型和必要中文 XML 注释整改，保持已交付产品行为不变。

**Architecture:** 维持 Presentation → Core → DataAccess、单一 Generic Host/DI、Dapper/SQLite。业务与映射按业务目录内聚；只为真实能力边界引入接口，不增加 Repository、DDD、CQRS、通用 IMapper 或继承框架。

**Tech Stack:** 仓库现有 .NET 10、WPF、Microsoft.Extensions.*、Dapper、SQLite、xUnit v3、Microsoft.Testing.Platform；不为本次整改新增第三方框架。

**Spec:** 用户本轮九项设计原则、修订后的 AGENTS.md、docs/requirements/RefactorSpecification.md。

## Global Constraints

- 只在 D:/Source/Repos/Dictation-refactor 的 codex/dictation-refactor 分支操作；不推送、不合并，不修改原 master 工作区或用户资源。
- AGENTS.md 必须先修订并核查，再改代码；规则文档单独中文提交，业务整改按约 2–3 人/小时可验证小批次提交，不能积累一次性大提交。
- 不更改词库 schema、用户数据库格式、学习调度／判题规则、UI 视觉和已批准行为；保持中文释义、整组词形、单窗口转场和内存音频。
- 同一实现的能力接口别名必须保留实例身份、生命周期及释放责任；无抽象价值的具体类继续通过 DI 使用。
- 纯映射归 Mapper，不把查询、业务校验、状态变化或调度算法搬进 Mapper。纯请求创建、WPF 绑定不是需要机械抽层的理由。
- 正常、边界、非法、异常、取消和状态测试先行。RED 必须构建成功、测试真实执行、行为断言失败；冻结测试后只修实现转绿，不提交 mutation。测试断言有误则修正后重新冻结并重做整轮证据。
- 纯文件移动和注释不制造无意义 RED，用静态审查和前后回归验证。接口／映射／算法调整需要真实行为证据；现有基线 1392 项，数量变化逐项说明，不跳过测试规避失败。
- 每批同步受影响调用方、DI 和测试；不铺空接口。一个实现可实现多个小能力接口，已有合理接口、框架继承和私有附属类型保留。
- 每个类／接口说明职责；公开及业务方法说明用途、参数和非直观返回值；注释用简体中文，复杂逻辑解释原因和边界，不逐行翻译。

## 基线与范围

起点 HEAD：769f602。五个生产项目有 116 个 C# 文件、206 个顶层类型，其中 22 个文件含多个顶层类型；206 个顶层类型和 150 个公开方法缺少 XML 职责说明。测试项目 53 个源文件均为单一顶层类型，注释也需复核。排除 bin/obj、第三方来源、Resources 和生成代码。

整改前实际验证：`dotnet build Dictation.sln --configuration Debug --no-restore '-clp:ErrorsOnly;Summary'` 成功，0 错误、72 警告；`dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1392` 为 1392/1392，零跳过。警告在后续受影响批次核查，不将隐藏警告视为消除。

## Tasks

### Task 1：规则先行（文档）

- [ ] 修订 AGENTS.md：消除仅多 Provider 才能用接口的限制，明确职责、接口隔离、组合、最小可见性、一文件一主要类型、XML 注释和主动同类审查。
- [ ] 同步 RefactorSpecification.md 第 2 节及学习模式计划的冲突表述；保存本计划，不重写历史验收事实。
- [ ] 检查范围、Markdown、冲突标记、`git diff --check`，单独中文提交；此时生产代码不得变化。

### Task 2：词库能力与映射（结构＋局部整理）

涉及 Core/Lexicon/LexiconLookupService.cs、LexiconLookupResultDto.cs、LexiconSearchRequestDto.cs，DataAccess/Lexicon 两个 LookupPo 文件，Core/DictationCoreServiceCollectionExtensions.cs，两个查询消费者 ImportInboxService/LearningPlanService，Presentation 的 LexiconLookupViewModel/LearningPlanViewModel 及对应测试。

- [ ] 先补查询、整组解析、中文元数据、未知／非法／取消及 DI 身份的特征测试，完成可编译的行为 RED→GREEN；最小检查点中文提交。
- [ ] 拆分独立 DTO、PO、枚举；提取内部 JapaneseLexiconMapper、EnglishLexiconMapper，标签和纯转换归 Mapper，查询与输入校验留 Service。
- [ ] 按真实消费者区分词库检索与整组解析接口；同一 LexiconLookupService 实现并由原单例提供，调用方仅注入所需能力；不增加第二个容器。
- [ ] 收窄仅内部使用的映射辅助可见性，补齐该批类型和方法 XML 注释，保持公开结果及错误行为。
- [ ] 聚焦 Core/Lexicon、两种词库 DataAccess、查询／计划 ViewModel、架构测试，随后全量构建／测试和相关 WPF 查询渲染／交互冒烟，中文提交。

### Task 3：导入 DTO、Mapper 与能力隔离（结构调整）

涉及 Core/Importing/ImportInboxDto.cs、ImportInboxService.cs、DataAccess/Importing/ImportInboxPo.cs，收件箱／查询 ViewModel、DI 及测试。

- [ ] 分开独立 DTO、请求、枚举和 PO；StageImportBatchResultDto 改为包含 ImportBatchDto 的组合，不用继承复用字段，更新仓内调用方。
- [ ] 提取批次 Mapper 与共享词组 Mapper；保持中文筛选、元数据和词形完整，业务校验／去重／确认仍由原业务链路处理。
- [ ] 暂存导入与收件箱查询／确认／释义维护按实际消费者隔离能力接口，不改变同实例语义。
- [ ] 以整组暂存、重复统计、中文补全、缺失中文阻止确认、非法锚点／选项、取消和回滚测试验证；冻结测试后执行可编译 mutation 与恢复，两个可验证中文提交。

### Task 4：学习职责与类型组织（结构调整）

涉及 Core/Learning/LearningPlanDto.cs、LearningSessionDto.cs、LearningPlanService.cs、LearningSessionService.cs，DataAccess/Learning 三组 PO 文件、学习 ViewModel、DI 和测试。

- [ ] 拆开 DTO／PO／枚举，提取计划与会话 Mapper，共用词组转换，不复制三套成员映射。
- [ ] 含词库加载的 MapPlanAsync 保留业务编排并按实际用途命名；默认值、示例词校验和答案／发音选择属于业务规则。
- [ ] 将纯出题调度从会话编排拆为具体组件，经 DI 组合；只有真正需要隔离持久化结构的算法输入／输出才使用业务模型。
- [ ] 按实际调用方隔离计划、词库管理和会话能力；不按方法数量机械拆接口。
- [ ] 保持每日额度、恢复、动态覆盖、3–7 题延迟、连续两次正确、错误重置、精确发音及拒绝过期／重复提交；实际 RED→GREEN 后跑相关 Core、DataAccess、ViewModel 和全量测试，分小批中文提交。

### Task 5：DataAccess 快照装配（结构调整）

涉及 ImportInboxDataAccessor.cs、LearningPlanDataAccessor.cs、LearningSessionDataAccessor.cs 与对应测试。

- [ ] 将重复词组快照读取收敛为 DataAccess 内具体读取组件，纯行／PO 转换归本层 Mapper；真正私有附属行类型可保留，跨组件共享的行类型单独 internal 文件。
- [ ] 查询与 SQL 不搬到 Mapper；组件复用现有连接／事务，不能新增跨层查询或改变提交、取消与回滚边界。
- [ ] 用真实 SQLite 验证字段／顺序完整、空组／空集合、错误与事务原子性；可编译 mutation 验证读取结果断言，回归通过后中文提交。

### Task 6：Presentation 能力隔离（结构＋局部整理）

涉及 NavigationService.cs、OverlayService.cs、ShellViewModel.cs、ImportInboxDetailViewModel.cs、LexiconLookupViewModel.cs、相关消费者、DI、XAML 和测试。

- [ ] 分开独立顶层类型，去掉未使用的 OverlayService 注入。
- [ ] 导航区分页面发起能力与宿主状态／转场完成；弹层区分请求显示与宿主状态／接受／取消，依赖最小接口，同实现同 DI 实例。
- [ ] Shell 不暴露不必要的具体实现；保持单窗口、页面 scope、返回刷新、状态保留、动画完成后的释放和 UI 线程边界。
- [ ] WPF 必需的 public 属性／类型保留并验证绑定，不引入页面间互相依赖、基类体系或第二套导航。
- [ ] 先验证真实状态／取消／释放行为 RED→GREEN，再跑导航、弹层、页面渲染／交互及全量测试，中文提交。

### Task 7：配置、TTS 与构建工具整理（局部整理为主）

涉及 Core/Configuration/ApplicationSettingsMapper.cs、ApplicationSettingsDocument.cs、Core/Tts/TtsPipeProtocol.cs 和 Provider／生命周期，TtsHost 各实现与测试，LexiconBuilder/BuilderOptions.cs、Program.cs、WordNet/JmDict 多类型文件。

- [ ] 拆独立类型，内部实现用最小可见性，跨程序集 TTS 合同保持可用；现有四个合理能力接口保留。
- [ ] 确认 UnconfiguredTtsSynthesisEngine 没有生产调用后删除占位实现及仅针对占位的过时测试，并记录测试数量变化。
- [ ] 配置继续复用 IConfiguration 和已有原子写回封装；不重复造库功能。
- [ ] 补职责、参数、算法约束注释；保持管道协议、隐藏进程、按需模型加载、纯内存 WAV、中文词库及词形生成结果。
- [ ] 聚焦配置、TTS、LexiconBuilder 测试，相关真实 TTS／词库烟测与全量回归通过后按模块中文提交。

### Task 8：全项目收口与恢复原目标（验证）

- [ ] 重扫全部生产与测试源文件，复核顶层类型、纯转换位置、接口消费者、可见性、XML 文档、依赖方向及 DI 生命周期；为必要例外记录理由，不靠白名单藏问题。
- [ ] 复核现有架构测试，新增测试必须验证真实合同而非只 grep 类名；使用现有工具／编译器诊断，不引入重型分析框架。
- [ ] 全量构建、测试、diff 检查、相关运行冒烟，记录新增／删除测试与真实警告；完成独立整体复核。
- [ ] 更新本计划完成记录和学习模式续接点，继续其 Task 5 页面／导航／内存播放器，随后推进原 GOAL 其它已批准切片；不把本整改当成整个 GOAL 完成。

## 通用验证命令

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1392
git diff --check
```

聚焦测试使用实际类名和合理的 minimum-expected-tests。每次报告必须包含真实 build 结果、执行数量、失败／跳过数量与 mutation 恢复证据；不能把编译失败或未执行步骤称为通过。
