# Dictation Architecture

> 项目架构以当前仓库源码为准；数据库表结构、约束和索引见 [数据库结构文档](database-schema.md)；项目程序版本由 `ProjectInfo.json` 管理。

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
