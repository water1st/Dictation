# 每日复习进度能力验证

目录 `D:\Source\Repos\Dictation`，分支 `codex/dictation-refactor`，起点 `dac1258`。这是复习单窗口计划 Task 3 的后端能力，不是整个 Task 3 完成。

## 行为与复用

`IReviewProgressReader` 只暴露每日进度查询，供首页消费；内部服务组合当前复习计划、已有 `ReviewSessionDataAccessor.CountCompletedOnDateAsync` 和应用时钟。没有新建 SQL、数据表、通用统计框架或依赖包。

- 按当地完成日期统计不同整组，不按形式、作答次数或会话启动日期计数。
- 日英和学习／复习互不混算。重复完成同一组当天只计一次，整组删除后关联进度随之删除。
- 目标采用当前计划，而非活动会话的旧快照；查询不启动会话或更改记忆。
- 非法语言、取消与数据库失败传递给调用方，不能用假“0 完成”隐藏问题。

## 证据修正与严格 RED

冻结前修正过一次使用题面不存在的 `MemberId` 的编译适配错误，编译失败不计 RED。

首轮 11 项失败中，`GetToday_MultipleFormsAndAnswersCompleteExactlyOneGroup` 因夹具错误要求假名 `IsPrimary=1` 而未查到礼貌形；这是测试取样错误，不是行为断言。首次全量 1728/1729，唯一失败就是该测试。原 RED 证据已作废，不把基础设施／夹具错误当作 RED。

只将夹具查询修正为该实际题目对应的假名变体并按优先级取首项，不修改“4 次作答完成 1 组”等业务期望。重新冻结测试：

`ReviewProgressServiceTests.cs` SHA-256：`CFA1108F16CD255A169F9117F7B79CA74DBD5CC02AD1160DC7F3D1A0ADE990F7`。

随后仅临时让生产查询返回 0/0；构建成功、0 警告错误后，以下命令实际执行 11 项，全部因值断言或未抛预期异常而失败，0 跳过，退出码 2：

```powershell
& ./Dictation.Tests/bin/Debug/net10.0-windows/Dictation.Tests.exe --filter-class '*ReviewProgressServiceTests' --minimum-expected-tests 11 --max-threads 4
```

## GREEN

仅恢复生产实现，不改冻结测试：

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1729 --max-threads 4
git diff --check
```

构建 0 警告、0 错误，全量 1729/1729 通过，0 失败、0 跳过，约 1 分 54 秒；冻结哈希不变，差异检查通过。真实 SQLite 测试包含多形式答题到整组完成、删除、日期及独立语言查询。临时错误不提交，不为记账再跑一遍。

首页绑定、复习单窗口答题／浏览与音频冒烟仍待接入，完整 GOAL 保持进行中。
