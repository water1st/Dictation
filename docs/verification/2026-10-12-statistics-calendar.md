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

## 月历与历史事实最小实现

Task 1 首页提交为 `c048b6f`，Task 2 从该提交继续。原学习进度只保存最后完成日，不能用来保留补学之前的历史；迁移 15 增加只有词组 ID／本地日期的完成事实表，原答题事务插入、日期去重、整组外键级联删除。升级仅回填现有的已知日期，不猜旧轮次或发明缺失历史。没有复制答案、掌握或 FSRS 状态，也没有引入读写分离或新框架。

| 冻结测试 | SHA-256 |
| --- | --- |
| CheckInCalendarServiceTests.cs | F24854C66177E69994126ADFE83C3323389E9B4D018FBC5EE26158278A93B22A |
| LearningCompletionHistoryTests.cs | FB535DF72E822CA9AC535F8D4C510B64D7CD3BADFE9D5B1F5A6CDA2C78F815CB |
| CalendarMonthQueryTests.cs | AD596DBF8798295AF6CC5D138C2FCB87B77FDF105D38B4DC017D08800D1CC7E3 |

- 月历可编译骨架的 14 项中，5 项因缺少完成计数／徽章而行为断言失败。
- 历史删除测试先补“事实确实存在”断言并重新冻结；4 项历史测试在 build 0 警告／0 错误后全部以值断言失败，未把空表删除当有效验证。
- 直接 SQL 边界冻结后，临时将最大月份上界改为排除 `9999-12-31`；build 0 警告／0 错误，3 项实际执行、1 项 `Assert.Single` 失败、2 通过。仅恢复上界，不改测试。
- 首次实现测试暴露 SQLite 空查询／UNION 列类型被推断为 Byte[]，Dapper 位置 record 无法匹配构造函数。使用仓库已有的无参可赋值 PO 方式，消费者只读、setter internal，不手搓反射映射，不修改测试期望。

恢复实现后，月历／历史／直接 SQL 新增测试 **21/21**；连同每日资格、迁移与启动曾验证 **49/49**。解决方案最终构建 **0 警告／0 错误**，测试冻结哈希保持不变。临时数据库验证了真实答题保存、补学不抹旧日、升级回填、双语言独立计数、同日去重、删除、闰年及最大日期边界。

提交前全量命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1975 --max-threads 4` 实际 **1975/1975，通过、0 跳过**，4 分 54 秒。`git diff --check` 通过，临时 mutation 全部恢复。月历 UI 及报表仍未验收，不能将此最小实现当完整计划或 GOAL 达成。

## 单窗口月历页面

月历页面复用原导航栈、页面作用域和转场；只有首页可进入，进入后语言固定。返回动画完成才释放旧页面，已释放的页面不再发布查询结果。页面提供上月、本月、下月和刷新操作，边界月份禁用越界按钮；周一首列，六行保持稳定。

最终冻结 `StatisticsCalendarTests.cs` SHA-256：`3650468E79447BE88C5ED9204488A9B2776AF551CE1504300B706ECADA0CE214`。初次组件属性名与 WPF 的 `Language` 冲突，改为 `SourceLanguage` 并在冻结前适配测试合同，该次旧证据作废，不用 `new` 掩盖框架成员。

有效 RED：构建 0 警告／0 错误，8 项确实执行，5 项因生产骨架为空月格而值断言失败，3 项通过、0 跳过。仅补生产月格对齐和日期绑定，同一批测试 **8/8 转绿**，测试哈希不变，解决方案构建 0 警告／0 错误。

真实 DI、SQLite 和 STA 控件验证了返回作用域、闰月、最大日期、迟到月份响应、取消、页内错误及浅深色绑定。白圈红心与圆形米字旗由 WPF 几何绘制，避免系统国旗字体不支持；两图标各自读 Core 的判定，UI 不计算打卡规则。

截图 `images/2026-10-12-calendar-light.png`、`images/2026-10-12-calendar-dark.png` 已生成并目视检查；使用临时数据库，未写用户数据。完整全量回归命令的门槛为 1983，实际 **1983/1983，通过、0 跳过**，4 分 50 秒。冻结测试哈希不变，差异检查通过。月历任务完成；完整 GOAL 的其它验收项仍须真实完成，不能用本次绿灯替代。

## 词组与记忆报表最小实现

报表按现存学习完成事实去重，而非导入量或词形数；当天补学保留早期完成日，整组删除撤回历史计数。只加载一份原复习词库快照，已有客观词形由原 FSRS 算法预测：每组取最弱已测形式，再按组等权平均。没有记忆时保持率为空，不展示 100%；到期图按组最早到期日分桶，逾期与待测独立计数。原错题查询的真实目标观察按组汇总，未复制题目表或建立新错题存储。

| 最终冻结测试 | SHA-256 |
| --- | --- |
| LanguageStatisticsServiceTests.cs | 20186189C37608E8DECFB77E53CF64E8C80950510D99309CFC20D46B73871489 |
| LearningStatisticsDataAccessorTests.cs | 9FEEB73860A1D4AB5D04329748090089D06A2A7055429AE6CB939DB3798BAEC7 |

DAO 9 项测试在编译成功后有 4 项计数断言失败。Core 初次时区测试直接索引空图，该异常不算合格 RED；先补图点数量断言、保持日期期望不变，重新冻结后构建 0 警告／0 错误，14 项实际执行、13 项业务断言失败、1 项通过、0 跳过。随后仅实现查询、纯 Mapper 和服务编排，同一批合计 **23/23 转绿**，哈希不变；解决方案构建 0 警告／0 错误。

真实 SQLite、原学习／共享听写与删除服务验证了完成整组只计一次、共享错误不重复、删除撤回、语言与五十音隔离；数值测试覆盖保持率单调下降、未测词形不制造记忆、损坏状态拒绝、Top 10 稳定排序、本地时区和首次到期去重。提交前完整回归门槛 2006，实际 **2006/2006，通过、0 跳过**，5 分 10 秒，构建 0 警告／0 错误。测试冻结哈希不变，差异检查通过。

## 单窗口报表页面

报表接入原 StatisticsPage，不新建窗口。卡片和错题列表只绑定 Core 结果；图表使用原生 Polyline／ItemsControl，只做坐标与高度适配。更换月份只查询月历，不重读与月份无关的词库统计；整体刷新同时更新两者，各自错误留在页面内。

最终冻结 `StatisticsReportTests.cs` SHA-256：`F21BA1102E3DD4B830F4AFB7FE98111736EB8433DD0952C4796941A9F40ED63F`。预构建的主题类型名错误不算 RED；冻结前适配为实际 ResolvedTheme，并使用同义 Assert.Single 消除新增分析器警告。最终有效 RED：构建 0 警告／0 错误，10 项实际执行，7 项因成功路径未发布报表而行为断言失败，3 通过、0 跳过。只恢复生产结果绑定后，报表与月历联合 **18/18** 转绿，测试文件未变。

真实 DI＋SQLite 读取已学计数；STA 页面验证固定语言、失败重试、取消、逆序结果、作用域释放和空记忆。浅／深色 960 宽及深色 760 宽截图已目视检查，统计卡片可换行，图表无横向截断，标明“FSRS 预计保持率／假定不再复习”。截图：`images/2026-10-13-report-light-960.png`、`images/2026-10-13-report-dark-960.png`、`images/2026-10-13-report-dark-760.png`。同一运行刷新了月历截图以反映当前完整页面，未写用户数据。

解决方案构建 0 警告／0 错误；提交前完整回归 **2016/2016，通过、0 跳过**，5 分 50 秒，冻结测试哈希不变。最终一次只读审查发现跨日活动轮次的目标包括昨日已完成组，必须补回归修复；页面本身通过验收不等于整个 GOAL 完成。

## 最终审查修复

本计划只进行一次独立只读审查，结果为 1 项 Important、0 Critical、0 Minor，没有未裁决事项。实际缺陷是跨日恢复时，把昨日已完成的组随完整活动轮次并入今天目标，导致实际只剩一组却冻结为两组、无法打卡。

补充 `CrossDayRemainingTargetsTests.cs`，最终 SHA-256 为 `FC81C25A769EAEE21FD21FD6A31830449B9B8F64D328108C30B6089B9B3FBEBF`。初次夹具误用看义题的空 SpokenText，未产生昨日完成事实，故作废而不计 RED。改用两个不同读音的真实听写、保持业务期望，重新冻结后构建 0 警告／0 错误；学习、复习两项均在今天目标处 **期望 1、实际 2** 断言失败。

仅修生产实现：学习原候选已包含活动未掌握组，取消冗余完整轮次合并；复习追加只读未完成组查询。原完整活动轮次仍含两组，浏览与结算合同未改变。冻结测试转绿并实际继续完成剩余组，今天完成数为 1、打卡为 true。与原每日目标／跨日用例联合 **14/14**。

最终解决方案构建 **0 警告／0 错误**；完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2018 --max-threads 4` 实际 **2018/2018，通过、0 跳过**，5 分 47 秒，测试哈希不变、差异检查通过。没有分派第二次审查。

本统计计划验收完成；原 GOAL 的更新／CI、最后 OCR 和最终交付清理仍需执行，不新增要求，也不提前宣称 GOAL 完成或结束。
