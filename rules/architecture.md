# Dictation Architecture and Database Schema

> 项目架构、数据库结构以当前仓库源代码和正式迁移定义为准；项目程序版本由 `ProjectInfo.json` 管理。

## Project

Dictation 是用于单词学习、听写与练习的 Windows 桌面应用。当前项目采用 C#、.NET 10、WPF、SQLite、Dapper、Microsoft.Extensions Generic Host、依赖注入以及测试项目 `Dictation.Tests`（**xUnit v3 + Microsoft.Testing.Platform**）。

## Solution and Modules

- `Dictation.Presentation/`：WPF 界面、ViewModel、绑定、用户输入和 UI 适配。
- `Dictation.Core/`：业务用例与服务、配置、导入、学习、词汇、TTS 合同、DTO、Mapper；UI 与 MCP 通过共同的 Core 业务入口。
- `Dictation.DataAccess/`：SQLite 数据访问、Dapper SQL、连接、事务及 Schema Migration，不管理 UI 或业务规则。
- `Dictation.TtsHost/`：提供 TTS 支持进程。
- `Dictation.Updater/`：独立更新器，通过受批准的 Core 服务边界使用能力。
- `Dictation.LexiconBuilder/`：词库构建工具，使用 `Dictation.LexiconBuilder/scripts/Build-Lexicons.ps1` 校验输入并生成只读词库；固定资源来源、哈希、运行布局与许可归属见[第三方资源规范](resources.md)。
- `Dictation.Tests/`：xUnit v3 + Microsoft.Testing.Platform 测试项目。

## Layer Dependencies and Design

已批准的物理结构为 `Dictation.Presentation → Dictation.Core → Dictation.DataAccess`，各层按自身责任使用对应依赖，而不为填满架构强迫创建多余 DTO、Service 或 Mapper。
- Presentation 执行 WPF/ViewModel/绑定与交互，不写业务决策或直接访问数据库。
- Core 承担业务编排和校验；必要的 DTO、Model、Mapper 与 Provider 合同按实际业务能力目录组织。Mapper 只进行确定性的纯转换。
- DataAccess 仅进行 CRUD、SQL、事务与迁移，不反向引用 Core，不决定业务流程。
- 使用单一 Generic Host DI 容器，`AddDictationCore()` 是业务注册入口；同一个有状态实现映射多个能力接口时应共享正确的实例身份和生命周期。
- 保留基于实际需求的小能力接口、最小必要公共可见性及组合优先原则；不引入 DDD、CQRS、泛用 Repository、EF 或第二套服务容器。
- 只读词库与用户运行数据库分别管理。运行数据、配置、日志与备份目录及不可变 Resources 资源依批准规格处理，不将用户数据视为可覆盖发布资源。

## 架构边界与数据组织

- Presentation 仅负责 WPF UI、ViewModel、绑定与输入输出适配，不承载 SQL 或业务规则；Core 负责业务用例、校验、DTO、Mapper 和 Provider 合同；DataAccess 负责 CRUD、SQL、连接、事务及迁移，不反向引用 Core。UI 与 MCP 使用同一 Core 业务能力入口。
- 应用使用由 Generic Host 管理的单一 DI 容器，业务注册入口为 `AddDictationCore()`；多能力接口映射同一有状态实现时共享实例及生命周期。按业务能力目录保持职责内聚，按调用方实际需要拆分接口，优先使用组合，不机械建立接口配对或额外容器。
- DTO、业务模型与持久化对象按职责分离；纯转换由对应模块 Mapper 执行，不查数据库、不编排业务、不改变状态。Service 负责校验与流程编排，DataAccessor 负责持久化。除实际需要外，不建立通用 IMapper、泛用 Repository、DDD、CQRS 或额外 Services 项目。
- 根据实际访问需求控制类型可见性；跨程序集合同可使用 public，内部实现与 Mapper 优先 internal 或 private；变更 WPF 类型时核验 XAML 创建、绑定和资源引用。
- 程序运行数据目录为 `CONFIG/`、`DATA/`、`LOGS/`、`BACKUPS/`，必要时使用 `CACHE/`；不可变词库及 TTS/OCR 模型放在 `Resources/`。MCP 仅监听 `127.0.0.1`，通过 Core 服务访问业务数据，不绕过服务直接写用户数据库。

## 核心业务流程与算法

以下记录当前源码中已实现的行为；具体接口、边界和最新行为以所列文件为准。

- **词库检索与导入**：`LexiconLookupService` 根据语言调用日语或英语只读词库的数据访问器；`ImportInboxService` 按来源义项 ID 规范化、去重所选词条，解析包含关联成员的词组快照，暂存为导入批次，待用户作出导入去向决定后由数据访问层写入。参见 `Dictation.Core/Lexicon/LexiconLookupService.cs`、`Dictation.Core/Importing/ImportInboxService.cs`。
- **学习会话与出题**：`LearningSessionService` 优先恢复活动会话；新会话依学习计划、可练词形、每日新词额度和手动/自动选择模式确定词组。`PracticeQuestionScheduler` 在未掌握、当前计划适用的词形中先补齐尚未覆盖的题型与输出方式，同等覆盖程度优先历史错误率高者，再随机打破平局。常规重练间隔为后续 4～8 题；候选不足时依真实作答历史判断能否提前释放。参见 `Dictation.Core/Learning/LearningSessionService.cs`、`Dictation.Core/Practice/PracticeQuestionScheduler.cs`。
- **答案判定**：`AnswerEvaluationService` 对输入和可接受答案去除首尾空白并作 Unicode NFC 规范化；英语比较忽略大小写，日语精确匹配。答题状态与词形进度由会话服务结合调度器及数据访问层处理。参见 `Dictation.Core/Learning/AnswerEvaluationService.cs`。
- **复习选组与间隔安排**：`ReviewSessionSelectionPlanner` 仅选择复习计划启用、形式与输出适用且未建立记忆或已经到期的词形；候选优先包含未建立记忆的词组，然后按预计保持率由低到高、到期时间及创建时间排序，并受每日剩余额度和单次组数限制。`ReviewMemoryUpdatePlanner` 通过 `FsrsScheduler` 的 FSRS 6 参数模型更新稳定度、难度与下次日期；整次评价从未出错记 Good，否则记 Again。参见 `Dictation.Core/Review/ReviewSessionSelectionPlanner.cs`、`Dictation.Core/Review/ReviewMemoryUpdatePlanner.cs`、`Dictation.Core/Review/FsrsScheduler.cs`。
- **五十音练习**：`KanaSessionPlanner` 根据配置筛选后的项目状态排题；首次答对即完成，曾答错的项目需连续答对两次。常规重练在后续 4～8 题，队列不足时选最早可再练的未完成项目。参见 `Dictation.Core/Kana/KanaSessionPlanner.cs`。

### FSRS 数值来源与验证

复习内核参考 [Open Spaced Repetition 官方 py-fsrs](https://github.com/open-spaced-repetition/py-fsrs/tree/9446cb06605c597a063aeee49f7d188d42e34dc2)，固定 commit `9446cb06605c597a063aeee49f7d188d42e34dc2`，MIT 许可证。使用 FSRS 6 默认 21 个参数，不自行拟合或声称适合每个人的最佳参数。

目标保持率为 0.9，只接受程序根据作答得出的 Good（3）／Again（1）。会话内重复练习由学习／复习流程处理，因此数值对照使用 `Scheduler(desired_retention=0.9, learning_steps=(), relearning_steps=(), enable_fuzzing=False)`。间隔为 1–36500 整天，采用 Python 银行家舍入；遗忘概率使用经过的完整天数，同日更新使用短期稳定度公式。仅出题顺序随机，不对到期日期加 fuzz。

以下黄金向量由固定版本未修改的 `rating/state/card/review_log/scheduler.py` 调用官方 Scheduler 生成，对应回归测试为 `Dictation.Tests/Core/FsrsSchedulerTests.cs`。产品和普通单元测试不依赖 Python、网络或官方源码文件。

| 作答序列（从首次评价起的天数） | 最终稳定度 | 最终难度 | 最终间隔（天） |
| --- | --- | --- | --- |
| 0 天 Good | 2.3065 | 2.118103970459016 | 2 |
| 0 天 Again | 0.212 | 6.4133 | 1 |
| 0 天 Good；2 天 Good | 10.964332335820698 | 2.111214235785395 | 11 |
| 0 天 Good；7 天 Again | 0.7178527669042931 | 7.394502741279718 | 1 |
| 0 天 Good；0.5 天 Again | 0.7750839828558984 | 7.394502741279718 | 1 |
| 0 天 Again；0.5 天 Good | 0.24668918777567272 | 6.402115069296838 | 1 |
| 0 天 Good；2 天 Good；8 天 Good；30 天 Again | 2.452929475623042 | 7.389975788014609 | 2 |

首次 Good 后第 2 天、第 7 天的预计保持率分别为 `0.9094932559773545`、`0.8083099402375552`，同日为 `1.0`；没有状态的旧词为待测，不提供虚构保持率。公式依据可核对[官方算法说明](https://github.com/open-spaced-repetition/fsrs4anki/wiki/The-Algorithm)，实现对照以固定 commit 为准。

实现位于 `Dictation.Core/Review/FsrsScheduler.cs`，随包许可证为 `LICENSES/py-fsrs.txt`。这是最小数值移植，不包含参数优化器、Hard/Easy 自评或新的通用调度框架。

## SQLite User Database Tables

本节记录 SQLite 用户数据库最终有效的字段、类型和约束。数据库定义以实际迁移执行结果为准。

**迁移定义来源**：`Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`；`Dictation.DataAccess/Kana/KanaSchemaMigration.cs`；`Dictation.DataAccess/Statistics/StatisticsSchemaMigration.cs`；`Dictation.DataAccess/Statistics/LearningCompletionHistoryMigration.cs`。

### DailyPlanSnapshots

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | 取值限定为 Japanese、English | 语言 |
| LocalDate | TEXT | 否 | — | — | 本地日期 |
| LearningTarget | INTEGER | 否 | — | 不小于 0 | 当天有效学习目标组数 |
| ReviewTarget | INTEGER | 否 | — | 不小于 0 | 当天有效复习目标组数 |
| KanaTarget | INTEGER | 否 | — | 非负；语言不是 Japanese 时只能为 0 | 当天五十音有效目标项目数 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`LocalDate` |


### ImportBatchItems

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| ItemId | INTEGER | 否 | — | 主键 | 导入条目标识 |
| BatchId | TEXT | 否 | — | — | 导入批次标识 |
| SourceText | TEXT | 否 | — | — | 来源文本 |
| AnchorSourceSenseId | TEXT | 否 | — | — | 锚定词义标识 |
| WordGroupId | TEXT | 是 | — | — | 单词组标识 |
| Decision | TEXT | 否 | — | 取值限定为 Pending、Duplicate、Learning、Review、Discard | 导入条目的处理决定 |

**表级约束**

| 约束说明 |
|---|
| 外键：`BatchId` → `ImportBatches`（`BatchId`）；被引用记录删除时级联删除 |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时置空 |


### ImportBatches

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| BatchId | TEXT | 否 | — | 主键 | 导入批次标识 |
| Language | TEXT | 否 | — | 取值限定为 Japanese、English | 语言 |
| SourceKind | TEXT | 否 | — | 取值限定为 Manual、Text、Mcp | 来源类型 |
| Status | TEXT | 否 | — | 取值限定为 Pending、Completed | 状态 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| CompletedAtUtc | TEXT | 是 | — | — | 完成时间（UTC） |


### KanaAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| QuestionOrdinal | INTEGER | 否 | — | 不小于 0 | 题目序号 |
| KanaId | TEXT | 否 | — | — | 五十音项目标识 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | 取值限定为 0、1 | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 答题时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SessionId`、`QuestionOrdinal` |
| 外键：`SessionId`、`KanaId` → `KanaSessionItems`（`SessionId`、`KanaId`）；被引用记录删除时级联删除 |


### KanaMemories

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| KanaId | TEXT | 否 | — | 主键 | 五十音项目标识 |
| Stability | REAL | 否 | — | 大于 0 | 记忆稳定度 |
| Difficulty | REAL | 否 | — | 取值范围：1～10（含两端） | 难度 |
| LastReviewedAtUtc | TEXT | 否 | — | — | 最近复习时间（UTC） |
| DueAtUtc | TEXT | 否 | — | — | 到期时间（UTC） |
| ReviewCount | INTEGER | 否 | — | 不小于 1 | 复习次数 |
| LapseCount | INTEGER | 否 | — | 不小于 0 | 遗忘次数 |


### KanaPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Id | INTEGER | 否 | — | 主键；仅允许 1 | 记录标识 |
| Enabled | INTEGER | 否 | — | 取值限定为 0、1 | 启用标志 |
| Hiragana | INTEGER | 否 | — | 取值限定为 0、1 | 启用平假名范围 |
| Katakana | INTEGER | 否 | — | 取值限定为 0、1 | 启用片假名范围 |
| Clear | INTEGER | 否 | — | 取值限定为 0、1 | 启用清音范围 |
| Voiced | INTEGER | 否 | — | 取值限定为 0、1 | 启用浊音范围 |
| Contracted | INTEGER | 否 | — | 取值限定为 0、1 | 启用拗音范围 |
| ReviewAll | INTEGER | 否 | — | 取值限定为 0、1 | 是否全量复习 |
| DailyTarget | INTEGER | 否 | — | 取值范围：1～214（含两端） | 每日目标数量 |

**表级约束**

| 约束说明 |
|---|
| 平假名、片假名开关至少启用一个 |
| 清音、浊音、拗音开关至少启用一个 |


### KanaSessionItems

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| KanaId | TEXT | 否 | — | — | 五十音项目标识 |
| Text | TEXT | 否 | — | — | 项目字符文本 |
| Reading | TEXT | 否 | — | — | 项目读音 |
| RowHint | TEXT | 否 | — | — | 假名行提示 |
| Script | TEXT | 否 | — | 取值限定为 Hiragana、Katakana | 文字种类 |
| Range | TEXT | 否 | — | 取值限定为 Clear、Voiced、Contracted | 练习范围 |
| QueueOrder | INTEGER | 否 | — | 不小于 0 | 项目排队顺序 |
| HasWrong | INTEGER | 否 | 0 | 取值限定为 0、1 | 是否曾答错 |
| CorrectStreak | INTEGER | 否 | 0 | 不小于 0 | 连续正确次数 |
| NextEligibleOrdinal | INTEGER | 否 | 0 | 不小于 0 | 下次可答题序号 |
| IsCompleted | INTEGER | 否 | 0 | 取值限定为 0、1 | 完成标志 |
| CompletedLocalDate | TEXT | 是 | — | — | 项目完成本地日期 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SessionId`、`KanaId` |
| 外键：`SessionId` → `KanaSessions`（`SessionId`）；被引用记录删除时级联删除 |
| 未完成时完成日期必须为空；已完成时必须填写完成日期 |


### KanaSessions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | 主键 | 会话标识 |
| StartedLocalDate | TEXT | 否 | — | — | 会话开始本地日期 |
| Status | TEXT | 否 | — | 取值限定为 Active、Completed | 状态 |
| QuestionOrdinal | INTEGER | 否 | — | 不小于 0 | 题目序号 |
| CurrentKanaId | TEXT | 是 | — | — | 当前五十音项目标识 |

**表级约束**

| 约束说明 |
|---|
| 活动会话必须有当前假名；已完成会话的当前假名必须为空 |


### LearningCompletionDates

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| LocalDate | TEXT | 否 | — | — | 本地日期 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`WordGroupId`、`LocalDate` |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |


### LearningFormStates

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormCode | TEXT | 否 | — | — | 练习词形代码 |
| CorrectStreak | INTEGER | 否 | 0 | 取值范围：0～2（含两端） | 连续正确次数 |
| IsMastered | INTEGER | 否 | 0 | 取值限定为 0、1 | 是否完成掌握 |
| NextEligibleOrdinal | INTEGER | 否 | 0 | 不小于 0 | 下次可答题序号 |
| MeaningWritingCount | INTEGER | 否 | 0 | 不小于 0 | 词义书写答题次数 |
| DictationCount | INTEGER | 否 | 0 | 不小于 0 | 听写答题次数 |
| LatinCount | INTEGER | 否 | 0 | 不小于 0 | 拉丁文字答题次数 |
| KanaCount | INTEGER | 否 | 0 | 不小于 0 | 假名答题次数 |
| KanjiCount | INTEGER | 否 | 0 | 不小于 0 | 汉字答题次数 |
| CorrectCount | INTEGER | 否 | 0 | 不小于 0 | 答对次数 |
| ErrorCount | INTEGER | 否 | 0 | 不小于 0 | 答错次数 |
| LastAnsweredAtUtc | TEXT | 是 | — | — | 最近答题时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`WordGroupId`、`MemberId`、`FormCode` |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |
| 外键：`MemberId` → `WordGroupMembers`（`MemberId`）；被引用记录删除时级联删除 |


### LearningGroupProgress

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | 主键 | 单词组标识 |
| Language | TEXT | 否 | — | 取值限定为 Japanese、English | 语言 |
| FirstStartedLocalDate | TEXT | 否 | — | — | 词组首次开始学习本地日期 |
| CompletedLocalDate | TEXT | 是 | — | — | 词组学习完成本地日期 |

**表级约束**

| 约束说明 |
|---|
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |


### LearningPlanChannels

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| Channel | TEXT | 否 | — | 取值限定为 MeaningWriting、Dictation | 练习通道 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`SortOrder` |
| 唯一性约束：`Language`、`Channel` 的组合不得重复 |
| 外键：`Language` → `LearningPlans`（`Language`）；被引用记录删除时级联删除 |


### LearningPlanForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| FormCode | TEXT | 否 | — | 去掉首尾空白后不得为空 | 练习词形代码 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`SortOrder` |
| 唯一性约束：`Language`、`FormCode` 的组合不得重复 |
| 外键：`Language` → `LearningPlans`（`Language`）；被引用记录删除时级联删除 |


### LearningPlanOutputs

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| OutputMode | TEXT | 否 | — | 取值限定为 Latin、Kana、Kanji | 答案输出模式 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`SortOrder` |
| 唯一性约束：`Language`、`OutputMode` 的组合不得重复 |
| 外键：`Language` → `LearningPlans`（`Language`）；被引用记录删除时级联删除 |


### LearningPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | 主键；取值限定为 Japanese、English | 语言 |
| DailyNewTarget | INTEGER | 否 | — | 取值范围：1～500（含两端） | 每日新学词组目标 |
| SessionWordLimit | INTEGER | 否 | — | 取值范围：1～500（含两端） | 单次学习词组上限 |
| SelectionMode | TEXT | 否 | — | 取值限定为 Random、Sequential、Manual | 学习词组选择模式 |
| EnglishFormLabelMode | TEXT | 否 | — | 取值限定为 GrammarName、ExampleParadigm | 英语词形标签模式 |
| EnglishExampleSourceSenseId | TEXT | 是 | — | — | 英语例词来源词义标识 |
| UpdatedAtUtc | TEXT | 否 | — | — | 计划更新时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 非英语时标签模式必须为 GrammarName，示例来源义项必须为空 |


### LearningQuestionAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| AttemptId | INTEGER | 否 | — | 主键 | 学习答题记录标识 |
| QuestionId | INTEGER | 否 | — | 唯一 | 关联学习题目标识 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | 取值限定为 0、1 | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 学习答题时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 外键：`QuestionId` → `LearningQuestions`（`QuestionId`）；被引用记录删除时级联删除 |


### LearningQuestions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| QuestionId | INTEGER | 否 | — | 主键 | 学习题目标识 |
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormCode | TEXT | 否 | — | — | 练习词形代码 |
| Channel | TEXT | 否 | — | 取值限定为 MeaningWriting、Dictation | 练习通道 |
| OutputMode | TEXT | 否 | — | 取值限定为 Latin、Kana、Kanji | 答案输出模式 |
| QuestionOrdinal | INTEGER | 否 | — | 不小于 0 | 题目序号 |
| Status | TEXT | 否 | — | 取值限定为 Pending、Answered | 状态 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 唯一性约束：`SessionId`、`QuestionOrdinal` 的组合不得重复 |
| 外键：`SessionId` → `LearningSessions`（`SessionId`）；被引用记录删除时级联删除 |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |
| 外键：`MemberId` → `WordGroupMembers`（`MemberId`）；被引用记录删除时级联删除 |


### LearningSessionGroups

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SessionId`、`WordGroupId` |
| 唯一性约束：`SessionId`、`SortOrder` 的组合不得重复 |
| 外键：`SessionId` → `LearningSessions`（`SessionId`）；被引用记录删除时级联删除 |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |


### LearningSessions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | 主键 | 会话标识 |
| Language | TEXT | 否 | — | 取值限定为 Japanese、English | 语言 |
| Status | TEXT | 否 | — | 取值限定为 InProgress、Completed | 状态 |
| CurrentOrdinal | INTEGER | 否 | 0 | 不小于 0 | 当前题目序号 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| CompletedAtUtc | TEXT | 是 | — | — | 完成时间（UTC） |


### LearningSharedAnswerAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| QuestionId | INTEGER | 否 | — | — | 共享学习题目标识 |
| QuestionOrdinal | INTEGER | 否 | — | 不小于 0 | 题目序号 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormCode | TEXT | 否 | — | — | 练习词形代码 |
| Channel | TEXT | 否 | — | 取值限定为 MeaningWriting、Dictation | 练习通道 |
| OutputMode | TEXT | 否 | — | 取值限定为 Latin、Kana、Kanji | 答案输出模式 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | 取值限定为 0、1 | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 共享学习答题时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SessionId`、`QuestionOrdinal`、`MemberId`、`FormCode` |
| 外键：`SessionId`、`WordGroupId` → `LearningSessionGroups`（`SessionId`、`WordGroupId`）；被引用记录删除时级联删除 |
| 外键：`WordGroupId`、`MemberId`、`FormCode` → `LearningFormStates`（`WordGroupId`、`MemberId`、`FormCode`）；被引用记录删除时级联删除 |


### ReviewAnswerAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| QuestionId | INTEGER | 否 | — | — | 复习题目标识 |
| QuestionOrdinal | INTEGER | 否 | — | 不小于 0 | 题目序号 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormCode | TEXT | 否 | — | — | 练习词形代码 |
| Channel | TEXT | 否 | — | 取值限定为 MeaningWriting、Dictation | 练习通道 |
| OutputMode | TEXT | 否 | — | 取值限定为 Latin、Kana、Kanji | 答案输出模式 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | 取值限定为 0、1 | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 复习答题时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SessionId`、`QuestionOrdinal`、`MemberId`、`FormCode` |
| 外键：`SessionId`、`WordGroupId` → `ReviewSessionGroups`（`SessionId`、`WordGroupId`）；被引用记录删除时级联删除 |
| 外键：`SessionId`、`MemberId`、`FormCode` → `ReviewSessionForms`（`SessionId`、`MemberId`、`FormCode`）；被引用记录删除时级联删除 |


### ReviewFormMemories

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormCode | TEXT | 否 | — | — | 练习词形代码 |
| Stability | REAL | 否 | — | 大于 0 | 记忆稳定度 |
| Difficulty | REAL | 否 | — | 取值范围：1～10（含两端） | 难度 |
| LastReviewedAtUtc | TEXT | 否 | — | — | 最近复习时间（UTC） |
| DueAtUtc | TEXT | 否 | — | 大于 LastReviewedAtUtc | 到期时间（UTC） |
| ReviewCount | INTEGER | 否 | — | 不小于 1 | 复习次数 |
| LapseCount | INTEGER | 否 | — | 非负；不得超过 `ReviewCount` | 遗忘次数 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`MemberId`、`FormCode` |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |
| 外键：`MemberId` → `WordGroupMembers`（`MemberId`）；被引用记录删除时级联删除 |


### ReviewPlanChannels

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| Channel | TEXT | 否 | — | 取值限定为 MeaningWriting、Dictation | 练习通道 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`Channel` |
| 唯一性约束：`Language`、`SortOrder` 的组合不得重复 |
| 外键：`Language` → `ReviewPlans`（`Language`）；被引用记录删除时级联删除 |


### ReviewPlanForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| FormCode | TEXT | 否 | — | — | 练习词形代码 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`FormCode` |
| 唯一性约束：`Language`、`SortOrder` 的组合不得重复 |
| Japanese：`FormCode` 限定为 Dictionary、Polite、Te、Ta；English：限定为 Base、PresentFirstPersonSingular、PresentNonThirdPerson、ThirdPersonSingular、Past、PastParticiple、PresentParticiple、Plural、Comparative、Superlative |
| 外键：`Language` → `ReviewPlans`（`Language`）；被引用记录删除时级联删除 |


### ReviewPlanGroups

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`WordGroupId` |
| 唯一性约束：`Language`、`SortOrder` 的组合不得重复 |
| 外键：`Language` → `ReviewPlans`（`Language`）；被引用记录删除时级联删除 |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |


### ReviewPlanOutputs

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| OutputMode | TEXT | 否 | — | — | 答案输出模式 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`Language`、`OutputMode` |
| 唯一性约束：`Language`、`SortOrder` 的组合不得重复 |
| Japanese：`OutputMode` 只能为 Kana 或 Kanji；English：只能为 Latin |
| 外键：`Language` → `ReviewPlans`（`Language`）；被引用记录删除时级联删除 |


### ReviewPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | 主键；取值限定为 Japanese、English | 语言 |
| DailyReviewTarget | INTEGER | 否 | — | 取值范围：1～500（含两端） | 每日复习词组目标 |
| SessionWordLimit | INTEGER | 否 | — | 取值范围：1～500（含两端） | 单次复习词组上限 |
| RangeMode | TEXT | 否 | — | 取值限定为 All、Selected | 复习范围模式 |
| EnglishFormLabelMode | TEXT | 否 | — | 取值限定为 GrammarName、ExampleParadigm | 英语词形标签模式 |
| EnglishExampleSourceSenseId | TEXT | 是 | — | — | 英语例词来源词义标识 |
| UpdatedAtUtc | TEXT | 否 | — | — | 计划更新时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 非英语时标签模式必须为 GrammarName，示例来源义项必须为空 |
| 标签模式为 GrammarName 时示例来源义项必须为空；为 ExampleParadigm 时必须填写非空白示例来源义项 |


### ReviewQuestions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| QuestionId | INTEGER | 否 | — | 主键 | 复习题目标识 |
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormCode | TEXT | 否 | — | — | 练习词形代码 |
| Channel | TEXT | 否 | — | 取值限定为 MeaningWriting、Dictation | 练习通道 |
| OutputMode | TEXT | 否 | — | 取值限定为 Latin、Kana、Kanji | 答案输出模式 |
| QuestionOrdinal | INTEGER | 否 | — | 不小于 0 | 题目序号 |
| Status | TEXT | 否 | — | 取值限定为 Pending、Answered | 状态 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| SubmittedText | TEXT | 是 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 是 | — | 取值限定为 0、1 | 答案正确标志 |
| AnsweredAtUtc | TEXT | 是 | — | — | 复习答题时间（UTC） |

**表级约束**

| 约束说明 |
|---|
| 唯一性约束：`SessionId`、`QuestionOrdinal` 的组合不得重复 |
| 外键：`SessionId`、`WordGroupId` → `ReviewSessionGroups`（`SessionId`、`WordGroupId`）；被引用记录删除时级联删除 |
| 外键：`SessionId`、`MemberId`、`FormCode` → `ReviewSessionForms`（`SessionId`、`MemberId`、`FormCode`）；被引用记录删除时级联删除 |


### ReviewSessionForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormCode | TEXT | 否 | — | 去掉首尾空白后不得为空 | 练习词形代码 |
| CorrectStreak | INTEGER | 否 | 0 | 取值范围：0～2（含两端） | 连续正确次数 |
| IsMastered | INTEGER | 否 | 0 | 取值限定为 0、1 | 是否完成掌握 |
| NextEligibleOrdinal | INTEGER | 否 | 0 | 不小于 0 | 下次可答题序号 |
| MeaningWritingCount | INTEGER | 否 | 0 | 不小于 0 | 词义书写答题次数 |
| DictationCount | INTEGER | 否 | 0 | 不小于 0 | 听写答题次数 |
| LatinCount | INTEGER | 否 | 0 | 不小于 0 | 拉丁文字答题次数 |
| KanaCount | INTEGER | 否 | 0 | 不小于 0 | 假名答题次数 |
| KanjiCount | INTEGER | 否 | 0 | 不小于 0 | 汉字答题次数 |
| CorrectCount | INTEGER | 否 | 0 | 不小于 0 | 答对次数 |
| ErrorCount | INTEGER | 否 | 0 | 不小于 0 | 答错次数 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SessionId`、`MemberId`、`FormCode` |
| 外键：`SessionId`、`WordGroupId` → `ReviewSessionGroups`（`SessionId`、`WordGroupId`）；被引用记录删除时级联删除 |
| 外键：`MemberId` → `WordGroupMembers`（`MemberId`）；被引用记录删除时级联删除 |


### ReviewSessionGroups

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| SortOrder | INTEGER | 否 | — | 不小于 0 | 项目排序序号 |
| CompletedLocalDate | TEXT | 是 | — | — | 复习词组完成本地日期 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SessionId`、`WordGroupId` |
| 唯一性约束：`SessionId`、`SortOrder` 的组合不得重复 |
| 外键：`SessionId` → `ReviewSessions`（`SessionId`）；被引用记录删除时级联删除 |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |


### ReviewSessions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | 主键 | 会话标识 |
| Language | TEXT | 否 | — | 取值限定为 Japanese、English | 语言 |
| Status | TEXT | 否 | — | 取值限定为 InProgress、Completed | 状态 |
| PlanJson | TEXT | 否 | — | 必须是有效 JSON | 会话启动计划快照 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| StartedLocalDate | TEXT | 否 | — | — | 会话开始本地日期 |
| CompletedAtUtc | TEXT | 是 | — | — | 完成时间（UTC） |


| CurrentOrdinal | INTEGER | 否 | 0 | 不小于 0 | 当前题目序号 |


### SchemaMigrations

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Version | INTEGER | 否 | — | 主键 | 迁移版本 |
| Name | TEXT | 否 | — | — | 迁移名称 |
| AppliedAtUtc | TEXT | 否 | — | — | 迁移应用时间（UTC） |


### WordGroupForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| FormIndex | INTEGER | 否 | — | — | 词形排序序号 |
| Surface | TEXT | 否 | — | — | 词形表面文本 |
| Reading | TEXT | 是 | — | — | 词形读音 |
| FormCode | TEXT | 否 | — | 取值限定为 Dictionary、Polite、Te、Ta、Base、PresentFirstPersonSingular、PresentNonThirdPerson、ThirdPersonSingular、Past、PastParticiple、PresentParticiple、Plural、Comparative、Superlative | 词形代码 |
| ScriptCode | TEXT | 是 | — | 允许为空；非空时取值限定为 Kanji、Kana | 文字类别代码 |
| RoleCode | TEXT | 是 | — | 允许为空；非空时取值限定为 Noun、Verb、Adjective、Adverb、Other | 词汇角色代码 |
| FormLabel | TEXT | 否 | — | — | 词形显示标签 |
| DetailLabel | TEXT | 否 | — | — | 词形详情标签 |
| IsCommon | INTEGER | 否 | — | 取值限定为 0、1 | 是否常用词形 |
| IsPrimary | INTEGER | 否 | — | 取值限定为 0、1 | 是否主要词形 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`MemberId`、`FormIndex` |
| 外键：`MemberId` → `WordGroupMembers`（`MemberId`）；被引用记录删除时级联删除 |


### WordGroupMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | — | 词组成员标识 |
| MeaningIndex | INTEGER | 否 | — | — | 释义排列序号 |
| Text | TEXT | 否 | — | — | 释义文本 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`MemberId`、`MeaningIndex` |
| 外键：`MemberId` → `WordGroupMembers`（`MemberId`）；被引用记录删除时级联删除 |


### WordGroupMembers

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | 主键 | 词组成员标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| Language | TEXT | 否 | — | 取值限定为 Japanese、English | 语言 |
| SourceSenseId | TEXT | 否 | — | — | 来源词义标识 |
| SourceLexemeId | TEXT | 否 | — | — | 来源词元标识 |
| Headword | TEXT | 否 | — | — | 词组成员词头 |
| Reading | TEXT | 是 | — | — | 词组成员读音 |
| PartOfSpeechLabel | TEXT | 否 | — | — | 词性显示标签 |
| AttributeLabel | TEXT | 否 | — | — | 词汇属性标签 |
| IsPhrase | INTEGER | 否 | — | 取值限定为 0、1 | 是否短语 |
| SortOrder | INTEGER | 否 | — | — | 词组成员排序序号 |
| MeaningLanguage | TEXT | 否 | 'und' | — | 释义语言 |
| MeaningSource | TEXT | 否 | 'Legacy' | — | 释义来源 |
| MeaningPrecision | TEXT | 否 | 'Missing' | — | 释义精度 |
| KanjiUsageCode | TEXT | 是 | — | English：`KanjiUsageCode` 必须为空；Japanese：只能为 None、Common、Rare | 汉字使用类别 |

**表级约束**

| 约束说明 |
|---|
| 唯一性约束：`Language`、`SourceSenseId` 的组合不得重复 |
| 唯一性约束：`WordGroupId`、`SortOrder` 的组合不得重复 |
| 外键：`WordGroupId` → `WordGroups`（`WordGroupId`）；被引用记录删除时级联删除 |


### WordGroups

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | 主键 | 单词组标识 |
| Language | TEXT | 否 | — | 取值限定为 Japanese、English | 语言 |
| AnchorSourceSenseId | TEXT | 否 | — | — | 锚定词义标识 |
| Placement | TEXT | 否 | — | 取值限定为 Inbox、Learning、Review | 所属学习阶段 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |


## 索引

| 索引名称 | 表 | 字段 | 唯一 | 条件 |
|---|---|---|---|---|
| IX_ImportBatchItems_BatchDecision | ImportBatchItems | BatchId, Decision | 否 | — |
| IX_ImportBatchItems_WordGroupId | ImportBatchItems | WordGroupId | 否 | — |
| IX_ImportBatches_StatusLanguage | ImportBatches | Status, Language | 否 | — |
| IX_KanaSessionItems_CompletedDate | KanaSessionItems | CompletedLocalDate | 否 | — |
| IX_LearningCompletionDates_LocalDate | LearningCompletionDates | LocalDate | 否 | — |
| IX_ReviewFormMemories_WordGroupId | ReviewFormMemories | WordGroupId | 否 | — |
| IX_ReviewPlanGroups_WordGroupId | ReviewPlanGroups | WordGroupId | 否 | — |
| IX_ReviewSessionGroups_WordGroupId | ReviewSessionGroups | WordGroupId | 否 | — |
| UX_KanaSessions_Active | KanaSessions | Status | 是 | WHERE Status='Active' |
| UX_LearningQuestions_PendingSession | LearningQuestions | SessionId | 是 | WHERE Status = 'Pending' |
| UX_LearningSessions_ActiveLanguage | LearningSessions | Language | 是 | WHERE Status = 'InProgress' |
| UX_ReviewQuestions_Pending | ReviewQuestions | SessionId | 是 | WHERE Status='Pending' |
| UX_ReviewSessions_ActiveLanguage | ReviewSessions | Language | 是 | WHERE Status = 'InProgress' |

## 日语只读词库

数据定义来源：`Dictation.LexiconBuilder/JmDict/JapaneseLexiconDatabaseBuilder.cs`。

### BuildMetadata

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Key | TEXT | 否 | — | 主键 | 构建元数据键 |
| Value | TEXT | 否 | — | — | 构建元数据值 |

### Entries

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | 主键 | 日语词条标识 |
| SourceSequence | INTEGER | 否 | — | — | JMdict 来源条目序号 |
| SenseIndex | INTEGER | 否 | — | — | 来源义项序号 |
| PrimaryWritten | TEXT | 否 | — | — | 主要书写形式 |
| PrimaryReading | TEXT | 否 | — | — | 主要读音 |
| LexicalCategory | TEXT | 否 | — | — | 词汇分类 |
| VerbClass | TEXT | 否 | — | — | 动词类别 |
| Transitivity | TEXT | 否 | — | — | 及物性类别 |
| KanjiUsage | TEXT | 否 | — | 取值限定为 None、Common、Rare | 汉字使用类别 |
| IsPhrase | INTEGER | 否 | — | 取值限定为 0、1 | 是否短语 |

**表级约束**

| 约束说明 |
|---|
| 唯一性约束：`SourceSequence`、`SenseIndex` 的组合不得重复 |

### PartsOfSpeech

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | 日语词条标识 |
| SortIndex | INTEGER | 否 | — | — | 词性排列序号 |
| Value | TEXT | 否 | — | — | 词性标记 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`EntryId`、`SortIndex` |
| 外键：`EntryId` → `Entries`（`EntryId`）；被引用记录删除时级联删除 |

### SourceGlosses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | 日语词条标识 |
| Language | TEXT | 否 | — | — | 语言 |
| GlossIndex | INTEGER | 否 | — | — | 释义排列序号 |
| GlossType | TEXT | 是 | — | — | 来源释义类型 |
| Text | TEXT | 否 | — | — | 来源释义文本 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`EntryId`、`Language`、`GlossIndex` |
| 外键：`EntryId` → `Entries`（`EntryId`）；被引用记录删除时级联删除 |

### ChineseMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | 日语词条标识 |
| MeaningIndex | INTEGER | 否 | — | — | 中文释义序号 |
| Text | TEXT | 否 | — | — | 中文释义文本 |
| MeaningSource | TEXT | 否 | — | 仅允许 Tomoshi | 释义来源 |
| MeaningPrecision | TEXT | 否 | — | 仅允许 Exact | 释义精度 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`EntryId`、`MeaningIndex` |
| 外键：`EntryId` → `Entries`（`EntryId`）；被引用记录删除时级联删除 |

### Forms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| FormId | INTEGER | 否 | — | 主键 | 词形标识 |
| EntryId | TEXT | 否 | — | — | 日语词条标识 |
| Surface | TEXT | 否 | — | — | 词形表面文本 |
| Reading | TEXT | 否 | — | — | 词形读音 |
| FormKind | TEXT | 否 | — | — | 词形类别 |
| ScriptKind | TEXT | 否 | — | — | 文字类别 |
| LexicalRole | TEXT | 否 | — | — | 词汇角色 |
| IsCommon | INTEGER | 否 | — | 取值限定为 0、1 | 是否常用词形 |
| IsPrimary | INTEGER | 否 | — | 取值限定为 0、1 | 是否主要词形 |

**表级约束**

| 约束说明 |
|---|
| 唯一性约束：`EntryId`、`Surface`、`Reading`、`FormKind`、`ScriptKind`、`LexicalRole` 的组合不得重复 |
| 外键：`EntryId` → `Entries`（`EntryId`）；被引用记录删除时级联删除 |


#### 日语词库索引

| 索引名称 | 表 | 字段 | 唯一 | 过滤条件 |
|---|---|---|---|---|
| IX_Forms_Surface | Forms | Surface | 否 | — |

## 英语只读词库

数据定义来源：`Dictation.LexiconBuilder/WordNet/EnglishLexiconDatabaseBuilder.cs`。

### BuildMetadata

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Key | TEXT | 否 | — | 主键 | 构建元数据键 |
| Value | TEXT | 否 | — | — | 构建元数据值 |

### Lexemes

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LexemeId | TEXT | 否 | — | 主键 | 英语词元标识 |
| Lemma | TEXT | 否 | — | — | 词元基本形式 |
| SourceEntryKey | TEXT | 否 | — | — | 来源条目键 |
| PartOfSpeech | TEXT | 否 | — | — | 词性类别 |
| IsPhrase | INTEGER | 否 | — | 取值限定为 0、1 | 是否短语 |

**表级约束**

| 约束说明 |
|---|
| 唯一性约束：`Lemma`、`SourceEntryKey` 的组合不得重复 |

### Synsets

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | 主键 | 同义词集标识 |
| PartOfSpeech | TEXT | 否 | — | — | 同义词集词性 |
| Ili | TEXT | 是 | — | — | 跨语言词汇索引标识 |

### Senses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SenseId | TEXT | 否 | — | 主键 | 词义标识 |
| LexemeId | TEXT | 否 | — | — | 英语词元标识 |
| SynsetId | TEXT | 否 | — | — | 同义词集标识 |
| SenseOrder | INTEGER | 否 | — | — | 词义排序序号 |

**表级约束**

| 约束说明 |
|---|
| 外键：`LexemeId` → `Lexemes`（`LexemeId`） |
| 外键：`SynsetId` → `Synsets`（`SynsetId`） |

### Definitions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | — | 同义词集标识 |
| DefinitionIndex | INTEGER | 否 | — | — | 定义序号 |
| Text | TEXT | 否 | — | — | 定义文本 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SynsetId`、`DefinitionIndex` |
| 外键：`SynsetId` → `Synsets`（`SynsetId`） |

### Examples

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | — | 同义词集标识 |
| ExampleIndex | INTEGER | 否 | — | — | 例句序号 |
| Text | TEXT | 否 | — | — | 例句文本 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SynsetId`、`ExampleIndex` |
| 外键：`SynsetId` → `Synsets`（`SynsetId`） |

### Forms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| FormId | INTEGER | 否 | — | 主键 | 词形标识 |
| LexemeId | TEXT | 否 | — | — | 英语词元标识 |
| Surface | TEXT | 否 | — | — | 词形表面文本 |
| FormKind | TEXT | 否 | — | — | 词形类别 |
| FormSourceKind | TEXT | 否 | — | — | 词形来源类别 |

**表级约束**

| 约束说明 |
|---|
| 唯一性约束：`LexemeId`、`Surface`、`FormKind` 的组合不得重复 |
| 外键：`LexemeId` → `Lexemes`（`LexemeId`） |

### Derivations

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SourceSenseId | TEXT | 否 | — | — | 派生来源词义标识 |
| TargetSenseId | TEXT | 否 | — | — | 派生目标词义标识 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`SourceSenseId`、`TargetSenseId` |
| 外键：`SourceSenseId` → `Senses`（`SenseId`） |
| 外键：`TargetSenseId` → `Senses`（`SenseId`） |

### LearningSenses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | 主键 | 学习词义标识 |
| SourceLexemeId | TEXT | 否 | — | — | 来源英语词元标识 |
| Headword | TEXT | 否 | — | — | 学习词条词头 |
| PartOfSpeech | TEXT | 否 | — | — | 学习词条词性 |
| IsPhrase | INTEGER | 否 | — | 取值限定为 0、1 | 是否短语 |
| MeaningSource | TEXT | 否 | — | 取值限定为 ChineseOpenWordNet、Ecdict、Missing | 释义来源 |
| MeaningPrecision | TEXT | 否 | — | 取值限定为 Exact、Broad、Missing | 释义精度 |
| SortOrder | INTEGER | 否 | — | — | 学习词义排列序号 |

### LearningSenseSources

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | — | 学习词义标识 |
| SenseId | TEXT | 否 | — | — | 来源词义标识 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`LearningSenseId`、`SenseId` |
| 外键：`LearningSenseId` → `LearningSenses`（`LearningSenseId`） |
| 外键：`SenseId` → `Senses`（`SenseId`） |

### LearningMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | — | 学习词义标识 |
| MeaningIndex | INTEGER | 否 | — | — | 学习释义序号 |
| Text | TEXT | 否 | — | — | 学习释义文本 |

**表级约束**

| 约束说明 |
|---|
| 联合主键：`LearningSenseId`、`MeaningIndex` |
| 外键：`LearningSenseId` → `LearningSenses`（`LearningSenseId`） |

#### 英语词库索引

| 索引名称 | 表 | 字段 | 唯一 | 过滤条件 |
|---|---|---|---|---|
| IX_Lexemes_Lemma | Lexemes | Lemma | 否 | — |
| IX_Forms_Surface | Forms | Surface | 否 | — |
| IX_Senses_SynsetId | Senses | SynsetId | 否 | — |
| IX_Derivations_TargetSenseId | Derivations | TargetSenseId | 否 | — |
| IX_LearningSenses_Headword | LearningSenses | Headword, PartOfSpeech, SortOrder | 否 | — |
| IX_LearningSenseSources_SenseId | LearningSenseSources | SenseId | 否 | — |

