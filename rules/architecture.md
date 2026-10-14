# Architecture and Verified Target Structure

> 技术事实取自 Dictation `codex/dictation-refactor` 已批准的参考架构；本 PR 基于旧版 `master`，此处描述目标结构，**不宣称 `master` 已实现这些模块和表**。项目版本信息以 `ProjectInfo.json` 为准；SDK 补丁号不在架构中锁死。

## Project

Dictation 是用于单词学习、听写与练习的 Windows 桌面应用。已批准的目标方案采用 C#、.NET 10、WPF、SQLite、Dapper、Microsoft.Extensions Generic Host、依赖注入以及测试项目 `Dictation.Tests`（**xUnit v3 + Microsoft.Testing.Platform**）。

## Solution and Modules

- `Dictation.Presentation/`：WPF 界面、ViewModel、绑定、用户输入和 UI 适配。
- `Dictation.Core/`：业务用例与服务、配置、导入、学习、词汇、TTS 合同、DTO、Mapper；UI 与 MCP 通过共同的 Core 业务入口。
- `Dictation.DataAccess/`：SQLite 数据访问、Dapper SQL、连接、事务及 Schema Migration，不管理 UI 或业务规则。
- `Dictation.TtsHost/`：按已批准方案提供 TTS 支持进程。
- `Dictation.Updater/`：独立更新器，通过受批准的 Core 服务边界使用能力。
- `Dictation.LexiconBuilder/`：词库构建工具。
- `Dictation.Tests/`：**直接采用仓库既有的 xUnit v3 测试项目**；测试 Runner 使用 Microsoft.Testing.Platform，不额外创建第二套单元测试工程。
- `docs/requirements/RefactorSpecification.md`：目标重构的权威需求来源，未实现的规格不能视作已存在的生产行为。

## Layer Dependencies and Design

已批准的物理结构为 `Dictation.Presentation → Dictation.Core → Dictation.DataAccess`，各层按自身责任使用对应依赖，而不为填满架构强迫创建多余 DTO、Service 或 Mapper。
- Presentation 执行 WPF/ViewModel/绑定与交互，不写业务决策或直接访问数据库。
- Core 承担业务编排和校验；必要的 DTO、Model、Mapper 与 Provider 合同按实际业务能力目录组织。Mapper 只进行确定性的纯转换。
- DataAccess 仅进行 CRUD、SQL、事务与迁移，不反向引用 Core，不决定业务流程。
- 使用单一 Generic Host DI 容器，`AddDictationCore()` 是业务注册入口；同一个有状态实现映射多个能力接口时应共享正确的实例身份和生命周期。
- 保留基于实际需求的小能力接口、最小必要公共可见性及组合优先原则；不引入 DDD、CQRS、泛用 Repository、EF 或第二套服务容器。
- 只读词库与用户运行数据库分别管理。运行数据、配置、日志与备份目录及不可变 Resources 资源依批准规格处理，不将用户数据视为可覆盖发布资源。

## SQLite User Database Tables

本节按目标参考分支的实际迁移定义记录用户数据库的表字段、SQLite 类型、主外键、CHECK、UNIQUE 等约束；表定义及后续添加字段按来源保留，需以完整迁移顺序应用的实际结果为准。引入此文档并不执行任何迁移。

**迁移定义来源**：`Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`；`Dictation.DataAccess/Kana/KanaSchemaMigration.cs`；`Dictation.DataAccess/Statistics/StatisticsSchemaMigration.cs`；`Dictation.DataAccess/Statistics/LearningCompletionHistoryMigration.cs`。

### DailyPlanSnapshots

```sql
CREATE TABLE DailyPlanSnapshots (
                Language TEXT NOT NULL CHECK (Language IN ('Japanese','English')),
                LocalDate TEXT NOT NULL,
                LearningTarget INTEGER NOT NULL CHECK (LearningTarget>=0),
                ReviewTarget INTEGER NOT NULL CHECK (ReviewTarget>=0),
                KanaTarget INTEGER NOT NULL CHECK (KanaTarget>=0 AND (Language='Japanese' OR KanaTarget=0)),
                PRIMARY KEY (Language,LocalDate)
);
```

### ImportBatchItems

```sql
CREATE TABLE ImportBatchItems (
                        ItemId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        BatchId TEXT NOT NULL,
                        SourceText TEXT NOT NULL,
                        AnchorSourceSenseId TEXT NOT NULL,
                        WordGroupId TEXT NULL,
                        Decision TEXT NOT NULL CHECK (Decision IN ('Pending', 'Duplicate', 'Learning', 'Review', 'Discard')),
                        FOREIGN KEY (BatchId) REFERENCES ImportBatches (BatchId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE SET NULL
);
```

### ImportBatches

```sql
CREATE TABLE ImportBatches (
                        BatchId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        SourceKind TEXT NOT NULL CHECK (SourceKind IN ('Manual', 'Text', 'Mcp')),
                        Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Completed')),
                        CreatedAtUtc TEXT NOT NULL,
                        CompletedAtUtc TEXT NULL
);
```

### KanaAttempts

```sql
CREATE TABLE KanaAttempts (
                SessionId TEXT NOT NULL,
                QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal>=0),
                KanaId TEXT NOT NULL,
                SubmittedText TEXT NOT NULL,
                WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0,1)),
                AnsweredAtUtc TEXT NOT NULL,
                PRIMARY KEY (SessionId,QuestionOrdinal),
                FOREIGN KEY (SessionId,KanaId) REFERENCES KanaSessionItems(SessionId,KanaId) ON DELETE CASCADE
);
```

### KanaMemories

```sql
CREATE TABLE KanaMemories (
                KanaId TEXT PRIMARY KEY,
                Stability REAL NOT NULL CHECK (Stability>0),
                Difficulty REAL NOT NULL CHECK (Difficulty BETWEEN 1 AND 10),
                LastReviewedAtUtc TEXT NOT NULL,
                DueAtUtc TEXT NOT NULL,
                ReviewCount INTEGER NOT NULL CHECK (ReviewCount>=1),
                LapseCount INTEGER NOT NULL CHECK (LapseCount>=0)
);
```

### KanaPlans

```sql
CREATE TABLE KanaPlans (
                Id INTEGER PRIMARY KEY CHECK (Id=1),
                Enabled INTEGER NOT NULL CHECK (Enabled IN (0,1)),
                Hiragana INTEGER NOT NULL CHECK (Hiragana IN (0,1)),
                Katakana INTEGER NOT NULL CHECK (Katakana IN (0,1)),
                Clear INTEGER NOT NULL CHECK (Clear IN (0,1)),
                Voiced INTEGER NOT NULL CHECK (Voiced IN (0,1)),
                Contracted INTEGER NOT NULL CHECK (Contracted IN (0,1)),
                ReviewAll INTEGER NOT NULL CHECK (ReviewAll IN (0,1)),
                DailyTarget INTEGER NOT NULL CHECK (DailyTarget BETWEEN 1 AND 214),
                CHECK (Hiragana + Katakana >= 1),
                CHECK (Clear + Voiced + Contracted >= 1)
);
```

### KanaSessionItems

```sql
CREATE TABLE KanaSessionItems (
                SessionId TEXT NOT NULL,
                KanaId TEXT NOT NULL,
                Text TEXT NOT NULL,
                Reading TEXT NOT NULL,
                RowHint TEXT NOT NULL,
                Script TEXT NOT NULL CHECK (Script IN ('Hiragana','Katakana')),
                Range TEXT NOT NULL CHECK (Range IN ('Clear','Voiced','Contracted')),
                QueueOrder INTEGER NOT NULL CHECK (QueueOrder>=0),
                HasWrong INTEGER NOT NULL DEFAULT 0 CHECK (HasWrong IN (0,1)),
                CorrectStreak INTEGER NOT NULL DEFAULT 0 CHECK (CorrectStreak>=0),
                NextEligibleOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (NextEligibleOrdinal>=0),
                IsCompleted INTEGER NOT NULL DEFAULT 0 CHECK (IsCompleted IN (0,1)),
                CompletedLocalDate TEXT NULL,
                PRIMARY KEY (SessionId,KanaId),
                FOREIGN KEY (SessionId) REFERENCES KanaSessions(SessionId) ON DELETE CASCADE,
                CHECK ((IsCompleted=0 AND CompletedLocalDate IS NULL) OR (IsCompleted=1 AND CompletedLocalDate IS NOT NULL))
);
```

### KanaSessions

```sql
CREATE TABLE KanaSessions (
                SessionId TEXT PRIMARY KEY,
                StartedLocalDate TEXT NOT NULL,
                Status TEXT NOT NULL CHECK (Status IN ('Active','Completed')),
                QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal>=0),
                CurrentKanaId TEXT NULL,
                CHECK ((Status='Active' AND CurrentKanaId IS NOT NULL) OR (Status='Completed' AND CurrentKanaId IS NULL))
);
```

### LearningCompletionDates

```sql
CREATE TABLE LearningCompletionDates (
                WordGroupId TEXT NOT NULL,
                LocalDate TEXT NOT NULL,
                PRIMARY KEY (WordGroupId,LocalDate),
                FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE
);
```

### LearningFormStates

```sql
CREATE TABLE LearningFormStates (
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        CorrectStreak INTEGER NOT NULL DEFAULT 0 CHECK (CorrectStreak BETWEEN 0 AND 2),
                        IsMastered INTEGER NOT NULL DEFAULT 0 CHECK (IsMastered IN (0, 1)),
                        NextEligibleOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (NextEligibleOrdinal >= 0),
                        MeaningWritingCount INTEGER NOT NULL DEFAULT 0 CHECK (MeaningWritingCount >= 0),
                        DictationCount INTEGER NOT NULL DEFAULT 0 CHECK (DictationCount >= 0),
                        LatinCount INTEGER NOT NULL DEFAULT 0 CHECK (LatinCount >= 0),
                        KanaCount INTEGER NOT NULL DEFAULT 0 CHECK (KanaCount >= 0),
                        KanjiCount INTEGER NOT NULL DEFAULT 0 CHECK (KanjiCount >= 0),
                        CorrectCount INTEGER NOT NULL DEFAULT 0 CHECK (CorrectCount >= 0),
                        ErrorCount INTEGER NOT NULL DEFAULT 0 CHECK (ErrorCount >= 0),
                        LastAnsweredAtUtc TEXT NULL,
                        PRIMARY KEY (WordGroupId, MemberId, FormCode),
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
);
```

### LearningGroupProgress

```sql
CREATE TABLE LearningGroupProgress (
                        WordGroupId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        FirstStartedLocalDate TEXT NOT NULL,
                        CompletedLocalDate TEXT NULL,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
);
```

### LearningPlanChannels

```sql
CREATE TABLE LearningPlanChannels (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        PRIMARY KEY (Language, SortOrder),
                        UNIQUE (Language, Channel),
                        FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE
);
```

### LearningPlanForms

```sql
CREATE TABLE LearningPlanForms (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        FormCode TEXT NOT NULL CHECK (length(trim(FormCode)) > 0),
                        PRIMARY KEY (Language, SortOrder),
                        UNIQUE (Language, FormCode),
                        FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE
);
```

### LearningPlanOutputs

```sql
CREATE TABLE LearningPlanOutputs (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')),
                        PRIMARY KEY (Language, SortOrder),
                        UNIQUE (Language, OutputMode),
                        FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE
);
```

### LearningPlans

```sql
CREATE TABLE LearningPlans (
                        Language TEXT NOT NULL PRIMARY KEY CHECK (Language IN ('Japanese', 'English')),
                        DailyNewTarget INTEGER NOT NULL CHECK (DailyNewTarget BETWEEN 1 AND 500),
                        SessionWordLimit INTEGER NOT NULL CHECK (SessionWordLimit BETWEEN 1 AND 500),
                        SelectionMode TEXT NOT NULL CHECK (SelectionMode IN ('Random', 'Sequential', 'Manual')),
                        EnglishFormLabelMode TEXT NOT NULL CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')),
                        EnglishExampleSourceSenseId TEXT NULL,
                        UpdatedAtUtc TEXT NOT NULL,
                        CHECK (
                            Language = 'English'
                            OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)
                        )
);
```

### LearningQuestionAttempts

```sql
CREATE TABLE LearningQuestionAttempts (
                        AttemptId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        QuestionId INTEGER NOT NULL UNIQUE,
                        SubmittedText TEXT NOT NULL,
                        WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0, 1)),
                        AnsweredAtUtc TEXT NOT NULL,
                        FOREIGN KEY (QuestionId) REFERENCES LearningQuestions (QuestionId) ON DELETE CASCADE
);
```

### LearningQuestions

```sql
CREATE TABLE LearningQuestions (
                        QuestionId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')),
                        QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal >= 0),
                        Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Answered')),
                        CreatedAtUtc TEXT NOT NULL,
                        UNIQUE (SessionId, QuestionOrdinal),
                        FOREIGN KEY (SessionId) REFERENCES LearningSessions (SessionId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
);
```

### LearningSessionGroups

```sql
CREATE TABLE LearningSessionGroups (
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        PRIMARY KEY (SessionId, WordGroupId),
                        UNIQUE (SessionId, SortOrder),
                        FOREIGN KEY (SessionId) REFERENCES LearningSessions (SessionId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
);
```

### LearningSessions

```sql
CREATE TABLE LearningSessions (
                        SessionId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        Status TEXT NOT NULL CHECK (Status IN ('InProgress', 'Completed')),
                        CurrentOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (CurrentOrdinal >= 0),
                        CreatedAtUtc TEXT NOT NULL,
                        CompletedAtUtc TEXT NULL
);
```

### LearningSharedAnswerAttempts

```sql
CREATE TABLE LearningSharedAnswerAttempts (
                        SessionId TEXT NOT NULL,
                        QuestionId INTEGER NOT NULL,
                        QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal >= 0),
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting','Dictation')),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin','Kana','Kanji')),
                        SubmittedText TEXT NOT NULL,
                        WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0,1)),
                        AnsweredAtUtc TEXT NOT NULL,
                        PRIMARY KEY (SessionId, QuestionOrdinal, MemberId, FormCode),
                        FOREIGN KEY (SessionId, WordGroupId) REFERENCES LearningSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId, MemberId, FormCode) REFERENCES LearningFormStates (WordGroupId, MemberId, FormCode) ON DELETE CASCADE
);
```

### ReviewAnswerAttempts

```sql
CREATE TABLE ReviewAnswerAttempts (
                        SessionId TEXT NOT NULL,
                        QuestionId INTEGER NOT NULL,
                        QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal >= 0),
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting','Dictation')),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin','Kana','Kanji')),
                        SubmittedText TEXT NOT NULL,
                        WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0,1)),
                        AnsweredAtUtc TEXT NOT NULL,
                        PRIMARY KEY (SessionId, QuestionOrdinal, MemberId, FormCode),
                        FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE
);
```

### ReviewFormMemories

```sql
CREATE TABLE ReviewFormMemories (
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Stability REAL NOT NULL CHECK (Stability > 0),
                        Difficulty REAL NOT NULL CHECK (Difficulty BETWEEN 1 AND 10),
                        LastReviewedAtUtc TEXT NOT NULL,
                        DueAtUtc TEXT NOT NULL CHECK (DueAtUtc > LastReviewedAtUtc),
                        ReviewCount INTEGER NOT NULL CHECK (ReviewCount >= 1),
                        LapseCount INTEGER NOT NULL CHECK (LapseCount >= 0 AND LapseCount <= ReviewCount),
                        PRIMARY KEY (MemberId, FormCode),
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
);
```

### ReviewPlanChannels

```sql
CREATE TABLE ReviewPlanChannels (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        PRIMARY KEY (Language, Channel),
                        UNIQUE (Language, SortOrder),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE
);
```

### ReviewPlanForms

```sql
CREATE TABLE ReviewPlanForms (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        FormCode TEXT NOT NULL,
                        PRIMARY KEY (Language, FormCode),
                        UNIQUE (Language, SortOrder),
                        CHECK ((Language = 'Japanese' AND FormCode IN ('Dictionary', 'Polite', 'Te', 'Ta'))
                            OR (Language = 'English' AND FormCode IN ('Base', 'PresentFirstPersonSingular', 'PresentNonThirdPerson',
                                'ThirdPersonSingular', 'Past', 'PastParticiple', 'PresentParticiple', 'Plural', 'Comparative', 'Superlative'))),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE
);
```

### ReviewPlanGroups

```sql
CREATE TABLE ReviewPlanGroups (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        WordGroupId TEXT NOT NULL,
                        PRIMARY KEY (Language, WordGroupId),
                        UNIQUE (Language, SortOrder),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
);
```

### ReviewPlanOutputs

```sql
CREATE TABLE ReviewPlanOutputs (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        OutputMode TEXT NOT NULL,
                        PRIMARY KEY (Language, OutputMode),
                        UNIQUE (Language, SortOrder),
                        CHECK ((Language = 'Japanese' AND OutputMode IN ('Kana', 'Kanji')) OR (Language = 'English' AND OutputMode = 'Latin')),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE
);
```

### ReviewPlans

```sql
CREATE TABLE ReviewPlans (
                        Language TEXT NOT NULL PRIMARY KEY CHECK (Language IN ('Japanese', 'English')),
                        DailyReviewTarget INTEGER NOT NULL CHECK (DailyReviewTarget BETWEEN 1 AND 500),
                        SessionWordLimit INTEGER NOT NULL CHECK (SessionWordLimit BETWEEN 1 AND 500),
                        RangeMode TEXT NOT NULL CHECK (RangeMode IN ('All', 'Selected')),
                        EnglishFormLabelMode TEXT NOT NULL CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')),
                        EnglishExampleSourceSenseId TEXT,
                        UpdatedAtUtc TEXT NOT NULL,
                        CHECK (Language = 'English' OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)),
                        CHECK ((EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)
                            OR (EnglishFormLabelMode = 'ExampleParadigm' AND EnglishExampleSourceSenseId IS NOT NULL AND LENGTH(TRIM(EnglishExampleSourceSenseId)) > 0))
);
```

### ReviewQuestions

```sql
CREATE TABLE ReviewQuestions (
                        QuestionId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')),
                        QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal >= 0),
                        Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Answered')),
                        CreatedAtUtc TEXT NOT NULL,
                        SubmittedText TEXT NULL,
                        WasCorrect INTEGER NULL CHECK (WasCorrect IN (0, 1)),
                        AnsweredAtUtc TEXT NULL,
                        UNIQUE (SessionId, QuestionOrdinal),
                        FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE
);
```

### ReviewSessionForms

```sql
CREATE TABLE ReviewSessionForms (
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL CHECK (length(trim(FormCode)) > 0),
                        PRIMARY KEY (SessionId, MemberId, FormCode),
                        FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
);
```

后续迁移追加的字段定义：

- `CorrectStreak INTEGER NOT NULL DEFAULT 0 CHECK (CorrectStreak BETWEEN 0 AND 2)`
- `IsMastered INTEGER NOT NULL DEFAULT 0 CHECK (IsMastered IN (0, 1))`
- `NextEligibleOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (NextEligibleOrdinal >= 0)`
- `MeaningWritingCount INTEGER NOT NULL DEFAULT 0 CHECK (MeaningWritingCount >= 0)`
- `DictationCount INTEGER NOT NULL DEFAULT 0 CHECK (DictationCount >= 0)`
- `LatinCount INTEGER NOT NULL DEFAULT 0 CHECK (LatinCount >= 0)`
- `KanaCount INTEGER NOT NULL DEFAULT 0 CHECK (KanaCount >= 0)`
- `KanjiCount INTEGER NOT NULL DEFAULT 0 CHECK (KanjiCount >= 0)`
- `CorrectCount INTEGER NOT NULL DEFAULT 0 CHECK (CorrectCount >= 0)`
- `ErrorCount INTEGER NOT NULL DEFAULT 0 CHECK (ErrorCount >= 0)`

### ReviewSessionGroups

```sql
CREATE TABLE ReviewSessionGroups (
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        CompletedLocalDate TEXT NULL,
                        PRIMARY KEY (SessionId, WordGroupId),
                        UNIQUE (SessionId, SortOrder),
                        FOREIGN KEY (SessionId) REFERENCES ReviewSessions (SessionId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
);
```

### ReviewSessions

```sql
CREATE TABLE ReviewSessions (
                        SessionId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        Status TEXT NOT NULL CHECK (Status IN ('InProgress', 'Completed')),
                        PlanJson TEXT NOT NULL CHECK (json_valid(PlanJson)),
                        CreatedAtUtc TEXT NOT NULL,
                        StartedLocalDate TEXT NOT NULL,
                        CompletedAtUtc TEXT NULL
);
```

后续迁移追加的字段定义：

- `CurrentOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (CurrentOrdinal >= 0)`

### SchemaMigrations

```sql
CREATE TABLE SchemaMigrations (Version INTEGER NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAtUtc TEXT NOT NULL);
```

### WordGroupForms

```sql
CREATE TABLE WordGroupForms (
                        MemberId INTEGER NOT NULL,
                        FormIndex INTEGER NOT NULL,
                        Surface TEXT NOT NULL,
                        Reading TEXT NULL,
                        FormCode TEXT NOT NULL CHECK (FormCode IN (
                            'Dictionary', 'Polite', 'Te', 'Ta',
                            'Base', 'PresentFirstPersonSingular', 'PresentNonThirdPerson',
                            'ThirdPersonSingular', 'Past', 'PastParticiple', 'PresentParticiple',
                            'Plural', 'Comparative', 'Superlative')),
                        ScriptCode TEXT NULL CHECK (ScriptCode IS NULL OR ScriptCode IN ('Kanji', 'Kana')),
                        RoleCode TEXT NULL CHECK (RoleCode IS NULL OR RoleCode IN ('Noun', 'Verb', 'Adjective', 'Adverb', 'Other')),
                        FormLabel TEXT NOT NULL,
                        DetailLabel TEXT NOT NULL,
                        IsCommon INTEGER NOT NULL CHECK (IsCommon IN (0, 1)),
                        IsPrimary INTEGER NOT NULL CHECK (IsPrimary IN (0, 1)),
                        PRIMARY KEY (MemberId, FormIndex),
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
);
```

### WordGroupMeanings

```sql
CREATE TABLE WordGroupMeanings (
                        MemberId INTEGER NOT NULL,
                        MeaningIndex INTEGER NOT NULL,
                        Text TEXT NOT NULL,
                        PRIMARY KEY (MemberId, MeaningIndex),
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
);
```

### WordGroupMembers

```sql
CREATE TABLE WordGroupMembers (
                        MemberId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        WordGroupId TEXT NOT NULL,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        SourceSenseId TEXT NOT NULL,
                        SourceLexemeId TEXT NOT NULL,
                        Headword TEXT NOT NULL,
                        Reading TEXT NULL,
                        PartOfSpeechLabel TEXT NOT NULL,
                        AttributeLabel TEXT NOT NULL,
                        IsPhrase INTEGER NOT NULL CHECK (IsPhrase IN (0, 1)),
                        SortOrder INTEGER NOT NULL,
                        UNIQUE (Language, SourceSenseId),
                        UNIQUE (WordGroupId, SortOrder),
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
);
```

后续迁移追加的字段定义：

- `MeaningLanguage TEXT NOT NULL DEFAULT 'und'`
- `MeaningSource TEXT NOT NULL DEFAULT 'Legacy'`
- `MeaningPrecision TEXT NOT NULL DEFAULT 'Missing'`
- `KanjiUsageCode TEXT NULL CHECK ( (Language = 'English' AND KanjiUsageCode IS NULL) OR (Language = 'Japanese' AND KanjiUsageCode IN ('None', 'Common', 'Rare')) )`

### WordGroups

```sql
CREATE TABLE WordGroups (
                        WordGroupId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        AnchorSourceSenseId TEXT NOT NULL,
                        Placement TEXT NOT NULL CHECK (Placement IN ('Inbox', 'Learning', 'Review')),
                        CreatedAtUtc TEXT NOT NULL
);
```

## Explicit Indices

以下索引直接来自 Schema Migration 定义；主键、UNIQUE 列组合也会形成相应约束或 SQLite 隐式索引。

- `CREATE INDEX IX_ImportBatchItems_BatchDecision ON ImportBatchItems (BatchId, Decision);`
- `CREATE INDEX IX_ImportBatchItems_WordGroupId ON ImportBatchItems (WordGroupId);`
- `CREATE INDEX IX_ImportBatches_StatusLanguage ON ImportBatches (Status, Language);`
- `CREATE INDEX IX_KanaSessionItems_CompletedDate ON KanaSessionItems(CompletedLocalDate);`
- `CREATE INDEX IX_LearningCompletionDates_LocalDate ON LearningCompletionDates(LocalDate);`
- `CREATE INDEX IX_ReviewFormMemories_WordGroupId ON ReviewFormMemories (WordGroupId);`
- `CREATE INDEX IX_ReviewPlanGroups_WordGroupId ON ReviewPlanGroups (WordGroupId);`
- `CREATE INDEX IX_ReviewSessionGroups_WordGroupId ON ReviewSessionGroups (WordGroupId);`
- `CREATE UNIQUE INDEX UX_KanaSessions_Active ON KanaSessions(Status) WHERE Status='Active';`
- `CREATE UNIQUE INDEX UX_LearningQuestions_PendingSession ON LearningQuestions (SessionId) WHERE Status = 'Pending';`
- `CREATE UNIQUE INDEX UX_LearningSessions_ActiveLanguage ON LearningSessions (Language) WHERE Status = 'InProgress';`
- `CREATE UNIQUE INDEX UX_ReviewQuestions_Pending ON ReviewQuestions (SessionId) WHERE Status='Pending';`
- `CREATE UNIQUE INDEX UX_ReviewSessions_ActiveLanguage ON ReviewSessions (Language) WHERE Status = 'InProgress';`

## Schema Changes and Source of Truth

- `WordGroupForms` 在迁移中由新表重建并替换，以上使用替换后的定义；历史中间表 `WordGroupForms_New` 不是最终的独立表。
- ALTER 添加列的定义独立列出，以避免将后续字段误认成最初建表时的字段；实际数据库结构由连续执行迁移确定。
- 静态词库文件与 LexiconBuilder 产生的数据不得按用户数据库的 Migration 臆造表结构。分析相关持久化行为时，应检查真实的词库库文件、构建器和数据访问代码。
- 业务表结构的变动必须先更新权威代码/迁移并通过测试，再同步此架构描述；不允许只改文档来“实现”数据库变化。
