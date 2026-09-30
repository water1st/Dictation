# FSRS 记忆与复习队列验证记录

本计划分片执行；当前完成数值内核、学习完成后的原子记忆初始化与只读复习词库页面，不表示复习答题或每日计划已经交付。最终验收仍在原 `Dictation` 目录，OCR 后置。

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

## 风险队列查询能力

通过 `IReviewLibraryQuery` 复用实际 Core→DataAccess→SQLite 链路；延迟只读事务读取词组、共享成员快照与记忆，保持一次查询的快照一致性。中文释义映射复用已有词组 Mapper；FSRS 保持率只由 Core 计算。未测试旧词在最前，其后按最低预计保持率排序，同级依到期时间、创建时间及标识稳定排列。

组内日语汉字／假名变体共享成员词形记忆，英语 `go` 和 `convert` 保留各自范式但整个导入组仍计 1 词。查看全部词形不代表启用全部词形：有已有记忆的学习完成组，未学习的其它形式显示未评价，但不因此将该组提前到期；后续复习会话必须按真正启用的复习范围处理它们。整组从未评价才显示待测，没有伪造 Good 或学习历史。

`ReviewLibraryServiceTests.cs` 冻结 SHA-256：`1E8812405364CCE8A84659427C25FCE5C45354A7ECEDCE86B2377531BD16D0DA`。可编译空查询骨架执行 **12 项，10 项行为失败、2 项通过**；失败包括遗漏旧词、错误计数、语言／取消校验缺失、风险队列为空。冻结后只改生产实现，相同哈希 **12/12**，全量 **1549/1549**，0 跳过，solution 构建 **0 警告、0 错误**，`git diff --check` 通过。测试还验证查看不改变稳定度、评价次数、到期日或答题历史，以及删除组后队列与记忆同时移除。

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*ReviewLibraryServiceTests' --minimum-expected-tests 12
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1549
```

## 单窗口复习词库页面

首页固定当前语言进入圆角词组列表，选中后展示各成员词形的记忆详情。页面复用唯一导航容器、页面作用域和转场释放；先呈现页面再后台读取 SQLite，读取中可以取消或返回，取消后即使业务返回较晚也不能回写旧页。刷新保留所选词组，读取异常显示可重试状态；无第二窗口、语言切换或假答题按钮。

`ReviewLibraryPageTests.cs` 冻结 SHA-256 `C8900373AB089D04C00C83173495622D00943A3979769876CECA38E1F5185983`。API 编译适配后，可编译页面／导航骨架实际执行 **10 项，10 个行为断言失败**；随后只修改生产实现，**10/10**。恢复工作时不重写已验证查询或重复 RED，继续原冻结批次。

STA 真实模板渲染发现 ListBox 继承系统前景色。补 `ReviewLibraryThemeTests.cs` 并冻结 `B4A6722252B71E474A0C51DC91D645D85367810F99618CFAD15D784EE73BA813`：先构建 **0 警告／错误**，实际 **1 项失败**（期望主题色 `#FF1F2937`，实际系统黑色 `#FF000000`）；只补生产 ListBox 动态前景色后同哈希 **1/1**，包含浅色→深色切换与选中卡片。未修改原冻结页面测试。

正式词库冒烟使用既有两个 schema v2 数据库的只读硬链接和独立空用户数据库，实际从 DI 调用导入、确认学习、计划、作答、记忆查询、WPF 页渲染与返回：日语 `jmdict:1358280:1`（吃／食用）2 次正确作答，英语动词 `go%2:38:00::`（离开／移动／行动／行进／运行）2 次正确作答；两种语言分别计 1 组、1 个已评价形式，下次日期均为 2026-10-02，页面返回成功。冒烟工具最初将 Latin 输出方式误当词库 ScriptCode，造成取不到英文答案；仅修工具的 SQL 过滤，未改生产实现或测试。正式词库未重新生成，原预览 `DATA/UserData.db` 保持 196608 字节及 2026-09-29 17:41:03 修改时间。

页面完成后的全量 **1560/1560**，失败／跳过均为 0；solution 构建 **0 警告、0 错误**；两个冻结页面测试哈希相同，`git diff --check` 通过。独立最终审查在本计划收尾时补充。未做人工点击验收；界面证据为 STA 绑定、实际模板渲染及正式资源端到端冒烟，不将这些描述为用户手工验收。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*ReviewLibraryPageTests' --minimum-expected-tests 10
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*ReviewLibraryThemeTests' --minimum-expected-tests 1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1560
```

## 待接通的下一步

完成本计划独立最终审查后继续复习答题。复习答题、错题、五十音、MCP、统计、完整 ZIP 更新发布和 OCR 仍属于未完成 GOAL，不在本计划现有证据中声称完成。最终完成原 `Dictation` 目录整合后，只清理本次创建的临时工作区和备份，不影响其它项目或用户数据。
