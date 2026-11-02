# Dictation Database Schema

> 本文档记录数据库表、字段、约束与索引；实际定义以当前仓库源码及迁移执行结果为准。

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

