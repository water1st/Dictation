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
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese','English')) | 语言 |
| LocalDate | TEXT | 否 | — | — | 本地日期 |
| LearningTarget | INTEGER | 否 | — | CHECK (LearningTarget>=0) | 源 Schema 未提供字段注释 |
| ReviewTarget | INTEGER | 否 | — | CHECK (ReviewTarget>=0) | 源 Schema 未提供字段注释 |
| KanaTarget | INTEGER | 否 | — | CHECK (KanaTarget>=0 AND (Language='Japanese' OR KanaTarget=0)) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language,LocalDate) |


### ImportBatchItems

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| ItemId | INTEGER | 否 | — | PRIMARY KEY | 导入条目标识 |
| BatchId | TEXT | 否 | — | — | 导入批次标识 |
| SourceText | TEXT | 否 | — | — | 来源文本 |
| AnchorSourceSenseId | TEXT | 否 | — | — | 锚定词义标识 |
| WordGroupId | TEXT | 是 | — | — | 单词组标识 |
| Decision | TEXT | 否 | — | CHECK (Decision IN ('Pending', 'Duplicate', 'Learning', 'Review', 'Discard')) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (BatchId) REFERENCES ImportBatches (BatchId) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE SET NULL |


### ImportBatches

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| BatchId | TEXT | 否 | — | PRIMARY KEY | 导入批次标识 |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | 语言 |
| SourceKind | TEXT | 否 | — | CHECK (SourceKind IN ('Manual', 'Text', 'Mcp')) | 来源类型 |
| Status | TEXT | 否 | — | CHECK (Status IN ('Pending', 'Completed')) | 状态 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| CompletedAtUtc | TEXT | 是 | — | — | 完成时间（UTC） |


### KanaAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal>=0) | 题目序号 |
| KanaId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0,1)) | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId,QuestionOrdinal) |
| FOREIGN KEY (SessionId,KanaId) REFERENCES KanaSessionItems(SessionId,KanaId) ON DELETE CASCADE |


### KanaMemories

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| KanaId | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| Stability | REAL | 否 | — | CHECK (Stability>0) | 记忆稳定度 |
| Difficulty | REAL | 否 | — | CHECK (Difficulty BETWEEN 1 AND 10) | 难度 |
| LastReviewedAtUtc | TEXT | 否 | — | — | 最近复习时间（UTC） |
| DueAtUtc | TEXT | 否 | — | — | 到期时间（UTC） |
| ReviewCount | INTEGER | 否 | — | CHECK (ReviewCount>=1) | 复习次数 |
| LapseCount | INTEGER | 否 | — | CHECK (LapseCount>=0) | 遗忘次数 |


### KanaPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Id | INTEGER | 否 | — | PRIMARY KEY; CHECK (Id=1) | 记录标识 |
| Enabled | INTEGER | 否 | — | CHECK (Enabled IN (0,1)) | 启用标志 |
| Hiragana | INTEGER | 否 | — | CHECK (Hiragana IN (0,1)) | 源 Schema 未提供字段注释 |
| Katakana | INTEGER | 否 | — | CHECK (Katakana IN (0,1)) | 源 Schema 未提供字段注释 |
| Clear | INTEGER | 否 | — | CHECK (Clear IN (0,1)) | 源 Schema 未提供字段注释 |
| Voiced | INTEGER | 否 | — | CHECK (Voiced IN (0,1)) | 源 Schema 未提供字段注释 |
| Contracted | INTEGER | 否 | — | CHECK (Contracted IN (0,1)) | 源 Schema 未提供字段注释 |
| ReviewAll | INTEGER | 否 | — | CHECK (ReviewAll IN (0,1)) | 源 Schema 未提供字段注释 |
| DailyTarget | INTEGER | 否 | — | CHECK (DailyTarget BETWEEN 1 AND 214) | 每日目标数量 |

**表级约束**

| 约束定义 |
|---|
| CHECK (Hiragana + Katakana >= 1) |
| CHECK (Clear + Voiced + Contracted >= 1) |


### KanaSessionItems

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| KanaId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Text | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Reading | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| RowHint | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Script | TEXT | 否 | — | CHECK (Script IN ('Hiragana','Katakana')) | 源 Schema 未提供字段注释 |
| Range | TEXT | 否 | — | CHECK (Range IN ('Clear','Voiced','Contracted')) | 源 Schema 未提供字段注释 |
| QueueOrder | INTEGER | 否 | — | CHECK (QueueOrder>=0) | 源 Schema 未提供字段注释 |
| HasWrong | INTEGER | 否 | 0 | CHECK (HasWrong IN (0,1)) | 源 Schema 未提供字段注释 |
| CorrectStreak | INTEGER | 否 | 0 | CHECK (CorrectStreak>=0) | 源 Schema 未提供字段注释 |
| NextEligibleOrdinal | INTEGER | 否 | 0 | CHECK (NextEligibleOrdinal>=0) | 源 Schema 未提供字段注释 |
| IsCompleted | INTEGER | 否 | 0 | CHECK (IsCompleted IN (0,1)) | 完成标志 |
| CompletedLocalDate | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId,KanaId) |
| FOREIGN KEY (SessionId) REFERENCES KanaSessions(SessionId) ON DELETE CASCADE |
| CHECK ((IsCompleted=0 AND CompletedLocalDate IS NULL) OR (IsCompleted=1 AND CompletedLocalDate IS NOT NULL)) |


### KanaSessions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | PRIMARY KEY | 会话标识 |
| StartedLocalDate | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Status | TEXT | 否 | — | CHECK (Status IN ('Active','Completed')) | 状态 |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal>=0) | 题目序号 |
| CurrentKanaId | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| CHECK ((Status='Active' AND CurrentKanaId IS NOT NULL) OR (Status='Completed' AND CurrentKanaId IS NULL)) |


### LearningCompletionDates

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| LocalDate | TEXT | 否 | — | — | 本地日期 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (WordGroupId,LocalDate) |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups(WordGroupId) ON DELETE CASCADE |


### LearningFormStates

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| CorrectStreak | INTEGER | 否 | 0 | CHECK (CorrectStreak BETWEEN 0 AND 2) | 源 Schema 未提供字段注释 |
| IsMastered | INTEGER | 否 | 0 | CHECK (IsMastered IN (0, 1)) | 源 Schema 未提供字段注释 |
| NextEligibleOrdinal | INTEGER | 否 | 0 | CHECK (NextEligibleOrdinal >= 0) | 源 Schema 未提供字段注释 |
| MeaningWritingCount | INTEGER | 否 | 0 | CHECK (MeaningWritingCount >= 0) | 源 Schema 未提供字段注释 |
| DictationCount | INTEGER | 否 | 0 | CHECK (DictationCount >= 0) | 源 Schema 未提供字段注释 |
| LatinCount | INTEGER | 否 | 0 | CHECK (LatinCount >= 0) | 源 Schema 未提供字段注释 |
| KanaCount | INTEGER | 否 | 0 | CHECK (KanaCount >= 0) | 源 Schema 未提供字段注释 |
| KanjiCount | INTEGER | 否 | 0 | CHECK (KanjiCount >= 0) | 源 Schema 未提供字段注释 |
| CorrectCount | INTEGER | 否 | 0 | CHECK (CorrectCount >= 0) | 源 Schema 未提供字段注释 |
| ErrorCount | INTEGER | 否 | 0 | CHECK (ErrorCount >= 0) | 源 Schema 未提供字段注释 |
| LastAnsweredAtUtc | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (WordGroupId, MemberId, FormCode) |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### LearningGroupProgress

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | PRIMARY KEY | 单词组标识 |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | 语言 |
| FirstStartedLocalDate | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| CompletedLocalDate | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |


### LearningPlanChannels

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, SortOrder) |
| UNIQUE (Language, Channel) |
| FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE |


### LearningPlanForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | CHECK (length(trim(FormCode)) > 0) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, SortOrder) |
| UNIQUE (Language, FormCode) |
| FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE |


### LearningPlanOutputs

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, SortOrder) |
| UNIQUE (Language, OutputMode) |
| FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE |


### LearningPlans

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | PRIMARY KEY; CHECK (Language IN ('Japanese', 'English')) | 语言 |
| DailyNewTarget | INTEGER | 否 | — | CHECK (DailyNewTarget BETWEEN 1 AND 500) | 源 Schema 未提供字段注释 |
| SessionWordLimit | INTEGER | 否 | — | CHECK (SessionWordLimit BETWEEN 1 AND 500) | 源 Schema 未提供字段注释 |
| SelectionMode | TEXT | 否 | — | CHECK (SelectionMode IN ('Random', 'Sequential', 'Manual')) | 源 Schema 未提供字段注释 |
| EnglishFormLabelMode | TEXT | 否 | — | CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')) | 源 Schema 未提供字段注释 |
| EnglishExampleSourceSenseId | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |
| UpdatedAtUtc | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| CHECK ( Language = 'English' OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL) ) |


### LearningQuestionAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| AttemptId | INTEGER | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| QuestionId | INTEGER | 否 | — | UNIQUE | 源 Schema 未提供字段注释 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0, 1)) | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (QuestionId) REFERENCES LearningQuestions (QuestionId) ON DELETE CASCADE |


### LearningQuestions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| QuestionId | INTEGER | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | 源 Schema 未提供字段注释 |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')) | 源 Schema 未提供字段注释 |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | 题目序号 |
| Status | TEXT | 否 | — | CHECK (Status IN ('Pending', 'Answered')) | 状态 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |

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
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |

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
| SessionId | TEXT | 否 | — | PRIMARY KEY | 会话标识 |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | 语言 |
| Status | TEXT | 否 | — | CHECK (Status IN ('InProgress', 'Completed')) | 状态 |
| CurrentOrdinal | INTEGER | 否 | 0 | CHECK (CurrentOrdinal >= 0) | 源 Schema 未提供字段注释 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| CompletedAtUtc | TEXT | 是 | — | — | 完成时间（UTC） |


### LearningSharedAnswerAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| QuestionId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | 题目序号 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting','Dictation')) | 源 Schema 未提供字段注释 |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin','Kana','Kanji')) | 源 Schema 未提供字段注释 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0,1)) | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId, QuestionOrdinal, MemberId, FormCode) |
| FOREIGN KEY (SessionId, WordGroupId) REFERENCES LearningSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (WordGroupId, MemberId, FormCode) REFERENCES LearningFormStates (WordGroupId, MemberId, FormCode) ON DELETE CASCADE |


### ReviewAnswerAttempts

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| QuestionId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | 题目序号 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting','Dictation')) | 源 Schema 未提供字段注释 |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin','Kana','Kanji')) | 源 Schema 未提供字段注释 |
| SubmittedText | TEXT | 否 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 否 | — | CHECK (WasCorrect IN (0,1)) | 答案正确标志 |
| AnsweredAtUtc | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SessionId, QuestionOrdinal, MemberId, FormCode) |
| FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE |


### ReviewFormMemories

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Stability | REAL | 否 | — | CHECK (Stability > 0) | 记忆稳定度 |
| Difficulty | REAL | 否 | — | CHECK (Difficulty BETWEEN 1 AND 10) | 难度 |
| LastReviewedAtUtc | TEXT | 否 | — | — | 最近复习时间（UTC） |
| DueAtUtc | TEXT | 否 | — | CHECK (DueAtUtc > LastReviewedAtUtc) | 到期时间（UTC） |
| ReviewCount | INTEGER | 否 | — | CHECK (ReviewCount >= 1) | 复习次数 |
| LapseCount | INTEGER | 否 | — | CHECK (LapseCount >= 0 AND LapseCount <= ReviewCount) | 遗忘次数 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (MemberId, FormCode) |
| FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### ReviewPlanChannels

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (Language, Channel) |
| UNIQUE (Language, SortOrder) |
| FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE |


### ReviewPlanForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

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
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |

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
| Language | TEXT | 否 | — | — | 语言 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| OutputMode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

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
| Language | TEXT | 否 | — | PRIMARY KEY; CHECK (Language IN ('Japanese', 'English')) | 语言 |
| DailyReviewTarget | INTEGER | 否 | — | CHECK (DailyReviewTarget BETWEEN 1 AND 500) | 源 Schema 未提供字段注释 |
| SessionWordLimit | INTEGER | 否 | — | CHECK (SessionWordLimit BETWEEN 1 AND 500) | 源 Schema 未提供字段注释 |
| RangeMode | TEXT | 否 | — | CHECK (RangeMode IN ('All', 'Selected')) | 源 Schema 未提供字段注释 |
| EnglishFormLabelMode | TEXT | 否 | — | CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')) | 源 Schema 未提供字段注释 |
| EnglishExampleSourceSenseId | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |
| UpdatedAtUtc | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| CHECK (Language = 'English' OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)) |
| CHECK ((EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL) OR (EnglishFormLabelMode = 'ExampleParadigm' AND EnglishExampleSourceSenseId IS NOT NULL AND LENGTH(TRIM(EnglishExampleSourceSenseId)) > 0)) |


### ReviewQuestions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| QuestionId | INTEGER | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Channel | TEXT | 否 | — | CHECK (Channel IN ('MeaningWriting', 'Dictation')) | 源 Schema 未提供字段注释 |
| OutputMode | TEXT | 否 | — | CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')) | 源 Schema 未提供字段注释 |
| QuestionOrdinal | INTEGER | 否 | — | CHECK (QuestionOrdinal >= 0) | 题目序号 |
| Status | TEXT | 否 | — | CHECK (Status IN ('Pending', 'Answered')) | 状态 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| SubmittedText | TEXT | 是 | — | — | 提交的答案 |
| WasCorrect | INTEGER | 是 | — | CHECK (WasCorrect IN (0, 1)) | 答案正确标志 |
| AnsweredAtUtc | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (SessionId, QuestionOrdinal) |
| FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE |
| FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE |


### ReviewSessionForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | CHECK (length(trim(FormCode)) > 0) | 源 Schema 未提供字段注释 |
| CorrectStreak | INTEGER | 否 | 0 | CHECK (CorrectStreak BETWEEN 0 AND 2) | 源 Schema 未提供字段注释 |
| IsMastered | INTEGER | 否 | 0 | CHECK (IsMastered IN (0, 1)) | 源 Schema 未提供字段注释 |
| NextEligibleOrdinal | INTEGER | 否 | 0 | CHECK (NextEligibleOrdinal >= 0) | 源 Schema 未提供字段注释 |
| MeaningWritingCount | INTEGER | 否 | 0 | CHECK (MeaningWritingCount >= 0) | 源 Schema 未提供字段注释 |
| DictationCount | INTEGER | 否 | 0 | CHECK (DictationCount >= 0) | 源 Schema 未提供字段注释 |
| LatinCount | INTEGER | 否 | 0 | CHECK (LatinCount >= 0) | 源 Schema 未提供字段注释 |
| KanaCount | INTEGER | 否 | 0 | CHECK (KanaCount >= 0) | 源 Schema 未提供字段注释 |
| KanjiCount | INTEGER | 否 | 0 | CHECK (KanjiCount >= 0) | 源 Schema 未提供字段注释 |
| CorrectCount | INTEGER | 否 | 0 | CHECK (CorrectCount >= 0) | 源 Schema 未提供字段注释 |
| ErrorCount | INTEGER | 否 | 0 | CHECK (ErrorCount >= 0) | 源 Schema 未提供字段注释 |

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
| SessionId | TEXT | 否 | — | — | 会话标识 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| SortOrder | INTEGER | 否 | — | CHECK (SortOrder >= 0) | 源 Schema 未提供字段注释 |
| CompletedLocalDate | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |

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
| SessionId | TEXT | 否 | — | PRIMARY KEY | 会话标识 |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | 语言 |
| Status | TEXT | 否 | — | CHECK (Status IN ('InProgress', 'Completed')) | 状态 |
| PlanJson | TEXT | 否 | — | CHECK (json_valid(PlanJson)) | 源 Schema 未提供字段注释 |
| CreatedAtUtc | TEXT | 否 | — | — | 创建时间（UTC） |
| StartedLocalDate | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| CompletedAtUtc | TEXT | 是 | — | — | 完成时间（UTC） |


后续迁移追加的字段定义：

- `CurrentOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (CurrentOrdinal >= 0)`
| CurrentOrdinal | INTEGER | 否 | 0 | CHECK (CurrentOrdinal >= 0) | 源 Schema 未提供字段注释 |


### SchemaMigrations

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| Version | INTEGER | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| Name | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| AppliedAtUtc | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |


### WordGroupForms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| FormIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| Surface | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Reading | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |
| FormCode | TEXT | 否 | — | CHECK (FormCode IN ( 'Dictionary', 'Polite', 'Te', 'Ta', 'Base', 'PresentFirstPersonSingular', 'PresentNonThirdPerson', 'ThirdPersonSingular', 'Past', 'PastParticiple', 'PresentParticiple', 'Plural', 'Comparative', 'Superlative')) | 源 Schema 未提供字段注释 |
| ScriptCode | TEXT | 是 | — | CHECK (ScriptCode IS NULL OR ScriptCode IN ('Kanji', 'Kana')) | 源 Schema 未提供字段注释 |
| RoleCode | TEXT | 是 | — | CHECK (RoleCode IS NULL OR RoleCode IN ('Noun', 'Verb', 'Adjective', 'Adverb', 'Other')) | 源 Schema 未提供字段注释 |
| FormLabel | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| DetailLabel | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| IsCommon | INTEGER | 否 | — | CHECK (IsCommon IN (0, 1)) | 源 Schema 未提供字段注释 |
| IsPrimary | INTEGER | 否 | — | CHECK (IsPrimary IN (0, 1)) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (MemberId, FormIndex) |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### WordGroupMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| MeaningIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| Text | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (MemberId, MeaningIndex) |
| FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE |


### WordGroupMembers

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| MemberId | INTEGER | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| WordGroupId | TEXT | 否 | — | — | 单词组标识 |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | 语言 |
| SourceSenseId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| SourceLexemeId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Headword | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Reading | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |
| PartOfSpeechLabel | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| AttributeLabel | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | 源 Schema 未提供字段注释 |
| SortOrder | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| MeaningLanguage | TEXT | 否 | 'und' | — | 释义语言 |
| MeaningSource | TEXT | 否 | 'Legacy' | — | 释义来源 |
| MeaningPrecision | TEXT | 否 | 'Missing' | — | 释义精度 |
| KanjiUsageCode | TEXT | 是 | — | CHECK ( (Language = 'English' AND KanjiUsageCode IS NULL) OR (Language = 'Japanese' AND KanjiUsageCode IN ('None', 'Common', 'Rare')) ) | 汉字使用类别 |

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
| WordGroupId | TEXT | 否 | — | PRIMARY KEY | 单词组标识 |
| Language | TEXT | 否 | — | CHECK (Language IN ('Japanese', 'English')) | 语言 |
| AnchorSourceSenseId | TEXT | 否 | — | — | 锚定词义标识 |
| Placement | TEXT | 否 | — | CHECK (Placement IN ('Inbox', 'Learning', 'Review')) | 所属学习阶段 |
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
| Key | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| Value | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

### Entries

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| SourceSequence | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| SenseIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| PrimaryWritten | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| PrimaryReading | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| LexicalCategory | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| VerbClass | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Transitivity | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| KanjiUsage | TEXT | 否 | — | CHECK (KanjiUsage IN ('None', 'Common', 'Rare')) | 源 Schema 未提供字段注释 |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (SourceSequence, SenseIndex) |

### PartsOfSpeech

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| SortIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| Value | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (EntryId, SortIndex) |
| FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE |

### SourceGlosses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Language | TEXT | 否 | — | — | 语言 |
| GlossIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| GlossType | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |
| Text | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (EntryId, Language, GlossIndex) |
| FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE |

### ChineseMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| EntryId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| MeaningIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| Text | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| MeaningSource | TEXT | 否 | — | CHECK (MeaningSource = 'Tomoshi') | 释义来源 |
| MeaningPrecision | TEXT | 否 | — | CHECK (MeaningPrecision = 'Exact') | 释义精度 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (EntryId, MeaningIndex) |
| FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE |

### Forms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| FormId | INTEGER | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| EntryId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Surface | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Reading | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| FormKind | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| ScriptKind | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| LexicalRole | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| IsCommon | INTEGER | 否 | — | CHECK (IsCommon IN (0, 1)) | 源 Schema 未提供字段注释 |
| IsPrimary | INTEGER | 否 | — | CHECK (IsPrimary IN (0, 1)) | 源 Schema 未提供字段注释 |

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
| Key | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| Value | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

### Lexemes

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LexemeId | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| Lemma | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| SourceEntryKey | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| PartOfSpeech | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (Lemma, SourceEntryKey) |

### Synsets

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| PartOfSpeech | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Ili | TEXT | 是 | — | — | 源 Schema 未提供字段注释 |

### Senses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SenseId | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| LexemeId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| SynsetId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| SenseOrder | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| FOREIGN KEY (LexemeId) REFERENCES Lexemes (LexemeId) |
| FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId) |

### Definitions

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| DefinitionIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| Text | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SynsetId, DefinitionIndex) |
| FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId) |

### Examples

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SynsetId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| ExampleIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| Text | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SynsetId, ExampleIndex) |
| FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId) |

### Forms

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| FormId | INTEGER | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| LexemeId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Surface | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| FormKind | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| FormSourceKind | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| UNIQUE (LexemeId, Surface, FormKind) |
| FOREIGN KEY (LexemeId) REFERENCES Lexemes (LexemeId) |

### Derivations

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| SourceSenseId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| TargetSenseId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (SourceSenseId, TargetSenseId) |
| FOREIGN KEY (SourceSenseId) REFERENCES Senses (SenseId) |
| FOREIGN KEY (TargetSenseId) REFERENCES Senses (SenseId) |

### LearningSenses

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | PRIMARY KEY | 源 Schema 未提供字段注释 |
| SourceLexemeId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| Headword | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| PartOfSpeech | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| IsPhrase | INTEGER | 否 | — | CHECK (IsPhrase IN (0, 1)) | 源 Schema 未提供字段注释 |
| MeaningSource | TEXT | 否 | — | CHECK (MeaningSource IN ('ChineseOpenWordNet', 'Ecdict', 'Missing')) | 释义来源 |
| MeaningPrecision | TEXT | 否 | — | CHECK (MeaningPrecision IN ('Exact', 'Broad', 'Missing')) | 释义精度 |
| SortOrder | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |

### LearningSenseSources

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| SenseId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (LearningSenseId, SenseId) |
| FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId) |
| FOREIGN KEY (SenseId) REFERENCES Senses (SenseId) |

### LearningMeanings

| 字段 | 类型 | 可空 | 默认值 | 键与约束 | 注释 |
|---|---|---|---|---|---|
| LearningSenseId | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |
| MeaningIndex | INTEGER | 否 | — | — | 源 Schema 未提供字段注释 |
| Text | TEXT | 否 | — | — | 源 Schema 未提供字段注释 |

**表级约束**

| 约束定义 |
|---|
| PRIMARY KEY (LearningSenseId, MeaningIndex) |
| FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId) |
