# 日英独立统计与打卡日历实现计划

> **执行要求：**批准后使用 `superpowers:executing-plans`，由主代理在当前会话串行实施；不分派实施 worker。步骤使用复选框追踪。最终按既定流程进行一次独立审查。

**状态：**2026-10-12 用户 y 批准执行，采用主代理串行实施；完成状态按下列复选框与验证记录核对。

**目标：**在现有单窗口中交付日英独立打卡、月历、已学词组数、易错词和可解释的记忆／到期趋势。

**架构：**复用现有学习、复习、五十音完成记录，不复制进度或另建错题系统。Core 组合查询与既有 FSRS 算法，DataAccess 使用 Dapper 读取事实、保存每日目标快照，Presentation 仅导航、绑定和渲染。保持一个 Generic Host／DI 容器。

**技术：**.NET 10、WPF、现有 Dapper／SQLite、TimeProvider、xUnit v3；不新增包。

**规格：**`docs/requirements/RefactorSpecification.md` 第 4、5、7、10、11 节及 `AGENTS.md`。

## 全局约束

- 最终验收目录为 `D:\Source\Repos\Dictation`，沿用 `codex/dictation-refactor`，不修改 master、不推送。
- 中文 UI／注释；一文件一个主要类型；纯转换归 Mapper；不引入 DDD、CQRS、Repository 或通用映射框架。
- `AddDictationCore()` 是唯一业务注册入口；实现尽量 internal，跨层能力合同按实际消费者拆分。
- 数据只写 `DATA/UserData.db`；不改只读词库、用户原始 Resources 或现有预览中的用户数据。
- 一组只计一个词，重试和多词形不膨胀数量；五十音独立计项目，不能混入词汇总数。
- 使用统一 TimeProvider 的本地日期；完成日不是轮次启动日。
- RED 必须编译成功且行为断言失败；冻结测试后只改实现转绿。每个切片先最小实现提交，再端到端验证提交，摘要中文。
- 旧已验证模块不重做 RED；每次产品提交前构建、全量测试及该切片冒烟通过，基线不少于 1910 项。

## 实现约定（请随计划一并审核）

1. 每种语言有自己的当天任务。学习／复习没有新增启用开关；无适用任务时该项有效目标为零。五十音沿用已有 Enabled。
2. 当天首次启动或恢复非空练习时，保存该语言各模式的有效目标快照。有效目标为计划上限与当日已完成加适用可练数量的较小者；已完成与可练组先按 ID 去重，不能相加重复词组。复习只计算计划范围内启用且适用的到期／待测形式所属词组，不受“每轮抽取数”截断。未完成的活动轮次也计入可练数量。
3. 快照只保存日期、语言、三个目标数，不复制词条、答案或完成数。同日重复启动不覆盖快照；之后改计划仍按现有行为出题，但不追改当天或历史打卡门槛，新门槛从次日使用。
4. 达标判定读取现存完成事实：所有非零目标达到，且该语言当日至少实际完成一组／一个五十音项目。仅浏览、启动空轮次、错误后未完成或无任务不打卡。
5. 整组删除沿用已有级联删除；报表、日历从剩余记录重算，不保留被删除词组的完成数。删除导致当天历史完成数低于原目标时图标取消。不得使用当前计划给升级前没有目标快照的日期补造打卡。
6. 日历同时显示日英两个圆形徽章；报表只显示进入页面时的语言，切换仍只能回首页。复习不足量示例显示“实际任务 5/5（计划 20）”，避免把任务不足显示成失败。
7. 记忆曲线明确标注“FSRS 预计保持率”，不是医学测量或历史正确率；展示从当前记忆状态推算、假定不再复习的未来 30 天趋势。每组取已有记忆形式的最低保持率，语言曲线对这些组求平均；完全未测的组单列，不填成 0% 或 100%。到期图展示未来 14 天的不同词组，每组只按最早一次到期归桶，并单列逾期／待测，不能把词形当词数。
8. 完成反馈只在当天首次从未达标变成达标时播放简短页面动画，不弹窗、不加积分系统；重开软件可看到徽章但不重复庆祝。

## 审查重点

1. 空数据库、零到期、全部范围不适用：没有实际完成则没有徽章，不能硬塞未来词。
2. 多形式、多轮、共享听写和错题重练：同语言同日同组最多计一次；不重复消费共享观察。
3. 跨午夜、重启、月末／闰年：按实际完成日读取，目标快照不因当前计划改变；无证据不补历史。
4. 删除整组及同日修改计划：删除记录立即反映到统计，历史不能继续显示已删除的完成数。
5. 异步刷新逆序／取消、读取失败和主题切换：旧语言或旧月份结果不覆盖当前页面；错误展示在页内，不伪造零数或打开新窗口。

## Task 1：真实每日目标与打卡入口

**文件：**
- 新增 `Dictation.DataAccess/Statistics/StatisticsSchemaMigration.cs`（迁移 14）、`DailyPlanSnapshotPo.cs`、`DailyCompletionCountsPo.cs`、`DailyCheckInDataAccessor.cs`。
- 新增 `Dictation.Core/Statistics/IDailyCheckInReader.cs`、`IDailyPlanSnapshotRecorder.cs`、`DailyCheckInDto.cs`、`DailyPlanSnapshotService.cs`、`DailyCheckInService.cs`、`DailyCheckInMapper.cs`。
- 修改 `Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`、`Dictation.DataAccess/DictationDataAccessServiceCollectionExtensions.cs`、`Dictation.Core/DictationCoreServiceCollectionExtensions.cs`，追加迁移，不重写前 13 个迁移。
- 修改 `Dictation.Core/Learning/LearningSessionService.cs`、`Dictation.Core/Review/ReviewSessionService.cs`、`Dictation.Core/Kana/KanaSessionService.cs`，在非空练习启动／恢复时调用窄能力记录当天目标。
- 将学习与复习现有适用性筛选提取为各自目录内的小型内部纯组件；保留原选词顺序和随机源，不额外建立通用规划接口。涉及 `LearningSessionSelectionEligibility.cs`、`ReviewSessionSelectionPlanner.cs`；统计与出题必须共用同一筛选，不复制业务规则。
- 新增 `Dictation.Core/Kana/KanaDailyAvailabilityService.cs`，提取现有五十音计划／完成 ID／记忆的只读适用项目查询，原 KanaSessionService 与目标记录服务组合复用。目标记录服务只依赖计划、DAO 和筛选组件，不注入学习／复习／五十音 SessionService 或其接口，避免 DI 循环。
- 修改 `Dictation.Presentation/ViewModels/HomeViewModel.cs`、`Views/HomePage.xaml`，展示当前语言当天状态与实际任务数。
- 新增 `Dictation.Tests/DataAccess/DailyCheckInDataAccessorTests.cs`、`Core/DailyPlanSnapshotServiceTests.cs`、`Core/DailyCheckInServiceTests.cs`、`Presentation/HomeCheckInTests.cs`；扩充原迁移／选词／恢复测试。

**能力合同：**
- `IDailyPlanSnapshotRecorder.RecordTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default) : Task`：仅为确有练习的语言创建当天目标，唯一键 `(LocalDate, Language)`；重复调用保持第一份目标。
- `IDailyCheckInReader.GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default) : Task<DailyCheckInDto>`：只读，不启动轮次或推进 FSRS。
- `DailyCheckInDto` 包含 Language、LocalDate、LearningCompleted／Target、ReviewCompleted／Target、KanaCompleted／Target、IsCheckedIn；计数使用 long，日期使用 DateOnly。
- DataAccessor 提供 `GetSnapshotAsync(string language, string localDate, CancellationToken cancellationToken = default) : Task<DailyPlanSnapshotPo?>`、`InsertIfAbsentAsync(DailyPlanSnapshotPo snapshot, CancellationToken cancellationToken = default) : Task`、`GetCompletionCountsAsync(string language, string localDate, CancellationToken cancellationToken = default) : Task<DailyCompletionCountsPo>`。SQL 负责事实计数，Core 负责目标和达标判断；目标暂未记录时首页显示“尚未开始”，不能展示成完成全部任务。

- [x] 写至少 20 项行为测试（DailyCheckInServiceTests 至少 10 项）：20 组计划／5 组到期，完成 4 为 false、完成 5 为 true；无任何完成 false；学习未达标阻止徽章；启用五十音未达标阻止日语而不影响英语；同组多形式只计 1；重复启动目标不变；活动轮次跨日恢复有当日快照；整组删除撤回相应事实。非法语言、取消、迁移回滚、并发 InsertIfAbsent 也必须覆盖。
- [x] 完成可编译骨架、冻结测试 SHA-256；仅临时将达标比较改成少算一组，或将空日判定改为 true；构建成功后运行聚焦测试，准确记录失败名称／期望／实际。
- [x] 恢复正确实现，同一批测试转绿；确认旧学习、复习、五十音选词及恢复行为回归通过。
- [x] 在隔离临时数据库验证迁移、重启和目标读取；构建／全量测试通过后提交“实现每日有效目标与客观打卡判定”。
- [x] 接入首页：返回首页刷新，只在未达标→达标时播放短动画；UI 不判定业务。编写真实 DI＋SQLite＋STA 首页测试，错误／取消不显示伪造徽章。
- [x] 运行聚焦、构建、全量与首页浅深色冒烟，提交“打通首页独立打卡与实际任务进度”。

## Task 2：同一窗口的双语言月历

**文件：**
- 新增 `Dictation.DataAccess/Statistics/CalendarDayCountsPo.cs`；在 `DailyCheckInDataAccessor.cs` 增加月区间只读查询。
- 执行核对发现原 `LearningGroupProgress.CompletedLocalDate` 只保存最后完成日，补学新词形会改写它。Task 2 追加迁移 15，增加只有词组 ID／日期两个字段的 `LearningCompletionDates` 历史事实表；原答题事务插入去重完成日，原外键级联删除。升级只回填现有进度的已知完成日，不能猜哪一旧轮次完成，也不补造其它日期。月历与报表读取完成事实与原进度兼容来源的去重并集，不复制进度、答案或 FSRS 状态。
- 新增 `Dictation.Core/Statistics/ICheckInCalendarReader.cs`、`CalendarMonthDto.cs`、`CalendarDayDto.cs`、`CheckInCalendarService.cs`、`CheckInCalendarMapper.cs`。
- 新增 `Dictation.Presentation/Navigation/IStatisticsNavigation.cs`、`ViewModels/StatisticsViewModel.cs`、`ViewModels/CalendarDayViewModel.cs`、`Views/StatisticsPage.xaml` 及其必要 code-behind。
- 修改 `Navigation/NavigationService.cs`、表现层注册入口、`App.xaml`、首页入口绑定；复用已有转场、返回栈和页面作用域，不增加 Window。
- 新增 `Dictation.Tests/Core/CheckInCalendarServiceTests.cs`、`DataAccess/CalendarMonthQueryTests.cs`、`Presentation/StatisticsCalendarTests.cs`；扩充原导航测试。

**能力合同：**
- `ICheckInCalendarReader.LoadMonthAsync(int year, int month, CancellationToken cancellationToken = default) : Task<CalendarMonthDto>`：返回该月每一天、日英各自完成计数与打卡标志；年 1–9999、月 1–12，非法值抛 ArgumentOutOfRangeException。
- `DailyCheckInDataAccessor.LoadMonthAsync(int year, int month, CancellationToken cancellationToken = default) : Task<IReadOnlyList<CalendarDayCountsPo>>`：单次只读事务查询目标与原完成记录；月区间采用起始含、结束不含，9999 年 12 月不得计算越界的下一年。
- `CalendarMonthDto(int Year, int Month, IReadOnlyList<CalendarDayDto> Days)`；每个 CalendarDayDto 含 DateOnly Date、JapaneseIsCheckedIn、EnglishIsCheckedIn。
- `IStatisticsNavigation.OpenStatisticsAsync(LearningLanguage language, CancellationToken cancellationToken = default) : Task`：只从首页进入，页面语言固定；同一 NavigationService 的实例别名。
- `StatisticsViewModel.Initialize(LearningLanguage language)`、`RefreshAsync(CancellationToken cancellationToken = default)`；提供上月／下月／本月 ICommand，边界月份禁用越界按钮。

- [x] 写至少 14 项测试：2028 年 2 月 29 天、月末边界、当月无记录全空、同格日英独立、重启不重算历史门槛、删除后图标与计数同步、升级旧数据不补徽章；取消和非法月份必须失败／传播。
- [x] 补充学习历史断言：补学新形式不得抹掉原完成日，跨日未结束的大轮次按各组完成日计数，同日同组去重，删除仍级联移除；升级有已知日期才回填，无证据不造历史。
- [x] 冻结并取得编译成功的行为 RED；恢复实现 GREEN，构建／全量通过后提交“实现双语言月历与历史打卡查询”。
- [x] 将页面接入原单窗口：月格下半部放两个圆形图标，各约四分之一格；白圈红心／圆形米字旗用 WPF 几何图形，避免引入图片依赖。显示日期和简明空态，不做 ERP 表格。
- [x] 添加至少 8 项 STA／导航测试，覆盖真实服务查询、返回动画、月份请求逆序、取消、页内错误和深浅色；持有旧页的作用域在转场结束后释放。
- [x] 构建／全量和截图冒烟通过后提交“打通单窗口打卡日历与月份导航”。

## Task 3：简明词汇与记忆报表

**文件：**
- 新增 `Dictation.DataAccess/Statistics/LearningStatisticsDataAccessor.cs`、`LearningStatisticsCountsPo.cs`。
- 新增 `Dictation.Core/Statistics/ILanguageStatisticsReader.cs`、`LanguageStatisticsDto.cs`、`StatisticsTrendPointDto.cs`、`FrequentMistakeDto.cs`、`LanguageStatisticsService.cs`、`LanguageStatisticsMapper.cs`。
- 扩展 `Presentation/ViewModels/StatisticsViewModel.cs`、`Views/StatisticsPage.xaml`；需要的图点绑定类型按一文件一类型置于 ViewModels，不建通用绘图库。
- 新增 `Dictation.Tests/Core/LanguageStatisticsServiceTests.cs`、`DataAccess/LearningStatisticsDataAccessorTests.cs`、`Presentation/StatisticsReportTests.cs`。

**能力合同：**
- `ILanguageStatisticsReader.LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default) : Task<LanguageStatisticsDto>`。
- DTO 包含 Language、AsOfUtc、LearnedGroupCount、TodayLearnedGroupCount、TodayReviewedGroupCount、PendingTestGroupCount、OverdueGroupCount、TopMistakes、RetentionTrend、DueTrend。
- `StatisticsTrendPointDto(DateOnly Date, double Value)`；保持率 Value 为 0–1，词组趋势 Value 为整数值；空记忆返回空曲线而不是 100%。
- `FrequentMistakeDto(string WordGroupId, string Headword, string Meanings, long ErrorCount)`：按组汇总原错题查询，Top 10，错误数降序、组 ID 稳定排序，同组不同词形的错误可相加但不创建新词。
- 消费已有 `IReviewLibraryQuery.LoadAsync`、`IWrongAnswerQuery.LoadAsync` 和 `FsrsScheduler.GetRetrievability`；DAO 只查询学习完成数，不能计算 FSRS。已有复习库数据仅加载一次，避免分别为各图表重复全量读取。
- `LearningStatisticsDataAccessor.LoadAsync(string language, string localDate, CancellationToken cancellationToken = default) : Task<LearningStatisticsCountsPo>`：读取现存学习完成整组总数及本地当天数量，旧词导入没有完成记录时不计已学。

- [x] 写至少 18 项行为测试：导入旧词不等于已学；学习完成整组计 1；删除撤回数量；五十音不混入词汇；共享听写错误不重复统计；未测试状态不伪造保持率；30 点预测值 0–1 且无新复习时不增长；14 天到期按组去重、逾期单列；日英、取消、损坏状态边界。
- [x] 冻结测试；临时把多词形当多词或保持率显示成常量，构建成功后记录行为 RED；仅恢复实现转 GREEN。
- [x] 构建／全量和真实 SQLite 查询冒烟通过后提交“实现词组统计与FSRS预测报表”。
- [x] 页内展示简明统计卡片、易错词 Top 10、预计保持率折线和到期柱条；复用主题资源。图表只需 WPF ItemsControl／Polyline 等原生元素，不引入重型图表包。标签、单位、空态和“预计”说明必须完整。
- [x] 至少 8 项页面测试覆盖语言固定、数据绑定、无数据、载入失败／重试、取消／逆序、浅深色和不同窗口尺寸；运行构建／全量／截图冒烟，提交“打通单窗口统计与记忆趋势报表”。

## 验证命令与交付

各任务执行时按实际新增数量设置聚焦门槛，不得低于计划最低数；记录真实通过／失败数量，不先声称结果。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*DailyCheckInServiceTests' --minimum-expected-tests 10 --max-threads 4
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1910 --max-threads 4
git diff --check
```

新增用例后将全量门槛提升至实际基线并更新 AGENTS 命令，不降低门槛。真实页面冒烟使用临时隔离 DATA／CONFIG、固定时钟和测试数据，不写用户实际记录；浅／深色截图保存在 `docs/verification/images/`。永久证据写入 `docs/verification/2026-10-12-statistics-calendar.md`，包含测试冻结哈希、编译成功的 RED、恢复后 GREEN、完整构建／测试／运行结果。

最终更新现有本地预览的程序文件时保留其 DATA／CONFIG 等运行目录，不重复复制或重建已验证词库／模型。按此前选择仅做一次最终独立审查，修复有效问题并验证；不反复调用 worker 重审同一证据。

**本计划不包括：**Updater、CI／GitHub Release、许可证决策、OCR 及旧工作区清理。这些仍是完整目标的后续任务，不能以本阶段完成宣称整个 GOAL 达成。
