# 错题查看与重练 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans，当前会话串行实现，不分派实现代理；全计划结束仅一次最终审查。Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在日英隔离的同窗口错题 List/Detail 中查看真实错误和到期信息，复用既有学习／复习链路进行针对性练习。

**Architecture:** Presentation → Core → DataAccess；错误是原始作答历史的查询投影，不复制词条、不另建错题库或记忆算法。复习错题可由用户主动重练启用的错误形式，即使尚未到期；每日自动复习仍按原 FSRS 风险排序。学习中的错误继续原学习会话，不提前移动进复习库。

**Tech Stack:** .NET 10、WPF、现有 Dapper/SQLite、xUnit v3，无新增依赖或迁移。

**Spec:** `docs/requirements/RefactorSpecification.md` 第4、7、8节。完整目标已批准串行执行，本计划不新建 GOAL 或新增需求；OCR诊断及手动覆盖仍在最后 OCR 切片，不冒称本次只读 Detail 已完成人工复核。

## Global Constraints

- 使用 Dictation 主目录、codex/ 分支与唯一 DI；不合并、推送或安装环境。
- 查询学习主答案、共享次要观察及复习独立观察；共享同一次错误在各义项各记一次，不从 ReviewQuestions 重复统计。
- 只展示源词中文快照，历史错误不因本轮稍后答对而消失；正确重试保留原错误审计，FSRS仍使用真实当前会话结果。
- 列表是历史错题，可展示累计次数和下一次记忆到期时间；不把它误称未纠正队列，不让浏览或发音修改进度。
- 整组删除自然移除该组错误，但保留其它共享目标错误；语言、成员、词形归属必须验证。
- 重练不修改全局计划；只取选中复习组中计划启用且有适用答案的错误形式，整组上限沿用本轮上限。已有活动复习优先恢复，不替换或清空进度；未完成学习回原会话。
- RED必须编译成功后目标行为失败，冻结后仅改实现；各阶段整合一次全量，不能重复重做既有功能。

## Review Focus

1. 删除共享主题后的次要错误仍可查看；Task1用真实共享错误并删除主题验证。
2. 未到期重练也更新原 FSRS 而非创建新词或强改计划；Task1检查原记忆和计划未被启动操作改写。
3. 多个义项有相同答案时各自显示元数据，但一次历史不能被两个表重复统计；Task1检查累计次数。
4. 页面返回或取消不能把迟到错误覆盖新语言页面；Task2检查生命周期和单窗口返回。
5. 旧活动复习不能被新错题选择重建；Task1验证恢复同一session，Task2明示恢复已有会话。

### Task 1：真实错误查询与复用复习启动

**Files:** 新增 `DataAccess/Mistakes/WrongAnswerObservationPo.cs`、`WrongAnswerDataAccessor.cs`；新增 `Core/Mistakes/{WrongAnswerAttemptDto,WrongAnswerItemDto,WrongAnswerMapper,IWrongAnswerQuery,WrongAnswerQueryService,IWrongAnswerPractice,WrongAnswerPracticeService}.cs`；在现有三层注册入口注册。测试 `Tests/Core/WrongAnswerServiceTests.cs`。

**Interfaces:**
- `IWrongAnswerQuery.LoadAsync(LearningLanguage,CancellationToken) -> Task<IReadOnlyList<WrongAnswerItemDto>>`。
- Item含组ID、义项ID、词形、词头、中文意思、词形标签、所在学习/复习库、到期时间及错误观察列表；Attempt含渠道、字形、提交文本、UTC时间、来源模式。
- `IWrongAnswerPractice.StartOrResumeAsync(LearningLanguage,IReadOnlyList<string>,CancellationToken) -> Task<ReviewSessionDto>`；依赖错误查询、复习计划/词库、现有会话DataAccessor与时钟；现有Mapper生成种子，不复制SQL或FSRS。

- [x] 先写12项真实DI/SQLite测试：空库、正常及重复错误、主/共享/复习计数、日英隔离、中文详情、整组删除、取消、非法语言/选组、未来到期主动重练、未启用形式排除、既有会话恢复、启动不修改记忆和计划。冻结前校准真实主题删除夹具。
- [x] 只在生产查询临时丢弃共享观察、启动临时包含非错误形式，零警告错误编译后12项6行为RED6通过，随后只修实现12/12GREEN，冻结不变。另修新查询英语空ScriptCode的答案读取缺口，不改测试期望。SQL查询、Mapper投影、Service校验/编排，无新错误存储。
- [x] `dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1`零警告错误；`dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1790 --max-threads 4`：1790/1790，零失败跳过，2分35秒，冻结hash与diff通过。证据 `docs/verification/2026-10-08-wrong-answer-backend.md`，首次中文提交；Task2页面尚未完成。

### Task 2：单窗口列表、详情与原练习页

**Files:** 新增 `Presentation/ViewModels/WrongAnswerListViewModel.cs`、`WrongAnswerItemViewModel.cs`、`Views/WrongAnswerListPage.xaml/.cs`；List中展开Detail不弹窗，复用现有喇叭能力。添加首页入口、现有导航/注册/App模板；新增 `Practice/WrongAnswerPracticeWorkflow.cs`、`PracticeSessionMode.WrongAnswers`；测试 `Tests/Presentation/WrongAnswerPresentationTests.cs`。

**Interfaces:** 首页 `IHomeNavigation.OpenWrongAnswersAsync(language,token)`；列表通过最小导航能力打开重练(选中Review组ID)或继续原学习；新workflow仅替换Start，查询/作答/浏览组合现有Review workflow；不继承，不另造练习页。页面Scope生命周期及动画沿用现有导航。

- [x] STA真实XAML+DI覆盖首页语言→List→Detail→复习重练→反馈/总结→返回，含列表最上方全选、取消、空选择、空库、错误提示；学习按钮回原会话，浏览无数据副作用，主题资源随系统/手动切换。
- [x] 冻结后生产临时错误证明模式／取消及真实未到期重练断言失败；恢复正确实现，9/9通过。新增页面内详情按选择显示词形、中文、历史文本、题型、字形、日期和到期状态，不宣称已完成OCR人工覆盖。
- [ ] 构建、提高基线后的全量测试、实际链路冒烟、冻结hash与diff通过后第二次中文提交；完整计划唯一最终只读审查，重要问题集中RED→GREEN修复，不重复审旧计划。验证结论持久化，提交后精确清理本计划临时目录。

其它五十音、MCP、统计打卡、更新发布及最后OCR仍属于完整GOAL，本计划完成不代表产品完成。
