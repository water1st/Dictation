# 独立五十音后端验证

## 完成范围

主目录 `D:\Source\Repos\Dictation`，分支 `codex/dictation-refactor`。实现范围、计划保存、原会话恢复、客观听写、原始错误、实际本地完成日期、独立每日数量和 FSRS；未创建任何词库词条或词汇记忆。

追加迁移13，新增 `KanaPlans`、`KanaMemories`、`KanaSessions`、`KanaSessionItems`、`KanaAttempts`，不修改前12迁移。作答使用题序与项目乐观锁，在同一事务提交观察、项目、轮次和必要记忆；取消／重复提交不能额外计数。活动轮次优先恢复，不被新计划或日期改变。

复用已有 `AnswerEvaluationService` 的 NFC 精确比较及 `FsrsScheduler`，没有新算法包或第二套记忆公式。DTO、PO、Mapper与业务／CRUD分别内聚在Core/Kana和DataAccess/Kana。

## 行为 RED 与冻结

最初骨架两个实际行为失败；随后发现测试夹具误写不存在的 `UserWordGroups`（应为 `WordGroups`），整轮证据作废。修正实际表名和等价 xUnit 断言语法，不放宽业务期望；旧迁移／启动测试只追加新表和版本13，在冻结前适配。

重新冻结后，仅在生产实现临时令每日数量加1、把错项最终FSRS强行判Good。编译零警告／错误，完整12项实际执行：5失败、7通过、0跳过。失败均为行为断言：每日及恢复3→4、跨日完成1→2、单项未完成时记忆0→1、错项Again记忆1→0。

只恢复两处生产实现，同批12项与相关13项迁移／6项启动测试，共31/31 GREEN、0失败／跳过。最终测试文件哈希与重新冻结值相同：

| 测试文件 | SHA-256 |
| --- | --- |
| KanaServiceTests.cs | A141662A423FE9DFD50FF4B080FB1547CD543DA78AA6AF5B75F7CA2019C3CD57 |
| SchemaMigrationRunnerTests.cs | 517574FF2F6C2E13AC20AF59BC661F17FB0C02EB74D9A1A615E8710CAC471E9F |
| StartupInitializerTests.cs | 872700BC7601C2489753B3DE33C6973C61DCF9CDD03F64D8D7948F9B4BC62115 |

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
& ./Dictation.Tests/bin/Debug/net10.0-windows/Dictation.Tests.exe --filter-class '*KanaServiceTests' --filter-class '*SchemaMigrationRunnerTests' --filter-class '*StartupInitializerTests' --minimum-expected-tests 31 --max-threads 4
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1811 --max-threads 4
git diff --check
```

解决方案构建零警告／错误，相关31/31通过。首次全量1811项为1806通过、5个旧迁移夹具失败：四个最新版本断言仍要求12，一个版本11夹具留下13缺12的非法记录。仅更新版本断言为13、恢复隔离夹具的真实版本11结构，保留所有原词形、作答、记忆、备份和完整性断言；冻结的新十二项测试未修改。

这五项修正后的迁移测试实际5/5通过。一次混用类与方法过滤导致零项匹配，已明确作废，不算通过或RED；改用纯方法过滤实际执行五项。修正后完整回归1811/1811、0失败／跳过，2分39秒；最终构建零警告／错误，冻结哈希和diff检查通过。不重复旧功能的RED，不为进度记录再跑一遍全量。

## 有依据的边界判断

- 现代范围依据[国际交流基金 Irodori 假名练习](https://www.irodori.jpf.go.jp/assets/data/Kana_all.pdf)及[拗音表](https://www.irodori-online.jpf.go.jp/a1/introduction/02-008-16-01/)：清音46、浊音／半浊音25、拗音36，平／片假名共214项；包含几乎不用的ぢゃ等，但不含古假名／外来音扩展。代价是实际全量数并非字面“50”，页面必须按启用范围显示真实数量。
- 全量首次正确只出一次，有过错误才要求连续两次正确；采用五十音的具体规格，不机械套用词汇每形式两次规则。代价是两种模式完成次数不同，页面需要明确错项重练。
- 同音项提供必要行提示，不显示待写字符，避免不可区分的音频导致误判。代价是少量额外题面提示。

**未完成范围：** Task2日语独立页面、同窗口听写、真实模型音频和人工UI验收仍未完成；OCR留最后切片，日历统计由后续统计切片接入本次的实际本地完成日期。本计划唯一最终审查在Task2全部完成后进行；当前不能宣称五十音或完整GOAL已交付。
