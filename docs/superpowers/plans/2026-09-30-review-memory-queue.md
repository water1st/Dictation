# FSRS 记忆与复习队列 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 已完成学习的词形自动获得可验证的 FSRS 下次复习时间，并让用户在单窗口查看按风险排列的复习词库；直接导入复习的旧词保持待测状态。

**Architecture:** `Presentation → Core → DataAccess`。Core 组合纯 FSRS 计算与队列查询，学习结果和初始记忆状态在同一数据库事务保存。复用已有词组快照读取器和 Mapper；不增加仓储、通用框架或第二套容器。

**Tech Stack:** .NET 10、WPF、Dapper/SQLite、xUnit v3。无新增运行依赖；以官方 `py-fsrs` 固定版本作为 FSRS 6 数值参考，仅移植本产品需要的 Good/Again 计算内核。

**Spec:** `docs/requirements/RefactorSpecification.md` 第 3、4、7、10、11 节。该计划是完整复习功能的第一个垂直切片；复习答题、每日计划、错题重练和报表随后分别接通，不将只读队列冒充已完成的复习模式。

## Global Constraints

- 目标保持率固定 **90%**；正确 `Good`、错误 `Again`，无主观熟悉度按钮。同一会话曾经答错的词形，即使重练正确，初次记忆评价仍是 `Again`。
- 记忆按词组成员＋词形记录，词组数量不随形式数量膨胀；日英语查询隔离。直接送到复习库但没有记忆状态的词，视为需要首次测试，不能伪造掌握记录。
- FSRS 内核依据官方默认 21 个参数；同日短期更新、长期正确／遗忘更新和遗忘概率保持参考语义。会话内已经处理间隔 3–7 题的重练，因此参考调度器 `learning_steps=()`、`relearning_steps=()`；关闭到期时间随机扰动，出题顺序仍由后续会话负责随机化。
- 学习完成后初始记忆只创建一次；补充学习或重复完成不能覆盖已有复习状态。整组删除连同记忆状态删除。迁移复用已有备份和事务机制，用户运行数据不进入发布 ZIP。
- 所有业务及纯类型一文件一个主要类型，简单算法具体类注入；跨 UI/MCP 的查询能力使用小接口。Mapping 不写入 Service，Presentation 不承担判题、风险算法或 SQL。
- RED 必须构建成功并执行行为断言；冻结测试后只改生产代码转绿。当前回归基线 1494 项；架构 API 和迁移枚举适配在冻结前完成。

## Review Focus

1. 学习中一个形式错误而其它正确，不能把整组全算错或在重练后丢失最初错误；Task 2 检查每个形式初始化结果。
2. 补充学习、重复提交或重启，不得重置已经形成的较长记忆间隔；Task 2 检查同一事务的首次插入及重复保护。
3. 老词直接送到复习库，没有学习历史；Task 3 明确显示待测且立即进入候选，不虚构稳定度。
4. 同日重答、逾期答题、时区偏移和时钟倒退；Task 1 用官方黄金向量和非法输入测试固定数学边界。
5. 学习答题保存失败、取消或整组删除，不能留下孤立记忆记录；Task 2 验证事务回滚和外键级联，Task 3 验证界面刷新。

---

### Task 1: 可核对的 FSRS 6 最小内核

**Files:** 新增 `Dictation.Core/Review/FsrsMemoryState.cs`、`FsrsScheduler.cs`，`Dictation.Tests/Core/FsrsSchedulerTests.cs`；更新 Core DI；新增 `docs/resources/FsrsReference.md` 与 `LICENSES/py-fsrs.txt`，并将许可证随程序复制发布。

**Interfaces:** `internal FsrsScheduler.Schedule(FsrsMemoryState? previous, bool wasCorrect, DateTimeOffset reviewedAtUtc) → FsrsMemoryState`；`GetRetrievability(FsrsMemoryState? state, DateTimeOffset now) → double`。状态字段为稳定度、难度、UTC 上次评价／到期时间、评价次数及遗忘次数；无状态以 `null` 表示。

- [x] **Step 1：测试及参考记录。** 官方固定 commit `9446cb06605c597a063aeee49f7d188d42e34dc2` 未改 Scheduler 生成黄金向量：首次 Good `S=2.3065`、2 天；首次 Again `S=0.212`、1 天；第二天 Good `S=10.964332335820698`、11 天；第七天 Again `S=0.7178527669042931`、1 天；同日 Again／Good、长序列、风险随天数下降。补非法非有限值、范围、计数、时钟倒退、UTC 归一化及不修改输入断言。
- [x] **Step 2：有效 RED。** 最小可编译骨架只返回错误初始状态，冻结测试；构建成功后 `--filter-class '*FsrsSchedulerTests' --minimum-expected-tests 12`，记录真实数值／边界失败。
- [x] **Step 3：GREEN。** 仅移植固定参考公式，采用与 Python 一致的整天取整及银行家舍入；保留 1–36500 天范围。无 optimizer、Python 运行时、Hard/Easy UI、自创记忆算法或为未来参数建配置系统。
- [x] **Step 4：验证并中文提交。** 聚焦与全量测试、solution 构建、冻结哈希及许可证输出检查。此提交交付算法能力，不声称完整复习已经可用。

### Task 2: 学习完成的原子记忆初始化

**Files:** 新增 `Dictation.DataAccess/Review/ReviewFormMemoryPo.cs`、`ReviewMemoryWriter.cs`；修改 `SchemaMigrationRunner.cs`、`Learning/ApplyLearningAnswerPo.cs`、`LearningSessionDataAccessor.cs`；新增 Core `Review/LearningMemorySeedPlanner.cs`、`ReviewMemoryMapper.cs`；修改 `LearningSessionService.cs` 及 DI。测试放在 `Dictation.Tests/Core/LearningMemorySeedTests.cs`、`DataAccess/ReviewMemoryTransactionTests.cs`，调整旧构造调用与迁移表枚举后再冻结。

**Interfaces:** `LearningMemorySeedPlanner.CreateSeeds(IReadOnlyList<LearningFormUnitPo> completedForms, IReadOnlyList<LearningQuestionHistoryPo> history, DateTimeOffset now) → IReadOnlyList<ReviewFormMemoryPo>`；调用方只提供本次完成组的启用形式，复用 Task 1 内核，按本会话该成员词形是否有错作首次评价。没有本会话作答的形式不伪造评价。`ApplyLearningAnswerPo` 增加可选记忆种子列表；`ReviewMemoryWriter.InsertMissingAsync(SqliteConnection connection, DbTransaction transaction, IReadOnlyList<ReviewFormMemoryPo> seeds, CancellationToken token)` 使用调用方事务，不另开连接。

- [x] **Step 1：正常和失败测试。** 双形式一错一对分别 Again／Good；只初始化本次完成组的启用形式；再次完成保留旧状态；直接入复习不创建伪造状态；取消／事务失败没有半保存；删除组和成员级联清理。字段严格校验所属词组／成员／词形，非法关系拒绝并回滚。
- [x] **Step 2：有效 RED。** 编译适配及迁移测试期望更新完成后冻结；临时禁止种子保存，运行目标测试获得行为 RED，再仅改实现。
- [x] **Step 3：打通 GREEN。** 新增迁移 `ReviewFormMemories`，按成员词形唯一，稳定度正数、难度 1–10、计数合法、组与成员外键级联；在原学习答题事务提交前插入未有状态的种子。初始计划完成、进度、Placement 与记忆必须共同提交；不在 Core 完成事务之后补写。
- [x] **Step 4：验证并中文提交。** 聚焦、全量、构建和真实 SQLite 学习完成到复习记忆冒烟；保留已有预览用户数据。

### Task 3: 单窗口复习词库与风险队列

**Files:** 新增 DataAccess `Review/ReviewLibraryDataAccessor.cs`、`ReviewLibrarySnapshotPo.cs`；Core `Review/IReviewLibraryQuery.cs`、`ReviewLibraryService.cs`、`ReviewLibraryMapper.cs`、`ReviewLibraryDto.cs`、`ReviewWordGroupDto.cs`、`ReviewFormMemoryDto.cs`；Presentation `ViewModels/ReviewLibraryViewModel.cs`、`Views/ReviewLibraryPage.xaml` 与 code-behind。修改首页导航接口、Home VM、NavigationService、App DataTemplate、DI；新增 Core／Presentation 对应测试及用户说明。

**Interfaces:** DataAccessor `LoadAsync(string language, CancellationToken token) → ReviewLibrarySnapshotPo`，读用户 Review 组、成员快照和记忆状态；`IReviewLibraryQuery.LoadAsync(LearningLanguage language, CancellationToken token) → ReviewLibraryDto`。结果包括已入复习的整组及形式、待测／已到期状态、最早下次复习 UTC 和当前最低预计保持率。Core 风险优先排序，同级使用稳定次序便于查看；后续答题会话负责随机题序。

- [ ] **Step 1：测试。** 真实 DI/SQLite 验证日英隔离、20 个形式仍计 1 词、直接导入的旧词立即待测、到期与风险顺序、查看不改变记忆、非法语言与取消。STA 验证中文卡片、词形及下次时间绑定，单窗口返回与后台取消不回写旧页。
- [ ] **Step 2：有效 RED。** 可编译最小页面／查询骨架后冻结测试，针对风险或旧词遗漏制造实现错误，运行聚焦行为失败再恢复。
- [ ] **Step 3：实现真实链路。** 复用词组快照读取器，Core 组合 FSRS 和 Mapper；页面异步后台读取，展示圆角词组卡片而非 ERP 表格，选定组显示其形式和记忆信息。首页增加“复习词库”，不放尚未实现的开始答题空按钮，不把浏览算作完成复习。
- [ ] **Step 4：验收并中文提交。** 全量≥当前实际基线、零错误构建、正式词库导入／学习完成／复习队列冒烟，预览数据保留；一次独立最终审查、统一修正后持久化证据并清理该计划临时目录。继续复习答题计划，不标记整个 GOAL 完成。

## 选型与范围审查

官方实现清单未提供官方 C# 运行库；直接绑定 Rust/Python 会引入与桌面目标无关的运行环境。这里移植固定版本 MIT 数学内核并用未改的官方 Scheduler 数值验证，不自行重新设计算法，不引入无人维护的小包。参考源码只用于开发验证，产品无需 Python、网络或额外服务。MCP、五十音、统计、更新发布与 OCR 不混进本计划；它们仍在已批准 GOAL 后续清单内。
