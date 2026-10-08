# 学习共享听写业务切片验证

## 已实现范围

在 `Dictation` 主目录复用已验证的 `PracticeDictationTargets`、客观判题、状态转换、调度器和 FSRS 初始化，没有另写匹配或记忆算法。学习提交保持公开 Service/DTO 合同不变，通过纯 Mapper 给原学习事务补充次要目标成绩。

主目标沿用原 `LearningQuestionAttempts`。追加迁移 12 创建 `LearningSharedAnswerAttempts`，仅保存共享的次要目标；外键归属于目标自身，不引用主题目的外键。删除主题目词组后，其它义项的初错记录和记忆仍保留。读调度历史统一合并两个来源，总结按题目编号统计真实作答数，不按目标投影数量膨胀。

同一次事务提交题目、全部目标状态、共享观察、完成整组、首次记忆和下一题；次要目标仍须属于本轮且未掌握。写入失败整体回滚，取消或重复提交不得新增观察。只有完全相同的听写共享，看义题、不同发音或不同可接受答案不合并。

## 真实 RED 与冻结

新增 9 项真实 DI/SQLite 测试及 1 项混合渠道测试。初轮 9 项执行 6 失败／3 通过，随后仅修生产 9/9；混合渠道临时禁用共享后期望 3 次、实际 4 次，构建成功后的值断言失败。

首次全量 1772/1773，唯一失败是旧学习迁移测试的内联表清单遗漏新表。补齐该清单，不改业务期望；前轮整体 RED 证据作废。重新冻结全部 7 份测试，仅临时禁用生产共享选择，构建零警告／错误后两类合计实际 10 项：7 项行为断言失败、3 项通过、0 跳过。随后只恢复生产代码。一次 runner 中间通配符参数错误未执行测试，不计 RED。

最终冻结 SHA-256：

| 文件 | SHA-256 |
| --- | --- |
| LearningSharedDictationTests.cs | AB5C63C4F8B90710686E0AB7CDF9695E58F127B823C33DD68B7D6B19DF7BF89B |
| LearningSharedCoverageTests.cs | 5FF7BB5AF84DD8DEBA853B06D370A1EF84667FB78E4F69EBDBBB7A99E61EC20A |
| SchemaMigrationRunnerTests.cs | 1A0F411F209C38DADD69ACE32484D243E3D07A0A93976219BC91DDE3D53497A8 |
| StartupInitializerTests.cs | 7B1BF7E6ACEF3ED7DDD9DD9035215F7730D011296E407A5B5E6E70D74D6E9CBA |
| LearningPlanDataAccessorTests.cs | 5C89BCA400C5824A08231DF88040955C1E48ACA94709A0C8ED30E0AA8BD2160D |
| LearningSessionDataAccessorTests.cs | F59AF70A26503EBDCDFC8FEA23A8510537AB68F2D5151D1BEF6CA57B86C8B0F9 |
| StableImportMetadataTests.cs | 6107423A948081F8435A325A8D169D2345D9392069DD8777108080782F74873D |

## 验证命令

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1773 --max-threads 4
git diff --check
```

最新 solution 构建零警告／错误；最终全量 1773/1773，零失败／跳过，2 分 08 秒，冻结哈希不变，diff 检查通过。测试直接经过 Core 和 SQLite 事务，不是只测 mock；日语两义项听写 2 次完成两组，初错后重练 3 次仍皆 Again；日英共享各推进独立目标。混合渠道保留各义项自己的看义题，只共享听写。不为完成记账重跑已通过的测试。

本次尚不宣称共用页面最终验收或整个 GOAL 完成。下一步计划 Task 2 检查真实 WPF 反馈、结算与首页整组计数；错题管理、五十音、MCP、统计打卡、更新发布与最后的 OCR 仍需继续。未合并、推送、覆盖正式词库或用户数据。
