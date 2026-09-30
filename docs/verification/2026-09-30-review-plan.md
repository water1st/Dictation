# 独立复习计划验证记录

## Task 1：业务与持久化

在原 `D:\Source\Repos\Dictation`、`codex/dictation-refactor` 执行，基线 `d16d64b`。沿用此前已验证的词库和记忆，不重新实现或重复其 RED。日英复习计划独立保存；只共享纯选项校验与英语例词解析，不共享学习计划数据。

迁移 8 增加复习计划主表和四个有序选项／范围子表。读取使用一个延迟只读事务；保存使用同一写事务校验词组的实际语言及 Review 位置，再替换主表、选项与范围。失败由 SQLite 事务释放自动回滚。删除整组级联删除记忆、学习进度、答题历史和计划范围；取消勾选仅删除范围关系。

API 构造适配和旧迁移版本／表清单在冻结前完成。词形校验与范式解析从学习服务提取为共享具体组件，通过唯一 DI 组合；不增加接口或运行依赖。已有学习服务行为由全量回归验证。

### 编译成功的行为 RED → GREEN

测试项目构建 **0 警告／0 错误**。空保存／读取／删除骨架真实执行：Core **39 项，39 个断言失败**；DAL **17 项，17 个断言失败**，均无跳过。代表失败是默认目标期望 20 实得 0、读回计划为空、缺少参数异常、取消未传播、失效组未拒绝、没有级联删除；不是编译失败、零测试或基础设施错误。

冻结后只改生产实现，聚焦 **39/39、17/17**。相同 11 个测试文件 SHA-256：

| 文件（`Dictation.Tests/` 下） | SHA-256 |
| --- | --- |
| `Core/ReviewPlanServiceTests.cs` | `66D97326CEE4AB383B1CF40369D1E2EF8441834045A43CCE7B3E1D32BF167B0E` |
| `DataAccess/ReviewPlanDataAccessorTests.cs` | `335AF7F395C141BA2C7D61207F0F287E40614050375A074D777F868F4E2DE5C0` |
| `Core/LearningPlanServiceTests.cs` | `6BF76A6F1EF60594EEC82F11085C898B9BCDBCF805C8F9D09D0551B3A8F23805` |
| `Presentation/LearningPlanViewModelTests.cs` | `FC7A79DD18402B806A3A321794E50FBCCB65D1B8C067F1E75603D6CEDE84448B` |
| `Core/LearningQuestionSchedulingTests.cs` | `374818E9938D06FD2DCCD847757259606CF34E5FDF3DE2188D0AACB751E15C6C` |
| `Core/LearningSessionServiceTests.cs` | `AAE9FC70700103BFD5AC406F100D932B132F6CFFBBC0ECE3C168DF43D8DA0EBB` |
| `Core/StartupInitializerTests.cs` | `68EA8AF528E2C35D51FA572DF300BB4DFF0E51BDC1FF4A4A894ABF122194D299` |
| `DataAccess/SchemaMigrationRunnerTests.cs` | `B9146FAD7F2063ED7252857CF6199B62A44A3629F2AAFF474C7A3D8A5CD7DE19` |
| `DataAccess/LearningPlanDataAccessorTests.cs` | `4464713CD79DAB053AEF3D99BBE6FE4311466A8C2C7D614B8B5F344AEBF17BC3` |
| `DataAccess/LearningSessionDataAccessorTests.cs` | `BB65F82369EE94A5C88F28B4FEA9131187387DFB627E761E3288C9C1D252D350` |
| `DataAccess/StableImportMetadataTests.cs` | `18A062B48BC4F7A3683090C9A33124C235EA806861F12C4F6B8DD31EEFE4C1CB` |

真实 SQLite 测试验证失效选择、错误语言／位置、英语示例缺少词形、取消、子表触发器写入失败回滚、整组删除与范围取消的不同效果。保存复习计划前后核对既有记忆的稳定度、次数和到期日，并确认学习计划未改动。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*ReviewPlanServiceTests' --minimum-expected-tests 39
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*ReviewPlanDataAccessorTests' --minimum-expected-tests 17
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1616
git diff --check
```

最终 solution 构建 **0 警告／0 错误**，全量 **1616/1616**、失败／跳过均为 0，静态 diff 检查通过。源码无新增数据库；用户下载资源及现有预览进度未被使用或覆盖。Task 2 页面仍待接通，不能把本阶段服务验证描述为 UI 已交付或整个 GOAL 完成。
