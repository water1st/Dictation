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

最终 solution 构建 **0 警告／0 错误**，全量 **1616/1616**、失败／跳过均为 0，静态 diff 检查通过。源码无新增数据库；用户下载资源及现有预览进度未被使用或覆盖。这是业务阶段验证，不是 UI 交付或整个 GOAL 完成。

## Task 2：单窗口复习计划页面

首页按当前语言进入 scoped ViewModel 与 UserControl，复用唯一导航栈、确认弹层。后台读写可取消；返回或释放取消操作，晚完成不回写旧页面。学习与复习共享纯选项工厂，但各自创建选项实例，不共享选择。

### RED → GREEN 及冻结

首次页面 RED 有空集合索引越界，**该页面批次证据作废**。GREEN 前补“应加载两组”的前置断言，重新冻结、构建成功，实际页面 **7/7 行为断言失败**；生命周期 **6/6 行为断言失败**。GREEN 首轮名词标签缺词性，由生产展示组件修正，不修改冻结测试。

正式深色截图发现选项仍是黑色，另补主题 **2/2 RED**（前景色断言）和例词选择 **1/1 RED**（没有可辨义项的主题列表），均编译成功后真实执行。随后只修 XAML，聚焦 **2/2、1/1**；原页面 **7/7**、生命周期 **6/6**。

| `Dictation.Tests/` 文件 | RED 前与 GREEN 后相同 SHA-256 |
| --- | --- |
| `Presentation/ReviewPlanPageTests.cs` | `990AF8ABC587808E8D5286754303D5377146677FACDF8181431C2AB42D419629` |
| `Presentation/ReviewPlanLifecycleTests.cs` | `F1FA2D6412DC77A6EA1D143C54A4B4C20CE612597E6CF13F0BC42377C1479F89` |
| `Infrastructure/ReviewPlanPresentationTestHost.cs` | `5A93B6B9C11BA22E65AF5FCE84EEA984106B0933DC81159D8C6F548AF29E4566` |
| `Presentation/HomeViewModelTests.cs` | `A11AB09FA74195A7D8926BB190465665A10299C59623F7A15F4936DFD402BD42` |
| `Presentation/LearningSessionNavigationAndRenderingTests.cs` | `8EC592CD25BCDAEE02757C1BEC6932C6589B3FC205A7255CD437FD962B1EC79A` |
| `Architecture/CoreLayerArchitectureTests.cs` | `D139D43D2C63AE3EF6511A680FFECF3A6AAB8E086AF34A3C0DE8F19360FAE10A` |
| `Presentation/ReviewPlanThemeTests.cs` | `E9B0311606D950861EBC25648A2AF9043E76F863538937B6077D39A80438E340` |
| `Presentation/ReviewPlanExampleSelectionTests.cs` | `E1D0A9AF38A630AAC376122FFFD53DEDB5E86F70F6D8F19813D605FA811BF043` |

最终 `dotnet build Dictation.sln --configuration Debug --no-restore`：**零警告／错误**。`dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1632`：**1632/1632**，失败／跳过均为零。一次错误中间通配符过滤运行零项，明确不计通过，后改用各类精确后缀过滤。

### 正式词库隔离冒烟

临时 STA 工具在本计划隔离运行目录，通过真实 DI、导航、Core 与 SQLite 只读使用现有正式词库，不重新生成或读取预览用户数据库。`食べる` 显示“吃；食用”，`go` 显示“离开；移动；行动；行进；运行”；日英分别保存每日 11／7 组、每轮 2 组、指定范围。重开与重启一致；确认删除日语组，英语组保留，日语范围级联清除。

实际输出 `Passed=true, Restart=true, ConfirmedDelete=true`；应用 DataTemplate 创建 UserControl，未新增 Window。浅深截图实际查看；这是无窗口 STA 渲染和真实业务冒烟，不是人工整机点击验收。

初次 NuGet 使用沙箱空缓存且网络拒绝，未运行冒烟；随后仅指向现有用户缓存离线还原成功，未安装工具或新增依赖，最终冒烟使用 `--no-restore`。

原预览用户数据库仍为 **196608 字节，修改时间 2026-09-29 17:41:03**。词库 SHA-256：日语 `EE4411EA2766AFCD3261BFAEC7599DFD6F42AB184C3391C23496D1504999C734`，英语 `151F960A23FFE0008E445F893892321131466921E4A8FBB68A32972135A8302B`。

### 执行裁决

1. 只落实已批准规格，自主继续非 OCR，不另开 GOAL 或新增产品选项；判断错误时修正执行安排，不能把安排当新增需求授权。
2. 勾选是范围，删除作用于点击选中的详情词组且需确认；全部包含后续加入词。若需批量删除，另加明确命令，不能让复选框隐式删除。
3. 数量使用 1–500 整数滑块以避免文本解析失败后保存旧值；若数字输入更顺手，后续替换正确验证的输入控件，不改 API。
4. 英语例词用固定高度主题列表展示中文义项，避免默认下拉白底白字与同词头难辨义；若需紧凑显示，只换呈现，不改业务规则。

复习答题、五十音、MCP、统计、更新发布及 OCR 仍为完整 GOAL 后续工作，不据本切片结束整个目标。

## 最终只读审查

范围 `d16d64b..b1c5ae0`，两次业务提交。首个指定模型因额度限制在执行前终止，没有审查结果；替代可用模型完成唯一实际审查，不重跑测试或构建、不写文件。Critical／Important 均无，非阻塞建议一项：页面测试没有固定纯假名／罕见汉字组的展示，当前页面直接显示已有快照且不生成词形，暂记为覆盖缺口，不是已证实运行错误。后续选词任务会检验无适用汉字不能出题，但不把该业务测试等同于补齐页面覆盖。

审查暂不评价事项的裁决（按审查顺序）：

1. 复习答题、风险抽词与 FSRS 结算继续由下一计划实现；此页不冒充会话完成。若误判成本为用户仍不能真正复习，因此完整 GOAL 保持 active。
2. 独立五十音是后续必做，不据此管理页宣称完成。若遗漏会缺少已批准的独立入口及日语打卡条件。
3. OCR、手写与申诉最后实现，当前不冒充已经识别笔迹。若顺序不合适需调整执行顺序，不砍掉需求。
4. MCP、统计打卡、ZIP 更新发布仍需分别打通并验收。若遗漏不能宣布完整重构完成或合并验收。
5. 批量删除暂不增加；当前单组确认删除符合既定范围。若用户确需批量操作，须加独立明确命令，不能让范围勾选承担删除。
6. 本次仅真实业务及 STA 渲染，不宣称人工整机、触控或笔的体验验收；若交互仍有问题，保留最终人工验收而非依赖测试数量。

保持 `codex/dictation-refactor` 继续完整目标，不在本切片合并或推送。当前计划临时目录在证据持久化后清理；原资源、预览进度及其它工作区不在此删除范围。
