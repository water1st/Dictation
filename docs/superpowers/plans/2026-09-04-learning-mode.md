# 学习计划与学习模式 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付日语、英语彼此独立的学习计划、学习库管理、可恢复学习会话、动态题目覆盖、即时判题／延迟重答／结算汇总，以及单窗口 WPF 学习入口和本轮浏览；本计划先用两种语言均支持的键盘输入完成闭环，日语默认手写和 OCR 在后续手写计划接入同一答题契约。

**Architecture:** 用户词组快照仍是唯一学习内容来源；具体类 `LearningPlanDataAccessor` 负责计划／学习库 CRUD，`LearningSessionDataAccessor` 负责会话／进度事务，`LearningPlanService`、`LearningSessionService`、`AnswerEvaluationService` 分别负责计划、会话编排和确定性文本判题。WPF 继续使用 `ContentControl + UserControl` 单窗口导航，ViewModel 调用 Core 业务能力；随机次序由注入的具体 `Random` 控制，时间由 .NET `TimeProvider` 控制。接口依据能力边界和实际隔离价值设置，不能仅按实现数量判断；按调用方需要拆分，DI 保持实例身份，优先组合，纯映射归对应 Mapper。遵循仓库 AGENTS.md，不增加 Repository、DDD、CQRS 或消息总线。

**Tech Stack:** C# 14、.NET 10、WPF、Generic Host、Dapper、Microsoft.Data.Sqlite、`TimeProvider`、xUnit v3、现有内存 WAV TTS 管道、NAudio（仅内存播放适配器）。

**Spec:** `docs/requirements/RefactorSpecification.md` 第 4–7、9–11 节。

**Depends on:** `docs/superpowers/plans/2026-08-31-single-window-navigation.md` 与 `docs/superpowers/plans/2026-08-31-chinese-runtime-lexicons.md` 已全部完成；基线为 1279/1279 测试通过。

## 续接状态（2026-09-07）

Task 1–4 已实现；Core 合并与旧 Core/WinForms 清理按 `2026-09-06-core-services-consolidation.md` 完成验证，当时全量基线为 1372/1372，Task 5 首个检查点后为 1392/1392。后续从 Task 5 继续，业务服务使用 `Dictation.Core.*`，只通过 `AddDictationCore()` 注册，不再建立独立 Services 项目。

用户已批准先执行 `2026-09-07-design-principles-cleanup.md`：先修规则，再完成全项目同类设计整改，之后继续本计划 Task 5。此期间不重复执行已完成切片，不把结构整改当成学习页面或播放器已经交付。

Task 5 进行中：已新增会话 ViewModel、内存音频接口和真实 SQLite 行为测试；WPF 页面、导航接线与实际播放器尚未完成。第一个最小实现检查点含 15 项会话测试和 5 项音频回归测试，最终 1392/1392 全量测试通过（零跳过），解决方案构建 0 错误；不要把它算作 Task 5 整体验收通过。

严格 RED 记录：先编译成功，再让提交操作暂不转发到 Core；`SubmitCommand_PersistsAnswerBeforeShowingFeedback` 实际执行 1 项，因答题记录期望 1、实际 0 而失败。只补回生产 Service 调用后转绿。测试 `LearningSessionViewModelTests.cs` 的 SHA-256 前后均为 `4BE128DD2D52AD9B7A12BB706E47C6EC751C069DC5B63074C1EE1AF725EB9494`；夹具 `LearningPresentationTestEnvironment.cs` 前后均为 `857A39F76364CF0675C161EF7750D3285D9C4BA65F4DD027BD35108F1C431641`。

复核补测：新测试夹具最初使用了错误的中文语言标记，该次夹具失败不计 RED；更正为 `zh-CN` 后重新冻结。成功编译后，5 项测试均因真实行为断言失败：已听过题目后重播失败／取消导致不能提交，以及汉字听写／同形异读词反馈播放了错误文本。只修正生产实现后 5/5 转绿。`LearningSessionAudioRegressionTests.cs` 冻结与恢复后 SHA-256 均为 `96478B04EC40DDFABD708294725EE1DDDA3A2B427521CE939A6F50EC6C418842`。Core 在答题结果中返回当前成员／词形的精确 `SpokenText`，WPF 不再跨词条猜测读音；看义题提交前仍不返回发音答案。两项复核发现均已修复并经独立只读复核。

## Global Constraints

- 只在 `codex/dictation-refactor` 工作，不推送、不合并；每个提交前先确认分支和暂存范围。
- 日语与英语计划、库、会话和进度用 `Language` 强隔离；学习期间不能切换语言。
- 一整个 `WordGroup` 计一个单词；组内 POS／义项和词形是练习单元，但不能被拆成新的用户词条。
- 数据规则只使用稳定代码，不解析中文 UI 标签；显示标签由 Service 映射。
- 学习题型固定为 `MeaningWriting`（看义默写）和 `Dictation`（听写）。所有启用渠道弱项优先、同级随机，不生成笛卡尔积。
- 日语输出固定为 `Kana`、`Kanji`；罕见汉字和无汉字自动排除 `Kanji`。英语输出固定为 `Latin`。
- 每个启用的成员词形需要连续正确 2 次；两次计数之间在存在足够其它可运行单元时随机间隔 3–7 题。错误只将该成员词形的连续正确数归零，并保留本次会话“曾错”状态。
- 当整轮不足 4 个可运行成员词形、客观上无法插入 3 道其它题时，调度器先各出完所有其它可运行单元，再允许重试；这是避免少量词库永久死锁的唯一降级，不用等待时间、浏览页面或无意义题目伪造间隔。
- 同一成员／词形的所有权威变体都是可接受答案；英语忽略大小写，日语与英语均只做 Trim + Unicode NFC，不做拼写纠错或模糊匹配。
- 听写必须播放实际目标词形。TTS 或播放失败只显示错误并保留当前题，不生成答题记录、不判错。
- 本计划不实现 OCR、手写格、FSRS、错题重练、统计日历或 MCP；但数据库和 Service 契约必须允许这些后续切片读取学习结果。
- 每个 RED 必须先 `dotnet build` 成功，再运行已发现的测试得到断言失败；冻结测试哈希，只修改生产实现转绿，临时 mutation 不得提交。

---

## 文件与职责

### 稳定快照元数据

- Modify: `Dictation.DataAccess/Importing/ImportInboxPo.cs`
- Modify: `Dictation.DataAccess/Importing/ImportInboxDataAccessor.cs`
- Modify: `Dictation.Core/Importing/ImportInboxDto.cs`
- Modify: `Dictation.Core/Importing/ImportInboxService.cs`
- Modify: `Dictation.Core/Lexicon/LexiconLookupResultDto.cs`
- Modify: `Dictation.Core/Lexicon/LexiconLookupService.cs`
- Modify: `Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`

### 学习 DataAccess 与 Core 业务服务

- Create: `Dictation.DataAccess/Learning/LearningPlanPo.cs`
- Create: `Dictation.DataAccess/Learning/LearningSessionPo.cs`
- Create: `Dictation.DataAccess/Learning/LearningPlanDataAccessor.cs`
- Create: `Dictation.DataAccess/Learning/LearningSessionDataAccessor.cs`
- Create: `Dictation.DataAccess/Learning/LearningDataAccessException.cs`
- Create: `Dictation.Core/Learning/LearningPlanDto.cs`
- Create: `Dictation.Core/Learning/LearningSessionDto.cs`
- Create: `Dictation.Core/Learning/LearningPlanService.cs`
- Create: `Dictation.Core/Learning/LearningSessionService.cs`
- Create: `Dictation.Core/Learning/AnswerEvaluationService.cs`
- Modify: `Dictation.DataAccess/DictationDataAccessServiceCollectionExtensions.cs`
- Modify: `Dictation.Core/DictationCoreServiceCollectionExtensions.cs`

### WPF 单窗口流程

- Create: `Dictation.Presentation/Audio/IAudioPlayer.cs`
- Create: `Dictation.Presentation/Audio/MemoryWavAudioPlayer.cs`
- Create: `Dictation.Presentation/ViewModels/LearningPlanViewModel.cs`
- Create: `Dictation.Presentation/ViewModels/LearningSessionViewModel.cs`
- Create: `Dictation.Presentation/ViewModels/LearningLibraryItemViewModel.cs`
- Create: `Dictation.Presentation/Views/LearningPlanPage.xaml(.cs)`
- Create: `Dictation.Presentation/Views/LearningSessionPage.xaml(.cs)`
- Modify: `Dictation.Presentation/ViewModels/HomeViewModel.cs`
- Modify: `Dictation.Presentation/Views/HomePage.xaml`
- Modify: `Dictation.Presentation/Navigation/NavigationService.cs`
- Modify: `Dictation.Presentation/App.xaml`
- Modify: `Dictation.Presentation/DictationPresentationServiceCollectionExtensions.cs`
- Modify: `Dictation.Presentation/Dictation.Presentation.csproj`

### 测试

- Modify: `Dictation.Tests/DataAccess/SchemaMigrationRunnerTests.cs`
- Modify: `Dictation.Tests/DataAccess/ImportInboxDataAccessorTests.cs`
- Modify: `Dictation.Tests/Core/ImportInboxServiceTests.cs`
- Create: `Dictation.Tests/DataAccess/LearningDataAccessorTests.cs`
- Create: `Dictation.Tests/Core/LearningPlanServiceTests.cs`
- Create: `Dictation.Tests/Core/LearningSessionServiceTests.cs`
- Create: `Dictation.Tests/Core/AnswerEvaluationServiceTests.cs`
- Create: `Dictation.Tests/Presentation/LearningPlanViewModelTests.cs`
- Create: `Dictation.Tests/Presentation/LearningSessionViewModelTests.cs`
- Create: `Dictation.Tests/Presentation/LearningPageRenderingTests.cs`
- Modify: `Dictation.Tests/Presentation/HomeViewModelTests.cs`
- Modify: `Dictation.Tests/Presentation/NavigationServiceTests.cs`
- Modify: `Dictation.Tests/Presentation/SingleWindowArchitectureTests.cs`

---

## 固定契约

词库与导入 DTO 增加以下稳定成员／词形元数据：

```csharp
public sealed class LexiconLookupResultDto
{
    public string? KanjiUsageCode { get; }
}

public sealed class LexiconLookupFormDto
{
    public string FormCode { get; }
    public string? ScriptCode { get; }
    public string? RoleCode { get; }
}

public sealed class ImportWordGroupMemberDto
{
    public string? KanjiUsageCode { get; }
}

public sealed class ImportWordGroupFormDto
{
    public string Surface { get; }
    public string? Reading { get; }
    public string FormCode { get; }
    public string? ScriptCode { get; }
    public string? RoleCode { get; }
    public string FormLabel { get; }
    public string DetailLabel { get; }
    public bool IsCommon { get; }
    public bool IsPrimary { get; }
}
```

代码直接复制自不可变词库：日语使用 `Dictionary/Polite/Te/Ta`、`Kanji/Kana`、`Noun/Verb/Adjective/Adverb/Other`，成员汉字级别使用 `None/Common/Rare`；英语使用 `Base/ThirdPersonSingular/Past/PastParticiple/PresentParticiple/Plural/Comparative/Superlative`，不适用的文字、角色和汉字级别保存为 null。

学习计划与会话契约：

```csharp
public enum LearningSelectionMode { Random, Sequential, Manual }
public enum LearningQuestionChannel { MeaningWriting, Dictation }
public enum LearningOutputMode { Latin, Kana, Kanji }
public enum LearningSessionStatus { InProgress, Completed }
public enum EnglishFormLabelMode { GrammarName, ExampleParadigm }

public sealed record LearningPlanDto(
    LearningLanguage Language,
    int DailyNewTarget,
    int SessionWordLimit,
    LearningSelectionMode SelectionMode,
    IReadOnlySet<string> EnabledFormCodes,
    IReadOnlySet<LearningQuestionChannel> EnabledChannels,
    IReadOnlySet<LearningOutputMode> EnabledOutputModes,
    EnglishFormLabelMode EnglishFormLabelMode,
    string? EnglishExampleSourceSenseId);

public sealed record StartLearningSessionRequestDto(
    LearningLanguage Language,
    IReadOnlyList<string>? SelectedWordGroupIds = null);

public sealed record SubmitLearningAnswerRequestDto(
    string SessionId,
    long QuestionId,
    string Answer);

public sealed record LearningQuestionDto(
    long QuestionId,
    string WordGroupId,
    string SourceSenseId,
    string FormCode,
    string FormLabel,
    LearningQuestionChannel Channel,
    LearningOutputMode OutputMode,
    IReadOnlyList<string> Meanings,
    string? SpokenText,
    int CurrentWordNumber,
    int TotalWordCount);

public sealed record LearningAnswerResultDto(
    bool IsCorrect,
    IReadOnlyList<string> CorrectAnswers,
    LearningQuestionDto? NextQuestion,
    LearningSessionSummaryDto? Summary);
```

`LearningSessionService` exposes only use cases:

```csharp
public Task<LearningSessionDto> StartOrResumeAsync(
    StartLearningSessionRequestDto request,
    CancellationToken cancellationToken = default);

public Task<LearningAnswerResultDto> SubmitAsync(
    SubmitLearningAnswerRequestDto request,
    CancellationToken cancellationToken = default);

public Task<LearningSessionDto?> GetActiveAsync(
    LearningLanguage language,
    CancellationToken cancellationToken = default);

public Task<IReadOnlyList<LearningBrowseItemDto>> GetBrowseItemsAsync(
    string sessionId,
    CancellationToken cancellationToken = default);
```

---

### Task 1：在导入快照中保存稳定词形标识

**文件：** 稳定快照元数据文件及其现有测试。

**接口：**
- 输入：当前词库 PO 中的原始 `FormKind`、`ScriptKind`、`LexicalRole`。
- 输出：每条导入词形的 `FormCode`、`ScriptCode`、`RoleCode`；后续学习任务不解析显示标签。

- [ ] **Step 1：增加架构与映射测试**

增加迁移版本断言和往返测试：日语 `Polite/Kana/Verb`、日语 `Dictionary/Kanji/Noun`、英语 `PastParticiple`。同时覆盖英语文字／角色为 null、空 `FormCode` 非法、重复快照、取消和迁移回滚。

- [ ] **Step 2：在编译成功后证明行为 RED**

运行：

```powershell
dotnet build Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --no-restore -- --filter-class Dictation.Tests.Core.ImportInboxServiceTests
```

临时把所有 `FormCode` 映射为 `Base`；必须至少出现一个断言失败，明确显示 `Polite` 或 `PastParticiple` 被错误映射成 `Base`。恢复 mutation，记录冻结测试哈希，转绿期间不得再编辑这些测试。

- [ ] **Step 3：增加迁移 4 和生产映射**

迁移 4 为 `WordGroupMembers` 增加可空 `KanjiUsageCode`，并重建 `WordGroupForms`，加入非空 `FormCode` 及可空 `ScriptCode`、`RoleCode`。复制旧数据时为现有中文 `FormLabel`/`DetailLabel` 写出完整 `CASE` 映射，并从日语属性快照推导成员汉字级别；任一日语值或词形落到 `Unknown` 就让迁移失败。替换旧表后重建主键和外键，并扩展词库 DTO、导入 DTO/PO、Mapper、插入和读取代码，使新导入数据不再解析显示标签。

- [ ] **Step 4：验证并提交后端检查点**

运行聚焦测试、全量测试和解决方案构建，只提交本任务文件：

```powershell
git commit -m "导入：保存稳定词形标识"
```

---

### Task 2：交付按语言隔离的学习计划与学习库管理

**文件：** `LearningPlanPo.cs`、`LearningPlanDataAccessor.cs`、`LearningPlanDto.cs`、`LearningPlanService.cs`、DI 文件、学习计划 WPF 文件、导航／首页文件和测试。

**接口：**
- 输入：`WordGroups.Placement = 'Learning'` 和完整导入快照。
- 输出：计划加载／保存、分页学习库列表、整组删除、手动勾选和开始请求。

- [ ] **Step 1：编写 DataAccess 与 Service 测试**

覆盖：两种语言独立默认值；保存／读取往返；每日目标和会话数量上下界 `1..500`；至少一个题型；词形／输出方式与语言兼容；日语 Kana/Kanji 至少一个；英语只能 Latin；英语语法名称模式；示例范式必须解析到包含全部已启用动词词形的英语动词；手选必须提供非空 ID；列表排除 Inbox/Review；排序稳定；整组删除级联快照；组不存在、语言错误、SQLite 异常和取消。

- [ ] **Step 2：运行有效 RED**

测试项目成功编译后，临时让英语请求返回日语计划。运行 `LearningPlanServiceTests`，必须在语言或默认值断言处失败。恢复 mutation、冻结测试哈希，再实现生产代码。

- [ ] **Step 3：实现迁移 5 与后端**

迁移 5 创建：

```sql
CREATE TABLE LearningPlans (
    Language TEXT NOT NULL PRIMARY KEY,
    DailyNewTarget INTEGER NOT NULL CHECK (DailyNewTarget BETWEEN 1 AND 500),
    SessionWordLimit INTEGER NOT NULL CHECK (SessionWordLimit BETWEEN 1 AND 500),
    SelectionMode TEXT NOT NULL CHECK (SelectionMode IN ('Random','Sequential','Manual')),
    EnglishFormLabelMode TEXT NOT NULL CHECK (EnglishFormLabelMode IN ('GrammarName','ExampleParadigm')),
    EnglishExampleSourceSenseId TEXT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE TABLE LearningPlanForms (
    Language TEXT NOT NULL,
    FormCode TEXT NOT NULL,
    PRIMARY KEY (Language, FormCode),
    FOREIGN KEY (Language) REFERENCES LearningPlans(Language) ON DELETE CASCADE
);
CREATE TABLE LearningPlanChannels (
    Language TEXT NOT NULL,
    Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting','Dictation')),
    PRIMARY KEY (Language, Channel),
    FOREIGN KEY (Language) REFERENCES LearningPlans(Language) ON DELETE CASCADE
);
CREATE TABLE LearningPlanOutputs (
    Language TEXT NOT NULL,
    OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin','Kana','Kanji')),
    PRIMARY KEY (Language, OutputMode),
    FOREIGN KEY (Language) REFERENCES LearningPlans(Language) ON DELETE CASCADE
);
```

默认值为每日 20、每轮 20、Random、两个题型；日语启用 Dictionary + Kana + Kanji，英语启用 Base + Latin 和 `GrammarName`，同时保留 `do` 作为预选示例。示例选择复用 `LexiconLookupService` 选择真实英语动词义项，不硬编码词形字符串。保存时在同一事务中替换三类子选择。`DeleteWordGroupAsync` 验证语言和位置后只执行一次 `DELETE FROM WordGroups`，其余完全依赖声明的级联关系。

- [ ] **Step 4：提交已测试后端**

```powershell
git commit -m "学习：保存语言独立计划与词库范围"
```

- [ ] **Step 5：增加 WPF 计划页**

`LearningPlanPage` shows daily count, per-session count, Random/Sequential/Manual, form/channel/output checkboxes, paged group cards, a top “全选” checkbox, removal confirmation and “开始本轮”。English mode also chooses grammar names or an example paradigm: a source-language lookup selects a verb such as `do`/`go`, and the UI previews `do / does / did / done / doing` without using Chinese reverse lookup. Invalid combinations are disabled or blocked inline; no modal Window is created. Home adds “学习 0/20” and “学习计划” entries for the selected language.

- [ ] **Step 6：验证完整垂直链路并提交**

测试 ViewModel 校验、全选三态行为、取消、导航语言固定、删除后刷新和 XAML 绑定。随后运行 Presentation 聚焦测试、全量测试、解决方案构建和单窗口架构测试。

```powershell
git commit -m "界面：打通学习计划与词库管理"
```

---

### Task 3：创建可恢复会话与确定性选词

**文件：** `LearningSessionPo.cs`、`LearningSessionDataAccessor.cs`、会话 DTO/Service 及其测试。

**接口：**
- 输入：已保存计划、学习库词组和可选手动 ID。
- 输出：每种语言一个活动会话、持久化组顺序、每日额度核算和浏览快照。

- [ ] **Step 1：编写会话选词测试**

覆盖：先恢复后创建；日英隔离；每日剩余额度；额度不结转；每轮上限；Random 使用注入且有种子的 `Random`；Sequential 使用 `WordGroups.CreatedAtUtc, WordGroupId`；Manual 保留所选顺序并拒绝不存在、错误语言或非 Learning ID；成员／词形再多也只计一个组；历史未完成组和后来启用的补充词形优先于新词；空池返回空的完成摘要而不抛异常；删除一个组会级联其词形状态、题目和答题记录且不触碰其它组；事务失败和取消不能留下半个会话。

- [ ] **Step 2：通过选词 mutation 证明 RED**

先成功编译，再临时在选词后应用 `.Take(1)`。运行 `LearningSessionServiceTests`，必须出现断言失败，明确显示请求多个组却只得到一个。恢复 mutation、冻结测试，再实现生产代码。

- [ ] **Step 3：实现迁移 6 与选词**

迁移 6 创建以下持久化学习状态：

```sql
CREATE TABLE LearningSessions (
    SessionId TEXT NOT NULL PRIMARY KEY,
    Language TEXT NOT NULL CHECK (Language IN ('Japanese','English')),
    Status TEXT NOT NULL CHECK (Status IN ('InProgress','Completed')),
    CurrentOrdinal INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    CompletedAtUtc TEXT NULL
);
CREATE UNIQUE INDEX UX_LearningSessions_ActiveLanguage
    ON LearningSessions(Language) WHERE Status = 'InProgress';
CREATE TABLE LearningSessionGroups (
    SessionId TEXT NOT NULL,
    WordGroupId TEXT NOT NULL,
    SortOrder INTEGER NOT NULL,
    PRIMARY KEY (SessionId, WordGroupId),
    UNIQUE (SessionId, SortOrder),
    FOREIGN KEY (SessionId) REFERENCES LearningSessions(SessionId) ON DELETE CASCADE,
    FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE
);
CREATE TABLE LearningGroupProgress (
    WordGroupId TEXT NOT NULL PRIMARY KEY,
    Language TEXT NOT NULL CHECK (Language IN ('Japanese','English')),
    FirstStartedLocalDate TEXT NOT NULL,
    CompletedLocalDate TEXT NULL,
    FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE
);
CREATE TABLE LearningFormStates (
    WordGroupId TEXT NOT NULL,
    MemberId INTEGER NOT NULL,
    FormCode TEXT NOT NULL,
    CorrectStreak INTEGER NOT NULL DEFAULT 0 CHECK (CorrectStreak BETWEEN 0 AND 2),
    IsMastered INTEGER NOT NULL DEFAULT 0 CHECK (IsMastered IN (0,1)),
    NextEligibleOrdinal INTEGER NOT NULL DEFAULT 0,
    MeaningWritingCount INTEGER NOT NULL DEFAULT 0,
    DictationCount INTEGER NOT NULL DEFAULT 0,
    LatinCount INTEGER NOT NULL DEFAULT 0,
    KanaCount INTEGER NOT NULL DEFAULT 0,
    KanjiCount INTEGER NOT NULL DEFAULT 0,
    CorrectCount INTEGER NOT NULL DEFAULT 0,
    ErrorCount INTEGER NOT NULL DEFAULT 0,
    LastAnsweredAtUtc TEXT NULL,
    PRIMARY KEY (WordGroupId, MemberId, FormCode),
    FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE,
    FOREIGN KEY (MemberId) REFERENCES WordGroupMembers(MemberId) ON DELETE CASCADE
);
CREATE TABLE LearningQuestions (
    QuestionId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    SessionId TEXT NOT NULL,
    WordGroupId TEXT NOT NULL,
    MemberId INTEGER NOT NULL,
    FormCode TEXT NOT NULL,
    Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting','Dictation')),
    OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin','Kana','Kanji')),
    QuestionOrdinal INTEGER NOT NULL,
    Status TEXT NOT NULL CHECK (Status IN ('Pending','Answered')),
    CreatedAtUtc TEXT NOT NULL,
    UNIQUE (SessionId, QuestionOrdinal),
    FOREIGN KEY (SessionId) REFERENCES LearningSessions(SessionId) ON DELETE CASCADE,
    FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE,
    FOREIGN KEY (MemberId) REFERENCES WordGroupMembers(MemberId) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_LearningQuestions_PendingSession
    ON LearningQuestions(SessionId) WHERE Status = 'Pending';
CREATE TABLE LearningQuestionAttempts (
    AttemptId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    QuestionId INTEGER NOT NULL UNIQUE,
    SubmittedText TEXT NOT NULL,
    WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0,1)),
    AnsweredAtUtc TEXT NOT NULL,
    FOREIGN KEY (QuestionId) REFERENCES LearningQuestions(QuestionId) ON DELETE CASCADE
);
```

文本 ID 使用 `Guid.NewGuid().ToString("N")`，UTC 时间来自注入的 `TimeProvider`，会话及其组顺序在一个事务内写入。只有 `LearningGroupProgress.FirstStartedLocalDate` 等于 `TimeProvider.GetLocalNow().Date` 的 `yyyy-MM-dd` 值时，才算“今日新词”；恢复已有会话不得再次占用额度。

- [ ] **Step 4：提供不改变状态的浏览数据**

`GetBrowseItemsAsync` returns the session’s ordered groups with headword, Chinese meanings, POS, attribute label and enabled forms. It performs no insert/update and has a test comparing progress/attempt counts before and after browsing.

- [ ] **Step 5：验证并提交**

```powershell
git commit -m "学习：创建并恢复每日学习会话"
```

---

### Task 4：调度动态题目并持久化答题状态迁移

**文件：** `AnswerEvaluationService.cs`、学习会话 Service/DataAccessor/DTO 文件及测试。

**接口：**
- 输入：活动会话、已启用计划维度和快照词形。
- 输出：一次一个且不泄露答案的题目、原子答题迁移、即时反馈、延迟重答和最终结算。

- [ ] **Step 1：编写判题器测试**

覆盖：null／空白答案、Trim/NFC、英语忽略大小写、日语假名／汉字精确匹配、多个不规则或拼写变体、错误文字形式、不接受模糊答案以及取消。预期答案由 Service 提供，判题器不查询 SQLite。

- [ ] **Step 2：编写调度器与状态测试**

使用有种子的 `Random` 和固定 `TimeProvider`。覆盖：不展开笛卡尔积也能覆盖两个题型和所有适用输出；尚未覆盖优先，其后错误率最高优先；随机处理平局；纳入日语常用汉字并排除罕见／无汉字；短语只有基本词形；本轮完全相同题面去重；连续正确 0→1→2；第二次计数前有 3–7 个题号；错误只重置对应成员／词形；后来答对仍保留 `HadErrorInSession`；重播和浏览不改变状态；过期／外部问题 ID、重复提交、已完成会话、SQLite 异常和取消。

- [ ] **Step 3：证明两次有意义的 RED**

先把 `AnswerEvaluationService` 临时改为始终返回 true，要求错误答案断言失败；恢复后再把重试间隔临时改成零，要求顺序断言失败。两次都恢复、冻结测试后，才编写生产行为。

- [ ] **Step 4：实现最小调度器**

创建题目时，按 `(MemberId, FormCode, OutputMode)` 合并可接受变体。输出方式与题型依次按“尚未覆盖、持久化错误率最高、种子随机打破平局”选择。返回前先保存当前题，崩溃或重启后仍呈现同一题。`LearningQuestionDto` 绝不公开可接受答案；`SpokenText` 只在 Dictation 题中存在，MeaningWriting 在提交前为 null；只有提交后的 `LearningAnswerResultDto` 返回正确答案。英语默认使用语法名称；选择 `ExampleParadigm` 且示例词含目标词形代码时才显示示例词形，名词／形容词／副词专属词形继续显示语法名称。

- [ ] **Step 5：原子应用答题状态**

在同一个 SQLite 事务中：拒绝过期问题，写入答题记录，更新覆盖数／错误数／连续正确数，把下次可用题号设置为当前题号加随机 `4..8`（中间恰有 3–7 题），创建下一题或结算，并把全部掌握的整组改为 `Placement='Review'`。即使后来答对，结算仍列出首次错误。若其它单元不足三个，则各运行一次所有其它可用单元后释放最早重试；用“一个词／一个词形”测试保证降级不会死循环。

- [ ] **Step 6：验证并提交**

```powershell
git commit -m "学习：实现动态覆盖与延迟重答"
```

---

### Task 5：完成 WPF 学习会话、浏览与纯内存音频链路

**文件：** 音频适配器、学习会话 WPF 文件、首页／导航／App／DI／项目文件和 Presentation 测试。

**接口：**
- 输入：`LearningSessionService`、`TtsService`、`IAudioPlayer` 和导航固定的语言。
- 输出：同一个 `LearningSessionPage` 内的题目／浏览／反馈／结算状态；没有子 Window，也没有音频文件。

- [ ] **Step 1：编写 ViewModel 与渲染测试**

覆盖：开始／恢复；严格固定语言；空输入禁用；提交忙碌门；即时正确／错误反馈；提交后才显示正确答案；稍后重答；最终结算；打开／关闭浏览后题目不变；喇叭命令；TTS 或播放失败时状态不变；取消、重试和释放。XAML 测试断言英语 `TextBox` 设置 `SpellCheck.IsEnabled="False"`，页面没有语言切换，喇叭是只有图标且带无障碍名称的按钮，题面绑定不出现目标答案，两种页面状态都留在 `UserControl` 内。

- [ ] **Step 2：证明 Presentation RED**

成功编译后，临时让 `SubmitCommand` 不转发答案。运行 `LearningSessionViewModelTests`，必须在捕获的请求上断言失败。恢复 mutation、冻结测试，再实现生产代码。

- [ ] **Step 3：增加纯内存播放器**

```csharp
public interface IAudioPlayer
{
    Task PlayWavAsync(ReadOnlyMemory<byte> wavData, CancellationToken cancellationToken = default);
}
```

`MemoryWavAudioPlayer` uses NAudio `WaveFileReader` over a `MemoryStream` and `WaveOutEvent`; it owns and disposes both after playback, supports cancellation, and never calls `File.*`, creates `.wav`, or caches bytes. Register it as the only `IAudioPlayer`.

- [ ] **Step 4：实现页面与导航**

`HomeViewModel.OpenLearningCommand` calls `NavigationService.OpenLearningAsync(selectedLanguage)`. The navigation scope resolves `LearningSessionViewModel`, pins the language, and resumes the same active session. The page contains a compact prompt card, Chinese meaning/form/output labels, plain TextBox, submit button, progress, inline feedback, “浏览本轮” panel and summary panel. The icon-only replay button appears during Dictation and after feedback, but is hidden during an unanswered MeaningWriting question. Browse cards show headword, Chinese meaning, POS, Japanese verb/transitivity/kanji attributes or English enabled forms, and an icon-only speaker for the selected target form. Back returns home with the session persisted.

- [ ] **Step 5：验证完整垂直切片并提交**

运行所有学习 Presentation 测试、TTS 测试、单窗口测试、全量测试和解决方案构建。源码扫描不得发现新的 `Window`、`.wav` 写入、`File.Write*` 音频路径或学习页语言切换。

```powershell
git commit -m "界面：打通学习答题浏览与内存发音"
```

---

### Task 6：显示准确的每日学习进度并完成最终验收

**文件：** 学习 Service/DataAccessor DTO、`HomeViewModel.cs`、`HomePage.xaml`、测试；仅在实际命名变化时修改 `docs/requirements/RefactorSpecification.md`。

**接口：**
- 输入：今天首次开始／完成的不同组和当前语言计划。
- 输出：首页 `学习 7/20` 等标签；从计划／会话返回后刷新，不宣称复习或连续打卡状态。

- [x] **Step 1：测试进度语义**

覆盖：零状态；部分完成；词形／答题再多也只统计一次已完成组；本地日期边界；日英隔离；目标改变；返回首页刷新；DataAccess 失败内联显示；取消。

- [x] **Step 2：证明 RED 并实现**

先成功编译，再临时按答题次数而不是不同组计数，并要求聚焦测试断言失败。恢复 mutation、冻结测试。实现一个按语言／日期聚合的查询，并由 `HomeViewModel` 暴露 `LearningProgressLabel`。

- [x] **Step 3：运行验收门禁**

```powershell
dotnet build Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git status --short --branch
rg --files -g "*.db" -g "!**/bin/**" -g "!**/obj/**"
```

另对两种语言执行手动 WPF 冒烟：打开学习计划，修改并保存，选择／开始一个组，先答错再稍后答对，浏览且确认进度不变，重播目标音频，返回首页确认学习计数。确认没有次级 Window，应用根目录下也没有 `.wav` 文件。

- [x] **Step 4：提交学习模式最终集成**

```powershell
git commit -m "学习：显示每日进度并完成模式验收"
```

**验收记录（2026-09-29）：** 隔离发布版实际完成英／日各一组的错答、即时反馈、浏览和发音、稍后答对及结算；SQLite 中两语言各 1 组、3 次作答、1 次错答。两门语言首页分别显示 `学习 1/20`，总结均保留“本轮曾错”。窗口始终唯一，关闭后 TTS 宿主退出，运行目录无 `.wav`。全量测试 1444/1444，测试项目与解决方案构建均为 0 警告、0 错误。发音按钮与内存音频已验证，不把未采集的听感质量视作已验收。

---

## 验收出口

- Imported snapshots contain stable form/script/role codes and old schema data migrates without reset.
- Each language independently saves a valid learning plan and manages only its own Learning groups.
- Random, sequential and manual selection are deterministic under tests; active sessions survive navigation/restart.
- Dynamic questions cover enabled channels/output modes without Cartesian expansion; each member/form obeys two consecutive correct and 3–7 intervening questions.
- Immediate feedback, delayed retry, end summary and “later correct still remembers first error” are persisted and verified.
- Fully mastered groups move as whole groups to Review; newly enabled forms can later re-enter supplemental learning.
- WPF remains one window; browse does not mutate progress; TTS and playback never persist audio; failures do not create wrong answers.
- Homepage shows per-language daily learning progress. Full tests, build, static scans and two-language WPF smoke all pass before the last commit.
