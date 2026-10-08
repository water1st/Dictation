# 客观复习会话 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans inline，按任务执行，不分派实现代理。Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 首页启动按独立计划选词的可恢复复习会话，客观判题、错后重练、结束总结并原子更新 FSRS。

**Architecture:** Presentation → Core → DataAccess，沿用唯一 DI、SQLite 事务与既有判题／动态覆盖／FSRS。复习会话与学习进度分表，公共题面与音频呈现复用，不通过继承复用业务。

**Tech Stack:** .NET 10、WPF、Dapper/SQLite、System.Text.Json、xUnit v3，无新包。

**Spec:** `docs/requirements/RefactorSpecification.md` 第 4–7、9、10、11 节；承接 `2026-09-30-review-plan.md`。用户已批准继续完整 GOAL、自主完成非 OCR，本文是执行安排，不另开 GOAL 或新增需求。

## Global Constraints

- 学习／复习及日／英独立。整组计一个词，不按形式或作答次数计数；首页才切换语言。
- 复习风险优先、题序随机；仅启用形式、适用字形参与风险和出题。无记忆的旧词先初测，已记忆形式按到期参加；禁用形式不挤占本轮。
- 会话记录启动计划快照，返回、重开、跨日重启继续同一轮，不因修改计划丢失本轮状态；新计划下轮生效。
- 错误立即展示答案、间隔 3–7 题重练、结算再次汇总；首次错误即使之后正确仍是 Again。每个启用形式间隔连续正确两次才完成，动态覆盖而非完整组合。
- 复用 FSRS 90% 的 Good／Again 内核。完成的成员词形与答题进度同一事务写入，不能更新学习成绩、重复评价、或让音频失败判错。
- 无待测／到期词或当天额度已满时不创建空会话。每日目标只计不同整组，跨日活动会话优先恢复。
- 整组删除级联复习状态／历史；晚完成不回写旧页。只读浏览、音频不推进进度。
- 原词库不重建、不覆盖用户数据。编译成功的真实行为 RED→冻结不变 GREEN；全量门槛不低于最新实际基线 1632。

## Review Focus

1. 启用形式没有适用汉字或实际词形时，不能创建永远完成不了的会话；Task 1 测试无适用形式且零写入。
2. 词组在选取后被删除／改变区域，创建必须原子拒绝；Task 1 DAL 测试失效组及触发器失败回滚。
3. 两次启动竞争、跨日恢复、整组全部删除，不能产生两轮活动会话或卡死唯一约束；Task 1 测试并发、恢复和清空后重开。
4. 答案重复提交或事务故障，不能重复计数／评价 FSRS，旧状态保持；Task 2 用真实 SQLite 检验过期题拒绝与中途回滚。
5. 浏览及失败的听写音频不能当答对或答错；Task 3 测试进度和记忆不变、失败后可重播。

---

### Task 1：风险选词与可恢复会话

**Files:**
- Core/Review：`IReviewSessionStarter.cs`、`IReviewSessionReader.cs`、`ReviewSessionDto.cs`、`ReviewSessionService.cs`、`ReviewSessionMapper.cs`、`ReviewSessionSelectionPlanner.cs`、`ReviewSessionGroupSelection.cs`。
- DataAccess/Review：`ReviewSessionDataAccessor.cs`、`ReviewSessionPo.cs`、`CreateReviewSessionPo.cs`、`ReviewSessionGroupSeedPo.cs`、`ReviewSessionFormSeedPo.cs`；迁移 9 及两层 DI。
- 测试 `Core/ReviewSessionStartTests.cs`、`DataAccess/ReviewSessionDataAccessorTests.cs`；迁移版本适配在冻结前完成。

**Interfaces:**
- `IReviewSessionStarter.StartOrResumeAsync(LearningLanguage, CancellationToken) → Task<ReviewSessionDto?>`，无候选返回 null，不制造完成记录。
- `IReviewSessionReader.GetActiveAsync(LearningLanguage, CancellationToken) → Task<ReviewSessionDto?>`；`GetBrowseItemsAsync(string sessionId, CancellationToken) → Task<IReadOnlyList<ImportWordGroupDto>>`。
- `ReviewSessionDto`：SessionId、Language、CreatedAtUtc、WordGroupIds、PlanSnapshot（ReviewPlanDto）；任务 2 在此增加真实会话状态，不复制学习进度。
- DAL `GetActiveAsync(string language, CT) → ReviewSessionPo?`、`CreateAsync(CreateReviewSessionPo, CT) → ReviewSessionPo`、`GetBrowseItemsAsync(string sessionId, CT) → IReadOnlyList<ImportWordGroupPo>`、`CountCompletedOnDateAsync(string language,string localDate,CT) → int`。
- PO 保存 SessionId、Language、PlanJson、CreatedAtUtc、WordGroupIds；创建另含 LocalDate、有序组及成员 SourceSenseId／FormCode 种子。表：ReviewSessions、ReviewSessionGroups、ReviewSessionForms；形式按成员+FormCode唯一，汉字假名不是两个记忆。
- SelectionPlanner 消费真实 ReviewLibraryDto／ReviewPlanDto 和剩余额度，返回内部 ReviewSessionGroupSelection；Mapper 转为 DAL 种子和 JSON，Service 只编排。

- [x] 写真实 DI／SQLite 测试：初测整组一次、仅弱到期形式、计划范围／每日与每轮限制、没有适用汉字、未来记忆不出题、英语派生同组、启动和浏览不改记忆／学习、计划快照和重启恢复、非法语言／空ID／取消。DAL 验证组／形式在事务内归属、并发唯一、写入失败回滚、级联清空后能新开。
- [x] 首批骨架编译与 22+27 项行为 RED／GREEN 已执行；随后补齐旧测试的迁移适配，按用户最新要求取消重复 RED，仅统一整合回归。证据边界见 `docs/verification/2026-10-01-primary-integration.md`，不冒称第二批 RED 已执行。
- [x] 最小实现：范围先筛选，只考虑启用且有适用答案的形式；待测优先，再按这些形式最低保持率／到期／稳定词组顺序排序，取 min(剩余每日额度, 每轮数量)。组内只带待测或到期形式，复用已存中文快照；写事务再次校验真实归属，唯一活动语言约束，返回竞争胜者。已有活动会话先返回，不读取新计划。JSON 用 BCL 序列化快照。
- [x] 主目录统一构建及全量 1681/1681、diff 检查；中文提交“复习：按风险保存可恢复整组会话”。不重复旧功能逐项验证。

### Task 2：客观答题与原子 FSRS

**Files:** Core/Review `IReviewQuestionReader.cs`、`ReviewQuestionService.cs`、`IReviewSessionAnswerer.cs`、`SubmitReviewAnswerRequestDto.cs`、`ReviewAnswerResultDto.cs`、`ReviewSessionSummaryDto.cs`；Core/Practice 复用 `PracticeQuestionDto`、`PracticeQuestionScheduler`、`PracticeQuestionPresenter` 和小能力合同 `IPracticeQuestionPlan`；DataAccess/Review 会话快照／待答题／提交 PO、迁移 10，DataAccess/Practice 共享纯调度状态类型。不得另建同构题面或复制已有算法。

**Interfaces:** `GetCurrentQuestionAsync(string sessionId, CT) → PracticeQuestionDto?`；`SubmitAnswerAsync(SubmitReviewAnswerRequestDto, CT) → ReviewAnswerResultDto`。请求字段 SessionId、QuestionId、SubmittedText；结果 WasCorrect、ExpectedAnswer、ExpectedSpokenText、NextQuestion、Summary。题面保留现有学习题面字段但不含答案键，看义写词 SpokenText=null。

**接口落实：** 当前题目读取单独使用 `IReviewQuestionReader.GetCurrentQuestionAsync → PracticeQuestionDto?`，答案提交能力随后独立实现；两模式共用同构题面，而非复制 `ReviewQuestionDto`。这不共享学习与复习进度。

- [x] 读题部分：17 项真实三层测试 RED→GREEN；当前题目持久化、并发唯一、计划快照、重启、回滚、中文／礼貌形读音及无成绩副作用；全量 1698/1698。见 `docs/verification/2026-10-01-review-question.md`。以下提交／重练／FSRS 工作仍未完成，不能把 Task 2 标为完成。

- [x] 正常、边界、非法／过期／重复题、取消、初次错误后重对仍 Again、独立词形、语言、动态覆盖、相同听写去重、间隔重练、重启、事务回滚测试；编译适配后冻结，20 项实际行为 RED。
- [x] 复用 AnswerEvaluationService 和 FSRS；提取已有学习成绩转换为共用纯组件，两个模式组合使用，不复制已有内核或继承 Service。复习状态按 session+member+form 保存，正确两次及渠道字形覆盖完成；首次错保存在历史。完成形式只评价一次，持久化事务同时提交题目、次数、FSRS、组完成日和会话状态。迁移 11 独立记录共享听写目标历史，避免删除一组时级联另一组自己的进度。
- [x] 冻结不变 GREEN、全量 1718/1718／构建 0 警告错误／真实 SQLite 冒烟；证据见 `docs/verification/2026-10-08-review-answer.md`。中文提交“复习：客观答题重练与原子记忆更新”。

### Task 3：单窗口答题、浏览和首页进度

**Files:** Presentation `ReviewSessionViewModel.cs`、`ReviewSessionPage.xaml(.cs)`、Home VM/Page、导航／DI／App 模板；Core/Review `IReviewProgressReader.cs`、`ReviewDailyProgressDto.cs`、`ReviewProgressService.cs`。复用现有题面／反馈卡片、音频和单窗口导航，不克隆整页学习实现。

**Interfaces:** VM `Initialize(LearningLanguage)`、`StartAsync(CT)`、`SubmitAsync(CT)`、`ContinueAsync(CT)`、`ReplayQuestionAsync(CT)`、`ReplayBrowseAsync(CT)`；Home navigation `OpenReviewSessionAsync(LearningLanguage,CT)`；progress `GetTodayAsync(LearningLanguage,CT) → ReviewDailyProgressDto`。

- [x] 每日进度能力已提交 `9c9aa36`；首页三层绑定、语言切换、返回刷新、独立行内错误、取消与晚完成隔离已完成，新增 11 项行为 RED→GREEN，最新全量 1740/1740。见 `docs/verification/2026-10-08-home-review-progress.md`。这是本任务的一部分，不代表下面的复习答题入口或会话页面已完成。

- [ ] 真实三层单窗口端到端：首页语言锁定、浏览不改进度、听写失败不判错且可重播、反馈后续作答、结算、当天不同组数、返回取消/晚完成隔离、主题和禁拼写提醒输入；先冻结行为 RED，再仅修生产 GREEN。
- [ ] 接实际入口而非占位；重用现有内存音频播放、喇叭图标、浏览属性和派生词形。只消费 Core，不执行调度或 SQL。首页分别读取语言每日进度；不可用时显示原因而非假装0完成。
- [ ] 最新全量、solution build、正式日中英中词库隔离会话／渲染冒烟、diff与哈希；中文提交“复习：打通单窗口答题浏览与每日进度”。一次最终审查，重要发现一次 RED→GREEN 修复，保留证据后清理本计划临时目录。

## 后续仍需完成

错题独立管理／人工覆盖、独立五十音、MCP、统计打卡、ZIP 更新与 Release、OCR 均仍在完整 GOAL 内，不以本计划结束缩小目标。当前计划不安装依赖、不更新正式词库、不合并或推送。
