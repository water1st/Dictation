# 统计与日历验证记录

## 范围与进度

计划：`docs/superpowers/plans/2026-10-12-statistics-calendar.md`，用户于 2026-10-12 回答 y 批准，主目录 `D:\Source\Repos\Dictation`、分支 `codex/dictation-refactor`、起点 a756852。

Task 1 的 Core／DataAccess 最小实现与首页接入已验收；月历与报表仍未实施。此记录不代表完整 GOAL 已完成。

## 严格行为 RED → GREEN

测试使用真实 SQLite、原完成表和 DI；不以编译失败或零测试作为 RED。

| 冻结测试 | SHA-256 |
| --- | --- |
| DailyCheckInServiceTests.cs | AD7A0027A574E20F84DA911572E064BAFAD7927F28E5A1CD478001303BC335C2 |
| DailyCheckInDataAccessorTests.cs | 7A2348256A3A2787BE82F866EB6038EF8985817B7432F7433DF09518D3539BF9 |
| DailyPlanSnapshotServiceTests.cs | 0607FAAE5DAD648760E608C836FAAF556F428670378A3E6926FD0A7AFC374EB5 |
| DailyPlanBoundaryTests.cs | 34C23E8A2C818990A9AF028C170599E3517C38EC85C76BD4270CBFB300063271 |

1. 打卡实现暂时恒返回 false：测试项目 build 0 警告／0 错误；21 项实际执行，6 项因期望 True 实得 False 失败，15 通过。只改实现，保留冻结测试。
2. 每日候选暂沿用每轮截断：build 0 警告／0 错误；8 项实际执行，`RecordToday_DoesNotCapAvailabilityAtSessionLimit` 期望 2 实得 1，1 失败／7 通过。修正实现复用未经截断的适用候选，测试不变。
3. 跨午夜直接答题未记录次日目标：build 0 警告／0 错误；4 项实际执行，Review 与 Kana 两项 `Assert.NotNull` 失败，2 通过。只在实现补直接作答／恢复待答题的记录调用。

跨日测试最初误用学习读取接口导致编译失败，以及 Kana 测试解引用空对象导致异常，两轮均明确作废；修正测试合同和空值断言、重新冻结后重新取得上面的合格行为 RED，未通过改业务期望转绿。

## 最小实现验证

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*DailyCheckInServiceTests' --filter-class '*DailyCheckInDataAccessorTests' --filter-class '*DailyPlanSnapshotServiceTests' --filter-class '*DailyPlanBoundaryTests' --filter-class '*SchemaMigrationRunnerTests' --minimum-expected-tests 46 --max-threads 4
```

解决方案构建 0 警告／0 错误；聚焦实际 46/46，通过且 0 跳过。该轮同时验证了独立临时数据库迁移、并发首次写入、重新创建 DataAccessor 后读取、真实 Core 启动／恢复与跨午夜直接提交；这些是运行冒烟，不是只看源码。

最初一次聚焦命令使用了框架不支持的中间通配符，运行零项，已作废并改用上述完整类名后缀重新执行，没有降低门槛。

首次全量实际 1943 项，1935 通过、8 失败。失败分别为 StartupInitializerTests 的三个默认迁移清单、LearningPlanDataAccessorTests／LearningSessionDataAccessorTests／StableImportMetadataTests 的旧最大版本 13 断言，以及 LearningSharedMigrationTests 模拟 v11 时遗漏删除新表。仅适配 schema14 元数据和完整旧版夹具，业务断言保持原值。此轮不作为通过证据。

适配后重新冻结最终测试版本，并临时制造四处生产行为错误（不打卡、按每轮截断、Review／Kana 直接跨日提交漏记目标）：build 0 警告／0 错误；33 项实际执行，9 项预期行为断言失败、24 通过、0 跳过。随后仅恢复四处生产实现，冻结的四份新增测试 SHA 与上表完全一致。StartupInitializerTests.cs 适配后冻结哈希为 `75A713E43A2DFABB957914663390719334DDFB5F7D572990257083EFD6321E9E`。

迁移适配后的阶段全量为 1943/1943、0 跳过。随后核对直接作答的对称调用点，补 `LearningDailyCompletionBoundaryTests.cs`（SHA `CB13F1B3ABF3539E97A5C94D0644419EF9964033B6068255D8ACC8C1C214ADA2`）：build 0 警告／0 错误，1 项因次日快照为空而 `Assert.NotNull` 失败；只修 Learning SubmitAnswerAsync 后，同一测试 1/1 转绿。

**首个最小实现提交的最终验收：**`dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1` 为 0 警告／0 错误；`dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1944 --max-threads 4` 实际 **1944/1944，通过、0 跳过**，4 分 27 秒。冻结业务测试未变，所有临时 mutation 已恢复。完整输出保存在本计划的 `.superpowers/sdd/2026-10-12-statistics-calendar/`，永久结论与哈希保留于本文件。

## 首页接入证据

Core 最小实现提交为 `5e9d23b`。首页接入使用窄能力 `IDailyCheckInReader`，保留真实服务的判定结果，页面不重算达标规则；增加同语言请求版本隔离、语言切换隔离、取消／页内错误、首次达标反馈和重启不重复庆祝。

最初首页可编译骨架：0 警告／0 错误，8 项执行、5 个呈现行为断言失败、3 通过。初次转绿 32/32；自查跨语言夹具发现英语原本也未达标，旧日语覆盖仍可能使测试通过。该轮证据作废，将夹具改为已达标英语／迟到未达标日语，重新冻结后移除生产成功路径的语言与版本屏障，10 项执行、2 个逆序／覆盖断言失败、8 通过。仅恢复实现屏障转绿。

首次完整 1954 项回归为 1953 通过、1 失败：旧架构白名单没有列入新增的只读接口。只适配首页能力清单，不允许注入写接口或 DAO；最终重新冻结并完整重跑编译成功的行为 RED：10 项执行、2 个覆盖断言失败、8 通过、0 跳过，随后仅恢复实现。

| 最终冻结文件 | SHA-256 |
| --- | --- |
| HomeCheckInTests.cs | 647C326E1ED52377F374D45C7699CCCD38401E646D8515C7134663FB242C5434 |
| HomeCheckInRenderingTests.cs | E308E3CB0DD9B7C6AD81B767ADE4D90256F3FFD1B66CB793EDA2697D070F6B94 |
| CoreLayerArchitectureTests.cs | C9FB3955E162EBA9A6CAE43359198A6E1CE95125755399D435598AD9B26535A7 |

最终聚焦 `HomeCheckInTests`、`HomeCheckInRenderingTests`、`CoreLayerArchitectureTests` 为 **20/20**，解决方案构建 **0 警告／0 错误**。此前连同旧首页设置及进度回归为 34/34，未删除或跳过测试。真实 STA 控件、原 DI／SQLite 与返回导航完成浅深色冒烟，生成截图并目视检查；测试使用隔离数据库，没有修改用户数据或实际打开新窗口。

截图：`images/2026-10-12-checkin-home-light.png`、`images/2026-10-12-checkin-home-dark.png`。

第二次提交最终验收：`dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1` 0 警告／0 错误；`dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1954 --max-threads 4` 实际 **1954/1954，通过、0 跳过**，4 分 40 秒。最终冻结哈希不变，语言／版本屏障已恢复，无临时红状态。`git diff --check` 通过，用户 Resources 未修改。
