# Architecture and Verified Target Structure

> 项目架构、数据库结构以当前仓库源代码和正式迁移定义为准；项目程序版本由 `ProjectInfo.json` 管理。

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

本节记录 SQLite 用户数据库最终有效的字段、类型和约束。数据库定义以实际迁移执行结果为准。

**迁移定义来源**：`Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`；`Dictation.DataAccess/Kana/KanaSchemaMigration.cs`；`Dictation.DataAccess/Statistics/StatisticsSchemaMigration.cs`；`Dictation.DataAccess/Statistics/LearningCompletionHistoryMigration.cs`。

### DailyPlanSnapshots

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese','English')) | — |
| LocalDate | TEXT | 否 | — | — | — |
| LearningTarget | INTEGER | 否 | — | CHECK (LearningTarget>=0) | — |
| ReviewTarget | INTEGER | 否 | — | CHECK (ReviewTarget>=0) | — |
| KanaTarget | INTEGER | 否 | — | CHECK (KanaTarget>=0 AND (Language='Japanese' OR KanaTarget=0)) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language,LocalDate) |


### ImportBatchItems

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| ItemId | INTEGER | 否 | — | PRIMARY KEY | — |
| BatchId | TEXT | 否 | — | — | — |
| SourceText | TEXT | 否 | — | — | — |
| AnchorSourceSenseId | TEXT | 否 | — | — | — |
| WordGroupId | TEXT | 是 | — | — | — |
| Decision | TEXT | 否 | — | CHECK (Decision IN ('Pending', 'Duplicate', 'Learning', 'Review', 'Discard')) | — |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (BatchId) REFERENCES ImportBatches (BatchId) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE SET NULL |


### ImportBatches

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| BatchId | TEXT | 否 | — | PRIMARY KEY | — |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | — |
| SourceKind | TEXT | 否 | — | CHECK (SourceKind IN ('Manual', 'Text', 'Mcp')) | — |
| Status | TEXT | 否 | — | CHECK (Status IN ('Pending', 'Completed')) | — |
| CreatedAtUtc | TEXT | 否 | — | — | — |
| CompletedAtUtc | TEXT | 是 | — | — | — |


### KanaAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | — |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal>=0) | — |
| KanaId | TEXT | 否 | — | — | — |
| SubmittedText | TEXT | 否 | — | — | — |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0,1)) | — |
| AnsweredAtUtc | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId,QuestionOrdinal) |
| FOREIGN KEY (SessionId,KanaId) REFERENCES KanaSessionItems(SessionId,KanaId) ON DELETE CASCADE |


### KanaMemories

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| KanaId | TEXT | 是 | — | PRIMARY KEY | — |
| Stability | REAL | 否 | — | CHECK (Stability>0) | — |
| Difficulty | REAL | 否 | — | CHECK (Difficulty BETWEEN 1 AND 10) | — |
| LastReviewedAtUtc | TEXT | 否 | — | — | — |
| DueAtUtc | TEXT | 否 | — | — | — |
| ReviewCount | INTEGER | 否 | — | CHECK (ReviewCount>=1) | — |
| LapseCount | INTEGER | 否 | — | CHECK (LapseCount>=0) | — |


### KanaPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Id | INTEGER | 是 | — | PRIMARY KEY; CHECK (Id=1) | — |
| Enabled | INTEGER | 否 | — | CHECK (Enabled IN (0,1)) | — |
| Hiragana | INTEGER | 否 | — | CHECK (Hiragana IN (0,1)) | — |
| Katakana | INTEGER | 否 | — | CHECK (Katakana IN (0,1)) | — |
| Clear | INTEGER | 否 | — | CHECK (Clear IN (0,1)) | — |
| Voiced | INTEGER | 否 | — | CHECK (Voiced IN (0,1)) | — |
| Contracted | INTEGER | 否 | — | CHECK (Contracted IN (0,1)) | — |
| ReviewAll | INTEGER | 否 | — | CHECK (ReviewAll IN (0,1)) | — |
| DailyTarget | INTEGER | 否 | — | CHECK (DailyTarget BETWEEN 1 AND 214) | — |

**表级约束**

| 约束定义 |
|---|
| CHECK (Hiragana + Katakana >= 1) |
| CHECK (Clear + Voiced + Contracted >= 1) |


### KanaSessionItems

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | — |
| KanaId | TEXT | 否 | — | — | — |
| Text | TEXT | 否 | — | — | — |
| Reading | TEXT | 否 | — | — | — |
| RowHint | TEXT | 否 | — | — | — |
| Script | TEXT | 否 | — | CHECK (Script IN ('Hiragana','Katakana')) | — |
| Range | TEXT | 否 | — | CHECK (Range IN ('Clear','Voiced','Contracted')) | — |
| QueueOrder | INTEGER | 否 | — | CHECK (QueueOrder>=0) | — |
| HasWrong | INTEGER | 否 | 0 | CHECK (HasWrong IN (0,1)) | — |
| CorrectStreak | INTEGER | 否 | 0 | CHECK (CorrectStreak>=0) | — |
| NextEligibleOrdinal | INTEGER | 否 | 0 | CHECK (NextEligibleOrdinal>=0) | — |
| IsCompleted | INTEGER | 否 | 0 | CHECK (IsCompleted IN (0,1)) | — |
| CompletedLocalDate | TEXT | 是 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId,KanaId) |
| FOREIGN KEY (SessionId) REFERENCES KanaSessions(SessionId) ON DELETE CASCADE |
| CHECK ((IsCompleted=0 AND CompletedLocalDate IS NULL) OR (IsCompleted=1 AND CompletedLocalDate IS NOT NULL)) |


### KanaSessions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 是 | — | PRIMARY KEY | — |
| StartedLocalDate | TEXT | 否 | — | — | — |
| Status | TEXT | 否 | — | CHECK (Status IN ('Active','Completed')) | — |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal>=0) | — |
| CurrentKanaId | TEXT | 是 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| CHECK ((Status='Active' AND CurrentKanaId IS NOT NULL) OR (Status='Completed' AND CurrentKanaId IS NULL)) |


### LearningCompletionDates

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | — |
| LocalDate | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (WordGroupId,LocalDate) |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE |


### LearningFormStates

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | — |
| MemberId | INTEGER | 否 | — | — | — |
| FormCode | TEXT | 否 | — | — | — |
| CorrectStreak | INTEGER | 否 | 0 | CHECK (CorrectStreak BETWEEN 0 AND 2) | — |
| IsMastered | INTEGER | 否 | 0 | CHECK (IsMastered IN (0, 1)) | — |
| NextEligibleOrdinal | INTEGER | 否 | 0 | CHECK (NextEligibleOrdinal >= 0) | — |
| MeaningWritingCount | INTEGER | 否 | 0 | CHECK (MeaningWritingCount >= 0) | — |
| DictationCount | INTEGER | 否 | 0 | CHECK (DictationCount >= 0) | — |
| LatinCount | INTEGER | 否 | 0 | CHECK (LatinCount >= 0) | — |
| KanaCount | INTEGER | 否 | 0 | CHECK (KanaCount >= 0) | — |
| KanjiCount | INTEGER | 否 | 0 | CHECK (KanjiCount >= 0) | — |
| CorrectCount | INTEGER | 否 | 0 | CHECK (CorrectCount >= 0) | — |
| ErrorCount | INTEGER | 否 | 0 | CHECK (ErrorCount >= 0) | — |
| LastAnsweredAtUtc | TEXT | 是 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (WordGroupId, MemberId, FormCode) |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### LearningGroupProgress

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | PRIMARY KEY | — |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | — |
| FirstStartedLocalDate | TEXT | 否 | — | — | — |
| CompletedLocalDate | TEXT | 是 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |


### LearningPlanChannels

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, SortOrder) |
| UNIQUE (Language, Channel) |
| FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE |


### LearningPlanForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| FormCode | TEXT | 否 | — | CHECK (length(trim(FormCode)) > 0) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, SortOrder) |
| UNIQUE (Language, FormCode) |
| FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE |


### LearningPlanOutputs

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, SortOrder) |
| UNIQUE (Language, OutputMode) |
| FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE |


### LearningPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | PRIMARY KEY; CHECK (Language IN ('Japanese', 'English')) | — |
| DailyNewTarget | INTEGER | 否 | — | CHECK (DailyNewTarget BETWEEN 1 AND 500) | — |
| SessionWordLimit | INTEGER | 否 | — | CHECK (SessionWordLimit BETWEEN 1 AND 500) | — |
| SelectionMode | TEXT | 否 | — | CHECK (SelectionMode IN ('Random', 'Sequential', 'Manual')) | — |
| EnglishFormLabelMode | TEXT | 否 | — | CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')) | — |
| EnglishExampleSourceSenseId | TEXT | 是 | — | — | — |
| UpdatedAtUtc | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| CHECK ( Language = 'English' OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL) ) |


### LearningQuestionAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| AttemptId | INTEGER | 否 | — | PRIMARY KEY | — |
| QuestionId | INTEGER | 否 | — | UNIQUE | — |
| SubmittedText | TEXT | 否 | — | — | — |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0, 1)) | — |
| AnsweredAtUtc | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (QuestionId) REFERENCES LearningQuestions (QuestionId) ON DELETE CASCADE |


### LearningQuestions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| QuestionId | INTEGER | 否 | — | PRIMARY KEY | — |
| SessionId | TEXT | 否 | — | — | — |
| WordGroupId | TEXT | 否 | — | — | — |
| MemberId | INTEGER | 否 | — | — | — |
| FormCode | TEXT | 否 | — | — | — |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | — |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')) | — |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | — |
| Status | TEXT | 否 | — | CHECK (Status IN ('Pending', 'Answered')) | — |
| CreatedAtUtc | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (SessionId, QuestionOrdinal) |
| FOREIGN KEY (SessionId) REFERENCES LearningSessions (SessionId) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### LearningSessionGroups

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | — |
| WordGroupId | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId, WordGroupId) |
| UNIQUE (SessionId, SortOrder) |
| FOREIGN KEY (SessionId) REFERENCES LearningSessions (SessionId) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |


### LearningSessions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | PRIMARY KEY | — |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | — |
| Status | TEXT | 否 | — | CHECK (Status IN ('InProgress', 'Completed')) | — |
| CurrentOrdinal | INTEGER | 否 | 0 | CHECK (CurrentOrdinal >= 0) | — |
| CreatedAtUtc | TEXT | 否 | — | — | — |
| CompletedAtUtc | TEXT | 是 | — | — | — |


### LearningSharedAnswerAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | — |
| QuestionId | INTEGER | 否 | — | — | — |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | — |
| WordGroupId | TEXT | 否 | — | — | — |
| MemberId | INTEGER | 否 | — | — | — |
| FormCode | TEXT | 否 | — | — | — |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting','Dictation')) | — |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin','Kana','Kanji')) | — |
| SubmittedText | TEXT | 否 | — | — | — |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0,1)) | — |
| AnsweredAtUtc | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId, QuestionOrdinal, MemberId, FormCode) |
| FOREIGN KEY (SessionId, WordGroupId) REFERENCES LearningSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId, MemberId, FormCode) REFERENCES LearningFormStates (WordGroupId, MemberId, FormCode) ON DELETE CASCADE |


### ReviewAnswerAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | — |
| QuestionId | INTEGER | 否 | — | — | — |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | — |
| WordGroupId | TEXT | 否 | — | — | — |
| MemberId | INTEGER | 否 | — | — | — |
| FormCode | TEXT | 否 | — | — | — |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting','Dictation')) | — |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin','Kana','Kanji')) | — |
| SubmittedText | TEXT | 否 | — | — | — |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0,1)) | — |
| AnsweredAtUtc | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId, QuestionOrdinal, MemberId, FormCode) |
| FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE |


### ReviewFormMemories

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | — |
| MemberId | INTEGER | 否 | — | — | — |
| FormCode | TEXT | 否 | — | — | — |
| Stability | REAL | 否 | — | CHECK (Stability > 0) | — |
| Difficulty | REAL | 否 | — | CHECK (Difficulty BETWEEN 1 AND 10) | — |
| LastReviewedAtUtc | TEXT | 否 | — | — | — |
| DueAtUtc | TEXT | 否 | — | CHECK (DueAtUtc > LastReviewedAtUtc) | — |
| ReviewCount | INTEGER | 否 | — | CHECK (ReviewCount >= 1) | — |
| LapseCount | INTEGER | 否 | — | CHECK (LapseCount >= 0 AND LapseCount <= ReviewCount) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (MemberId, FormCode) |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### ReviewPlanChannels

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, Channel) |
| UNIQUE (Language, SortOrder) |
| FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE |


### ReviewPlanForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| FormCode | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, FormCode) |
| UNIQUE (Language, SortOrder) |
| CHECK ((Language = 'Japanese' AND FormCode IN ('Dictionary', 'Polite', 'Te', 'Ta')) OR (Language = 'English' AND FormCode IN ('Base', 'PresentFirstPersonSingular', 'PresentNonThirdPerson', 'ThirdPersonSingular', 'Past', 'PastParticiple', 'PresentParticiple', 'Plural', 'Comparative', 'Superlative'))) |
| FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE |


### ReviewPlanGroups

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| WordGroupId | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, WordGroupId) |
| UNIQUE (Language, SortOrder) |
| FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |


### ReviewPlanOutputs

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| OutputMode | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, OutputMode) |
| UNIQUE (Language, SortOrder) |
| CHECK ((Language = 'Japanese' AND OutputMode IN ('Kana', 'Kanji')) OR (Language = 'English' AND OutputMode = 'Latin')) |
| FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE |


### ReviewPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | PRIMARY KEY; CHECK (Language IN ('Japanese', 'English')) | — |
| DailyReviewTarget | INTEGER | 否 | — | CHECK (DailyReviewTarget BETWEEN 1 AND 500) | — |
| SessionWordLimit | INTEGER | 否 | — | CHECK (SessionWordLimit BETWEEN 1 AND 500) | — |
| RangeMode | TEXT | 否 | — | CHECK (RangeMode IN ('All', 'Selected')) | — |
| EnglishFormLabelMode | TEXT | 否 | — | CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')) | — |
| EnglishExampleSourceSenseId | TEXT | 是 | — | — | — |
| UpdatedAtUtc | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| CHECK (Language = 'English' OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)) |
| CHECK ((EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL) OR (EnglishFormLabelMode = 'ExampleParadigm' AND EnglishExampleSourceSenseId IS NOT NULL AND LENGTH(TRIM(EnglishExampleSourceSenseId)) > 0)) |


### ReviewQuestions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| QuestionId | INTEGER | 是 | — | PRIMARY KEY | — |
| SessionId | TEXT | 否 | — | — | — |
| WordGroupId | TEXT | 否 | — | — | — |
| MemberId | INTEGER | 否 | — | — | — |
| FormCode | TEXT | 否 | — | — | — |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | — |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')) | — |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | — |
| Status | TEXT | 否 | — | CHECK (Status IN ('Pending', 'Answered')) | — |
| CreatedAtUtc | TEXT | 否 | — | — | — |
| SubmittedText | TEXT | 是 | — | — | — |
| WasCorrect | INTEGER | 是 | — | CHECK (WasCorrect IN (0, 1)) | — |
| AnsweredAtUtc | TEXT | 是 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (SessionId, QuestionOrdinal) |
| FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE |


### ReviewSessionForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | — |
| WordGroupId | TEXT | 否 | — | — | — |
| MemberId | INTEGER | 否 | — | — | — |
| FormCode | TEXT | 否 | — | CHECK (length(trim(FormCode)) > 0) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId, MemberId, FormCode) |
| FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


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

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | — |
| WordGroupId | TEXT | 否 | — | — | — |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | — |
| CompletedLocalDate | TEXT | 是 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId, WordGroupId) |
| UNIQUE (SessionId, SortOrder) |
| FOREIGN KEY (SessionId) REFERENCES ReviewSessions (SessionId) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |


### ReviewSessions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | PRIMARY KEY | — |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | — |
| Status | TEXT | 否 | — | CHECK (Status IN ('InProgress', 'Completed')) | — |
| PlanJson | TEXT | 否 | — | CHECK (json_valid(PlanJson)) | — |
| CreatedAtUtc | TEXT | 否 | — | — | — |
| StartedLocalDate | TEXT | 否 | — | — | — |
| CompletedAtUtc | TEXT | 是 | — | — | — |


后续迁移追加的字段定义：

- `CurrentOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (CurrentOrdinal >= 0)`

### SchemaMigrations

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Version | INTEGER | 否 | — | PRIMARY KEY | — |
| Name | TEXT | 否 | — | — | — |
| AppliedAtUtc | TEXT | 否 | — | — | — |


### WordGroupForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | — | — |
| FormIndex | INTEGER | 否 | — | — | — |
| Surface | TEXT | 否 | — | — | — |
| Reading | TEXT | 是 | — | — | — |
| FormCode | TEXT | 否 | — | CHECK (FormCode IN ( 'Dictionary', 'Polite', 'Te', 'Ta', 'Base', 'PresentFirstPersonSingular', 'PresentNonThirdPerson', 'ThirdPersonSingular', 'Past', 'PastParticiple', 'PresentParticiple', 'Plural', 'Comparative', 'Superlative')) | — |
| ScriptCode | TEXT | 是 | — | CHECK (ScriptCode IS NULL OR ScriptCode IN ('Kanji', 'Kana')) | — |
| RoleCode | TEXT | 是 | — | CHECK (RoleCode IS NULL OR RoleCode IN ('Noun', 'Verb', 'Adjective', 'Adverb', 'Other')) | — |
| FormLabel | TEXT | 否 | — | — | — |
| DetailLabel | TEXT | 否 | — | — | — |
| IsCommon | INTEGER | 否 | — | CHECK (IsCommon IN (0, 1)) | — |
| IsPrimary | INTEGER | 否 | — | CHECK (IsPrimary IN (0, 1)) | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (MemberId, FormIndex) |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### WordGroupMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | — | — |
| MeaningIndex | INTEGER | 否 | — | — | — |
| Text | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (MemberId, MeaningIndex) |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### WordGroupMembers

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | PRIMARY KEY | — |
| WordGroupId | TEXT | 否 | — | — | — |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | — |
| SourceSenseId | TEXT | 否 | — | — | — |
| SourceLexemeId | TEXT | 否 | — | — | — |
| Headword | TEXT | 否 | — | — | — |
| Reading | TEXT | 是 | — | — | — |
| PartOfSpeechLabel | TEXT | 否 | — | — | — |
| AttributeLabel | TEXT | 否 | — | — | — |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | — |
| SortOrder | INTEGER | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (Language, SourceSenseId) |
| UNIQUE (WordGroupId, SortOrder) |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |


后续迁移追加的字段定义：

- `MeaningLanguage TEXT NOT NULL DEFAULT 'und'`
- `MeaningSource TEXT NOT NULL DEFAULT 'Legacy'`
- `MeaningPrecision TEXT NOT NULL DEFAULT 'Missing'`
- `KanjiUsageCode TEXT NULL CHECK ( (Language = 'English' AND KanjiUsageCode IS NULL) OR (Language = 'Japanese' AND KanjiUsageCode IN ('None', 'Common', 'Rare')) )`

### WordGroups

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | PRIMARY KEY | — |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | — |
| AnchorSourceSenseId | TEXT | 否 | — | — | — |
| Placement | TEXT | 否 | — | CHECK (Placement IN ('Inbox', 'Learning', 'Review')) | — |
| CreatedAtUtc | TEXT | 否 | — | — | — |


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
| Key | TEXT | 否 | — | PRIMARY KEY | — |
| Value | TEXT | 否 | — | — | — |

### Entries

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | PRIMARY KEY | — |
| SourceSequence | INTEGER | 否 | — | — | — |
| SenseIndex | INTEGER | 否 | — | — | — |
| PrimaryWritten | TEXT | 否 | — | — | — |
| PrimaryReading | TEXT | 否 | — | — | — |
| LexicalCategory | TEXT | 否 | — | — | — |
| VerbClass | TEXT | 否 | — | — | — |
| Transitivity | TEXT | 否 | — | — | — |
| KanjiUsage | TEXT | 否 | — | CHECK (KanjiUsage IN ('None', 'Common', 'Rare')) | — |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | — |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (SourceSequence, SenseIndex) |

### PartsOfSpeech

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | — |
| SortIndex | INTEGER | 否 | — | — | — |
| Value | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (EntryId, SortIndex) |
| FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE |

### SourceGlosses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | — |
| Language | TEXT | 否 | — | — | — |
| GlossIndex | INTEGER | 否 | — | — | — |
| GlossType | TEXT | 是 | — | — | — |
| Text | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (EntryId, Language, GlossIndex) |
| FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE |

### ChineseMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | — |
| MeaningIndex | INTEGER | 否 | — | — | — |
| Text | TEXT | 否 | — | — | — |
| MeaningSource | TEXT | 否 | — | CHECK (MeaningSource = 'Tomoshi') | — |
| MeaningPrecision | TEXT | 否 | — | CHECK (MeaningPrecision = 'Exact') | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (EntryId, MeaningIndex) |
| FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE |

### Forms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| FormId | INTEGER | 否 | — | PRIMARY KEY | — |
| EntryId | TEXT | 否 | — | — | — |
| Surface | TEXT | 否 | — | — | — |
| Reading | TEXT | 否 | — | — | — |
| FormKind | TEXT | 否 | — | — | — |
| ScriptKind | TEXT | 否 | — | — | — |
| LexicalRole | TEXT | 否 | — | — | — |
| IsCommon | INTEGER | 否 | — | CHECK (IsCommon IN (0, 1)) | — |
| IsPrimary | INTEGER | 否 | — | CHECK (IsPrimary IN (0, 1)) | — |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole) |
| FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE |

## 英语只读词库

数据定义来源：`Dictation.LexiconBuilder/WordNet/EnglishLexiconDatabaseBuilder.cs`。

### BuildMetadata

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Key | TEXT | 否 | — | PRIMARY KEY | — |
| Value | TEXT | 否 | — | — | — |

### Lexemes

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LexemeId | TEXT | 否 | — | PRIMARY KEY | — |
| Lemma | TEXT | 否 | — | — | — |
| SourceEntryKey | TEXT | 否 | — | — | — |
| PartOfSpeech | TEXT | 否 | — | — | — |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | — |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (Lemma, SourceEntryKey) |

### Synsets

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | PRIMARY KEY | — |
| PartOfSpeech | TEXT | 否 | — | — | — |
| Ili | TEXT | 是 | — | — | — |

### Senses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SenseId | TEXT | 否 | — | PRIMARY KEY | — |
| LexemeId | TEXT | 否 | — | — | — |
| SynsetId | TEXT | 否 | — | — | — |
| SenseOrder | INTEGER | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (LexemeId) REFERENCES Lexemes (LexemeId) |
| FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId) |

### Definitions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | — | — |
| DefinitionIndex | INTEGER | 否 | — | — | — |
| Text | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SynsetId, DefinitionIndex) |
| FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId) |

### Examples

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | — | — |
| ExampleIndex | INTEGER | 否 | — | — | — |
| Text | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SynsetId, ExampleIndex) |
| FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId) |

### Forms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| FormId | INTEGER | 否 | — | PRIMARY KEY | — |
| LexemeId | TEXT | 否 | — | — | — |
| Surface | TEXT | 否 | — | — | — |
| FormKind | TEXT | 否 | — | — | — |
| FormSourceKind | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (LexemeId, Surface, FormKind) |
| FOREIGN KEY (LexemeId) REFERENCES Lexemes (LexemeId) |

### Derivations

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SourceSenseId | TEXT | 否 | — | — | — |
| TargetSenseId | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SourceSenseId, TargetSenseId) |
| FOREIGN KEY (SourceSenseId) REFERENCES Senses (SenseId) |
| FOREIGN KEY (TargetSenseId) REFERENCES Senses (SenseId) |

### LearningSenses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | PRIMARY KEY | — |
| SourceLexemeId | TEXT | 否 | — | — | — |
| Headword | TEXT | 否 | — | — | — |
| PartOfSpeech | TEXT | 否 | — | — | — |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | — |
| MeaningSource | TEXT | 否 | — | CHECK (MeaningSource IN ('ChineseOpenWordNet', 'Ecdict', 'Missing')) | — |
| MeaningPrecision | TEXT | 否 | — | CHECK (MeaningPrecision IN ('Exact', 'Broad', 'Missing')) | — |
| SortOrder | INTEGER | 否 | — | — | — |

### LearningSenseSources

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | — | — |
| SenseId | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (LearningSenseId, SenseId) |
| FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId) |
| FOREIGN KEY (SenseId) REFERENCES Senses (SenseId) |

### LearningMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | — | — |
| MeaningIndex | INTEGER | 否 | — | — | — |
| Text | TEXT | 否 | — | — | — |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (LearningSenseId, MeaningIndex) |
| FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId) |
