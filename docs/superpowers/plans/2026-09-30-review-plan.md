# 独立复习计划 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用户能够在单窗口为日语、英语分别保存每日复习目标、每轮词数、词形／题型／字形及词组范围，并管理复习库；随后复习答题直接消费此计划，不借用学习计划。

**Architecture:** `Presentation → Core → DataAccess`，唯一 DI。复用现有词库、词组快照、只读风险查询、选项控件与英语示例范式解析；只有确有两个消费者的规则才抽出组合组件，不增加仓储或通用框架。

**Tech Stack:** .NET 10、WPF、Dapper/SQLite、xUnit v3，无新包。

**Spec:** `docs/requirements/RefactorSpecification.md` 第 4、5、6、7、10、11 节；承接 `2026-09-30-review-memory-queue.md`。用户已授权自主继续非 OCR 开发，这里整理已有批准需求，不新增产品决策或重新发起 GOAL。

## Global Constraints

- 两种语言、学习与复习计划分别保存；只在首页切换语言。一组无论词形数量多少仍计 1 词。
- 沿用学习计划现有数量边界 1–500、默认每日 20／每轮 20；复习固定风险优先，不能新增随机／顺序选词设置。
- 词形、题型至少各一项。日语假名／汉字至少一项；英语只用 Latin。不存在的汉字不会生成题目；选择字形不等于所有词都有该字形。
- 默认范围为全部当前语言复习库，也可勾选具体整组；指定范围至少一组。勾选是计划范围，不删除其它词组或进度。
- 默认英语标签仍用已有语法名称；示例范式设置继续采用已存在的 do／自选动词解析规则，不泄露目标答案。
- 删除是整组物理删除及其学习／记忆／历史级联；删除前复用单窗口确认弹层，无“归档”。从计划范围取消勾选不等于删除词组。
- 红灯必须编译成功、目标测试真实执行且行为断言失败；冻结后只修生产实现。以执行时最新全量基线为门槛，不降低数量掩盖回归。

## Review Focus

1. 保存所选范围时组被删除或改变 Placement：拒绝无效选择，原计划不能半保存；Task 1 用 SQLite 事务故障及失效组测试固定。
2. 英语例词缺少某个启用形式：保持既有明确校验，不出现能保存但无法使用的计划；Task 1 覆盖无效例词与缺失范式。
3. 计划更新不改变现有记忆间隔、评价次数或学习计划；Task 1 前后直接核对三类持久化状态。
4. 日语无汉字词、名词没有动词活用：管理列表正常展示，不能把不可用形式伪造为适用；Task 2 验证属性与词形。
5. 全选、部分选与全部取消：界面明确“全部词库”或“指定词组”，删除按钮不得把取消勾选当删除；Task 2 验证命令和状态变化。

---

### Task 1: 真实复习计划与整组管理服务

**Files:**

- 新增 Core `Review/ReviewPlanDto.cs`、`SaveReviewPlanRequestDto.cs`、`ReviewRangeMode.cs`、`IReviewPlanReader.cs`、`IReviewPlanEditor.cs`、`IReviewLibraryManager.cs`、`ReviewPlanService.cs`、`ReviewPlanMapper.cs`。
- 新增 DataAccess `Review/ReviewPlanPo.cs`、`ReviewPlanDataAccessor.cs`；修改 `Runtime/SchemaMigrationRunner.cs` 和两层 DI。
- 将已有学习计划的词形／渠道／输出校验和英语例词解析分别抽到 Core `Learning/PracticePlanOptionValidator.cs`、`Learning/EnglishExampleParadigmResolver.cs`；学习服务组合调用，保留现有消费者行为，不复制两套规则。
- 新增 `Dictation.Tests/Core/ReviewPlanServiceTests.cs`、`DataAccess/ReviewPlanDataAccessorTests.cs`，先适配迁移枚举和构造测试再冻结。

**Interfaces:**

- `ReviewPlanDto`／保存请求字段：Language、DailyReviewTarget、SessionWordLimit、RangeMode、WordGroupIds、FormCodes、Channels、OutputModes、EnglishFormLabelMode、EnglishExampleSourceSenseId；查询 DTO 另含已解析 EnglishExampleParadigm。
- `IReviewPlanReader.LoadAsync(LearningLanguage, CancellationToken) → ReviewPlanDto`；`IReviewPlanEditor.SaveAsync(SaveReviewPlanRequestDto, CancellationToken) → ReviewPlanDto`。
- `IReviewLibraryManager.DeleteWordGroupAsync(LearningLanguage, string wordGroupId, CancellationToken) → Task`，只允许当前语言 Review 组。
- `ReviewPlanDataAccessor.GetPlanAsync(string language, CancellationToken) → ReviewPlanPo?`、`SavePlanAsync(ReviewPlanPo, CancellationToken) → Task`、`DeleteWordGroupAsync(string language, string wordGroupId, CancellationToken) → Task`。具体类 DI；选定组列表按真实外键级联移除，SQL 校验 language／Placement。
- 共享具体组件 `PracticePlanOptionValidator.NormalizeForms(LearningLanguage, IReadOnlyList<string>) → IReadOnlyList<string>`、`NormalizeChannels(IReadOnlyList<LearningChannel>) → IReadOnlyList<LearningChannel>`、`NormalizeOutputs(LearningLanguage, IReadOnlyList<LearningOutputMode>) → IReadOnlyList<LearningOutputMode>`、`ValidateLanguageLabels(LearningLanguage, EnglishFormLabelMode, string?) → void`；不依赖某一个计划 DTO。
- `EnglishExampleParadigmResolver.ResolveAsync(string? sourceSenseId, IReadOnlyList<string> enabledForms, CancellationToken) → EnglishExampleParadigmDto`。学习与复习服务直接组合该具体类，保留现有词库能力接口与动词校验；不新增无消费者的接口。

- [ ] **Step 1：测试与编译适配。** 默认只读不写数据；日英保存独立；学习设置不变；重复选项规范化；空值、越界、跨语言形式／字形、空指定范围、外语／Learning／不存在组、未知枚举、取消均拒绝。保存过程触发器失败后旧计划与范围完整保留；删除整组时记忆与历史同时级联，不触碰其它组；取消范围只改关系。
- [ ] **Step 2：行为 RED。** 先构建最小可编译骨架；冻结 SHA-256。禁止保存或取消参数校验产生实际状态／异常断言失败，记录失败数量；不以构造错误或编译失败充数。
- [ ] **Step 3：最小 GREEN。** 沿用现有计划的按语言主表与选项子表模式，新增一次迁移；范围关系使用组外键。事务内验证选择并更新计划、选项和范围；Core 校验业务与组归属，Mapper 只转换，DAL 不执行风险算法。没有已存计划时按语言产生默认词形及输出，与学习默认值一致但对象和存储独立。
- [ ] **Step 4：验证和提交。**
  - `dotnet build Dictation.sln --configuration Debug --no-restore` → 0 错误。
  - 聚焦 `--filter-class '*ReviewPlanServiceTests'`／`'*ReviewPlanDataAccessorTests'`，最低项数采用实际冻结批次 → 全绿且非零。
  - 全量 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1560`（新增后提高实际门槛）→ 无失败／跳过。
  - 同一测试哈希、`git diff --check` 和真实 SQLite 状态核对 → 中文提交“复习：保存独立计划与整组范围”。

### Task 2: WPF 复习计划与词库管理

**Files:** 新增 Presentation `ViewModels/ReviewPlanViewModel.cs`、`ReviewPlanWordGroupItemViewModel.cs`、`Views/ReviewPlanPage.xaml` 及 code-behind；修改 Home VM／Page、导航能力、NavigationService、App DataTemplate 与 DI。复用现有选项项模型、确认弹层和风险查询，不新建窗口。新增 `Dictation.Tests/Presentation/ReviewPlanPageTests.cs`、`ReviewPlanLifecycleTests.cs`；API 适配在冻结前完成。

**Interfaces:** `ReviewPlanViewModel.Initialize(LearningLanguage)` 固定语言；`RefreshAsync(CancellationToken)`、`SaveAsync(CancellationToken)`、`DeleteSelectedGroupAsync(CancellationToken)`；绑定命令 Save／Refresh／Back、AllSelected 及单组选择。首页 `OpenReviewPlanAsync(LearningLanguage, CancellationToken)` 使用原页面栈。

- [ ] **Step 1：测试。** 真实 DI／SQLite：加载和修改后保存、重开与重启仍一致；范围列表显示中文、属性及整组数；全选复选框与各项联动；空指定范围显示错误不改数据库；删除确认／取消、组移除与原记忆级联；语言仅首页切换。STA 真实控件检查绑定、浅深主题文字、窄窗口滚动及无 DataGrid／独立 Window。
- [ ] **Step 2：行为 RED。** 先骨架编译成功并冻结测试；空保存命令或错误范围映射必须使真实持久化断言失败。随后只改生产转绿。
- [ ] **Step 3：打通页面。** 卡片式计划与词组管理；列表顶部全选。删除前单窗口弹层确认。数据读取／保存后台执行，UI 线程绑定；返回先取消操作，晚完成不回写旧页。显示明确保存／错误状态，不新增未接通的复习答题按钮。
- [ ] **Step 4：验收与提交。** 聚焦、最新基线全量、solution 构建和正式词库范围／保存／重开／删除冒烟；用户数据隔离、不重新生成词库。中文提交“复习：接通独立计划与词库管理页面”。一次最终审查，重要发现按新冻结测试 RED→GREEN 修正，记录证据后清理本计划临时目录。

## 下一步与非目标

紧接本计划接通复习会话：按此独立范围与目标风险选词、弱词形与渠道动态覆盖、客观答案、首次错误保留 Again、同一事务更新 FSRS 与答题进度。当前计划不把管理／浏览算复习完成，不改变既有学习会话。独立五十音、MCP、统计、ZIP 更新发布和 OCR 仍为完整 GOAL 的后续必做内容，不能据本计划结束将 GOAL 标记完成。
