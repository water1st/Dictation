# 复习当前题目切片验证

目录：`D:\Source\Repos\Dictation`，分支：`codex/dictation-refactor`，起点：`30dfdcc`。

## 范围与复用

- 新增 `IReviewQuestionReader`，按已保存的复习计划生成当前题目，并在 SQLite 中持久化、并发唯一创建及重启恢复。
- 迁移 10 独立保存复习题目和本轮词形状态，不读取或修改学习成绩。读题不评价 FSRS、不增加完成数。
- 将已有调度实现移为 `PracticeQuestionScheduler`，没有重写算法；已有题面和答案选择规则集中到 `PracticeQuestionPresenter`。两种模式使用相同题面合同和纯状态类型，但不共享数据库成绩。
- `IPracticeQuestionPlan` 仅暴露形式、渠道、字形及标签选项；学习和复习各自的计划对象提供该能力，不相互调用计划服务。
- 学习模式的消费者及旧测试做类型位置适配；没有创建兼容类、第二套容器或新增依赖包。

## 测试证明

17 项新增真实三层测试覆盖中文题面及变形要求、看义写词不携带发音答案、礼貌形听写读 `たべます`、罕见汉字排除、英语本词元词形、计划快照、容器重建恢复、并发创建、回滚重试、整组删除、完成会话、无成绩副作用、非法输入及取消。

首轮全量发现旧架构测试仍通过 `Learning.PracticeQuestionDto` 查找已移动的类型；1697 项通过、1 项失败。只将该类型定位修正为 `Practice.PracticeQuestionDto`，作废首批 RED 证据并重新冻结，不修改新业务期望。

重新冻结后的测试 SHA-256：

- `ReviewQuestionServiceTests.cs`：`07FFD9C9AA91251E761F625C6D0FEC13B2C98EDC5B239EC1ED2F7B7386B30895`。
- `CoreLayerArchitectureTests.cs`：`46DAEB148D60004BB90ABAEABDD95B0109F5273BEA74DDEF45D510F5F5BE1AD7`。

编译成功、0 警告及错误后，仅临时让生产读题方法返回空；17/17 实际执行并因行为断言失败，0 跳过。随后只移除生产临时错误，两份测试哈希保持一致。

验证命令：

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*ReviewQuestionServiceTests' --minimum-expected-tests 17 --max-threads 4
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1698 --max-threads 4
git diff --check
```

最终构建成功，0 警告、0 错误；全量 GREEN 1698/1698，0 失败、0 跳过，耗时约 1 分 49 秒；冻结哈希保持一致，差异检查通过。临时故障已移除，不提交 RED 状态。

当前只是计划 Task 2 的读题部分，客观答案提交、重练、原子 FSRS 和复习 UI 尚未完成；完整 GOAL 继续执行。
