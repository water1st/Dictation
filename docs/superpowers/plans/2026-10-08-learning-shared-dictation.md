# 学习共享听写 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline，串行执行，不分派实现代理。Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 学习模式的一次相同听写作答同步推进各义项的独立词形状态，保留重练、记忆和整组删除隔离。

**Architecture:** Presentation → Core → DataAccess。直接组合现有 `PracticeDictationTargets`、`PracticeAnswerTransition`、调度器及记忆初始化；原学习页和答题接口不克隆。新增持久化只填补共享目标历史缺口。

**Tech Stack:** .NET 10、WPF、Dapper/SQLite、xUnit v3，无新增包。

**Spec:** `docs/requirements/RefactorSpecification.md` 第 4、7、11 节；承接已完成的复习会话计划。这是已批准完整目标的执行安排，不新建 GOAL、不新增需求。

## Global Constraints

- 只有题型、字形、提示词形、实际发音和可接受答案完全相同的听写题共享一次观察；不同义项看义写词不合并，同音不同字也不合并。
- 独立词形仍需两次间隔正确及渠道／字形覆盖；首次错误不能因后续正确变为 Good。整组计数不按观察数量膨胀。
- 同一事务提交实际作答、所有目标状态、完成词组、记忆种子和下一题；失败、取消、过期提交不得部分写入。
- 删除主出题组不能删除其他词组的共享成绩；只删除被指定组的学习／复习状态。
- 不重新验证既有词库、TTS、导航内核；不修改已提交迁移，新增迁移 12。冻结前适配迁移版本／表枚举，RED 必须编译成功后的行为断言失败。
- 只在 Dictation 的 codex/ 分支工作；不覆盖 Resources 或用户数据，不合并、推送或发布。

## Review Focus

1. 相同答案但不同提示或发音，不能共享；Task 1 用真实词库快照改变答案／读音验证隔离。
2. 删除主出题组不能令另一义项失去初错记录；Task 1 验证共享历史独立级联与 Again 记忆。
3. 次要目标写入失败不得只保存主目标；Task 1 用 SQLite 触发器验证整体回滚。
4. 渠道混用必须覆盖两者，不能把听写成功算作看义写词成功；Task 1 验证动态覆盖及独立看义题。
5. UI 总结应统计真实作答次数，而非目标投影数量；Task 2 验证共用页面实际反馈与结算。

### Task 1：复用纯规则并原子保存目标历史

**Files:** 修改 `Core/Learning/LearningSessionService.cs`、`DataAccess/Learning/ApplyLearningAnswerPo.cs`、`LearningSessionDataAccessor.cs`、`Runtime/SchemaMigrationRunner.cs`；新增 `Core/Learning/LearningAnswerMapper.cs`、`DataAccess/Learning/LearningFormOutcomePo.cs`。测试 `Tests/Core/LearningSharedDictationTests.cs`；迁移版本／表清单调用方在冻结前统一适配。

**Interfaces:** `LearningFormOutcomePo(WordGroupId, MemberId, FormCode, CorrectStreak, IsMastered, NextEligibleOrdinal)`；`ApplyLearningAnswerPo` 追加可选 `AdditionalOutcomes`（只含次要目标）；Mapper 纯转换词形结果。现有单题公开 Service／DTO 接口不变。迁移 12 创建 `LearningSharedAnswerAttempts`：次要目标的会话、题目编号／序号、词组、成员、词形、渠道、字形、文本、判定和时间；外键归属于本目标而非主题，主目标沿用原记录。调度历史合并原题目历史与这些观察。

- [x] 真实日英 DI/SQLite 测试覆盖共享、独立 FSRS、初错保留、看义和答案／发音隔离、删除、失败回滚、取消／重复；追加混合渠道共 10 项。迁移表清单遗漏及重新冻结经过见验证文档，不隐藏旧测试适配失败。
- [x] 重新冻结后零警告／错误构建，实际 10 项中 7 项行为 RED、3 通过、0 跳过，之后仅恢复生产；复用现有匹配／状态转换，不复制算法。事务复查目标归属和未掌握状态，保留独立观察及词组计数。
- [x] 全量 1773/1773、零失败／跳过，solution 构建零警告／错误，冻结哈希不变、diff 检查通过。证据 `docs/verification/2026-10-08-learning-shared-dictation.md`，准备首次中文提交；Task 2 尚未执行，不提前标记完整计划完成。

### Task 2：验证现有共用页面的完整学习链路

**Files:** 新增 `Tests/Presentation/LearningSharedDictationPresentationTests.cs`，必要的实际 UI 修正仅在原 Practice 页面／workflow；不创建另一套学习页。更新 AGENTS 测试基线及验证记录。

**Interfaces:** 沿用 `PracticeSessionViewModel.StartAsync/SubmitAsync/ContinueAsync`、现有学习 workflow 与首页进度读取能力。

- [ ] STA 真实页面验证相同听写的反馈、两次正确结算“2 次作答／2 词组”、首页完成数；错误后重练显示双方错误而作答数不加倍、浏览无成绩副作用。对新增断言临时生产行为 mutation，编译成功 RED→恢复 GREEN。
- [ ] 统一全量、构建、相关运行冒烟后中文提交；计划末尾只进行一次独立最终审查，重要问题集中 RED→GREEN 修复，持久化证据后清理本计划临时目录。完整 GOAL 仍有错题管理、五十音、MCP、统计、更新发布和最后 OCR。
