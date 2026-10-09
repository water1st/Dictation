# 五十音单窗口链路验证

## 范围

验收代码位于 `D:\Source\Repos\Dictation` 的 `codex/dictation-refactor`。旧工作区代码已在该目录，不重新复制或重做旧功能。后端首次提交为 `74cfd11`；本轮接日语首页入口、独立范围计划、听写、即时反馈、错项重试、结算和返回，沿用原 Shell 导航与动画，无新增窗口、依赖或音频文件。

平／片假名和音类分别至少选一项：最后选项禁用取消，程序化设置同样保留有效范围。全量显示实际启用数，而非固定“50”；每日数量交由 Core 校验，原活动轮次优先恢复。题面只绑定字形、音类及必要行提示，提交前不显示正确答案或语音文本。

首页、计划只注入 `IKanaProgressReader`，答题读取使用 `IKanaSessionReader`；两个能力仍是同一单例服务。旧测试只适配构造依赖、导航方法和统计合同，保留原行为期望。

## 真实行为 RED → GREEN

新增选入边界两项，先在原实现上编译成功并执行：一项因不同随机种子总选同一个 Unicode 最前项失败，风险优先测试通过。修正为先打散候选、稳定按保持率排序、按额度选入，再随机题序；只读进度不消耗随机源。

新增页面九项先完成编译适配。冻结后仅临时修改生产实现：允许取消最后范围项、英语入口可见、音频失败误设为已成功播放。编译零警告／错误，九项实际执行为四失败、五通过、零跳过；失败都是业务／UI行为断言，不是编译、零测试或基础设施错误。

只恢复生产实现，同批九项、选入两项及原后端十二项共 **23/23 GREEN**。十份新增／适配测试文件的恢复后 SHA-256 与冻结值一致，主要测试为：

| 文件 | 冻结 SHA-256 |
| --- | --- |
| KanaPresentationTests.cs | A2EB420CF4F80D2369118464788EDCF00051BC75FCD7D830FA8E2C66948B108A |
| KanaSelectionBoundaryTests.cs | 8B566468913BDF006266F713BB17A570814E8862AD1683BD48A560F7698E0395 |
| KanaServiceTests.cs | 1A5708251A9DA8B7147BF61A8BB3CEED23A1016255A4BFA934123BA6D2995966 |

第三项与后端提交的历史哈希不同，仅因冻结前把三个统计读取改为独立统计合同；没有改原断言，也不重新宣称后端历史 RED。新增页面测试覆盖真实 DI、隔离 SQLite、STA Dispatcher 和 XAML 布局：全链路计数、FSRS Again、原始错误保留、同窗口返回与释放、英语隐藏、浅／深色、最后选项、非法数量、损坏存储、音频失败无观察、取消后的迟到异常及语言切换后的迟到统计。

## 验证命令与结果

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
& ./Dictation.Tests/bin/Debug/net10.0-windows/Dictation.Tests.exe --filter-class '*KanaPresentationTests' --filter-class '*KanaSelectionBoundaryTests' --filter-class '*KanaServiceTests' --minimum-expected-tests 23 --max-threads 4
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1822 --max-threads 4
git diff --check
```

解决方案构建零警告／错误；本切片一次完整回归 **1822/1822**，零失败／跳过，2分27秒。临时错误已移除，冻结哈希及 diff 检查通过。注释补充后做相称的构建，不为纯注释重跑全量。

音频测试使用受控外部 Provider／播放器，不能据此冒称真实 Piper 模型或人工视觉验收通过。当前五十音使用临时键盘输入；日语默认手写、米字格、OCR和人工复核仍由最后输入切片接入，页面明确标注。日历打卡由后续统计切片消费实际完成日期。MCP、统计、更新发布、实际模型验收和最终工作区清理仍在完整目标内。

本计划唯一最终只读审查在本轮提交后进行；不合并、推送，也不宣称完整目标完成。
