# FSRS 记忆与复习队列验证记录

本计划分片执行；当前完成数值内核和学习完成后的原子记忆初始化，不表示复习答题、队列页面或每日计划已经交付。最终验收仍在原 `Dictation` 目录，OCR 后置。

## 数值内核

固定参考来源与七组官方黄金向量见 `docs/resources/FsrsReference.md`。`FsrsSchedulerTests.cs` 冻结 SHA-256 为 `D1BCD41C89A924E03AB88EC5C59646F61A04024AB59A6CEDB8C76A456DDABFCD`。

可编译生产骨架返回错误稳定度／计数：测试项目构建成功，0 警告／错误；实际执行 30 项，25 个数值、非法输入、时间或状态断言失败，5 项通过。此后不改测试，仅移植生产公式，聚焦 30/30；冻结哈希相同。

全量 **1524/1524**，无跳过；solution Debug 构建 **0 警告、0 错误**。随包 MIT 许可证已出现在 WPF 输出 `LICENSES/py-fsrs.txt`。算法是内部纯计算组件，无 Python／网络运行依赖、额外调度服务或新 NuGet 包。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*FsrsSchedulerTests' --minimum-expected-tests 30
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1524
```

## 学习完成的原子记忆初始化

学习服务筛选本次完成词组的启用形式，按本会话该成员／词形是否曾错误计算首次 Good 或 Again。Mapper 只转换数据；DataAccess 使用原答题连接及事务写入缺失记录，不另开事务，不覆盖已有稳定度、到期日或次数。迁移 7 增加 `ReviewFormMemories`，成员词形唯一、数值约束和词组／成员删除级联。

专门测试使用真实 DI、离线测试词库及 SQLite：双形式一错一对分别初始化、重练不抹去错误、错误不跨成员传播、无作答不伪造记忆、补充学习保留原状态、直接导入复习保持待测、取消不写入、所属关系校验、整组删除级联，以及第二条写入／最终答题失败的事务回滚与重试。

第一轮全量发现遗漏的旧迁移版本／表清单适配，1537 项中 7 项失败；该轮不作验收证据。补齐结构适配后重新冻结；新测试与夹具从最初写入起未改动任何断言。重新编译成功后，仅在生产实现临时清空种子计划器、调用范围和 writer 的遍历范围：学习 8 项中 **6 项行为失败**，事务 5 项中 **4 项行为失败**（已取消令牌检查仍通过）。失败均为缺少应有记录／校验／异常，不是编译或基础设施错误。只恢复生产实现后，相同测试 **8/8、5/5**。

冻结 SHA-256（RED 与最终 GREEN 相同）：

| 文件（`Dictation.Tests/` 下） | SHA-256 |
| --- | --- |
| `Core/LearningMemorySeedTests.cs` | `70794949B6F817734365764B047796EADCD8B06095F50463EF7F1F28940AC279` |
| `DataAccess/ReviewMemoryTransactionTests.cs` | `B4C02C4D1E39D304D4E126AC3F07DADE5BF2D0B58CE61CD3A069612AD879E20A` |
| `Infrastructure/MemorySeedTestEnvironment.cs` | `B2BB5F7E18385DA2C8E4BFE2F216764813ECF892899128A4D0D984847C94B246` |
| `Core/StartupInitializerTests.cs` | `13AE9F4E40C323F2E0716F5D6AC4BA57845DCCF4F92A88E432A7404F939CB2EA` |
| `DataAccess/StableImportMetadataTests.cs` | `7DF9F2826813E8867B6DF59A615EDB2E25A74DA56DBF2190016EBCBA7C32EFE8` |
| `DataAccess/LearningSessionDataAccessorTests.cs` | `DE6804C48DC077B13D7E266B92AC45595D89F632A6EC39347DE7D462E6E3D4CF` |
| `DataAccess/LearningPlanDataAccessorTests.cs` | `AC59FFE5E56FE3A1FFB1FD3B38FF9414A05D7407BF36B9A3DD58DBBB8B1ED05F` |

全量 **1537/1537**、无跳过；solution Debug 构建 **0 警告、0 错误**；`git diff --check` 通过。真实学习事务测试覆盖答题到持久化链路，包括触发器模拟保存故障后检查答案仍待答、进度与 Placement 未推进、记忆为空，再重试成功。没有发布或改写原目录预览的用户数据库，也未新增依赖。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*LearningMemorySeedTests' --minimum-expected-tests 8
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*ReviewMemoryTransactionTests' --minimum-expected-tests 5
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1537
```

## 待接通的下一步

接通风险复习词库页面，并完成本计划独立最终审查。复习答题、错题、五十音、MCP、统计、完整 ZIP 更新发布和 OCR 仍属于未完成 GOAL，不在本计划现有证据中声称完成。最终完成原 `Dictation` 目录整合后，只清理本次创建的临时工作区和备份，不影响其它项目或用户数据。
