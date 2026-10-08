# 共用页面的共享学习与升级验证

本切片直接使用既有 `PracticeSessionViewModel`、学习 workflow 和 `PracticeSessionPage`，不复制页面、不改写已验证的导航。真实 DI/SQLite 经首页进入学习，浏览两义项不推进成绩，听写后显示客观反馈，结算显示完成两组和实际 2／3 次作答，返回首页刷新为“学习 2/20”，复习仍为“复习 0/20”。初错后重练保留两义项错误。

另以真实已学单词和两个旧作答记录构造隔离版本 11 数据库，再运行正式迁移器升级到 12。检查旧作答、到期记忆、词组位置不变，新共享表存在且为空，实际产生备份，SQLite 完整性检查为 `ok`。仅测试夹具降为旧结构，不对正式数据库降级。

## 冻结与行为 RED→GREEN

冻结后仅在生产 Mapper 暂时将结算组数加一，以及在迁移 12 暂时删除旧作答。编译零警告／错误后三项实际执行并全部行为失败：UI 期望 2 组实际 3 组，升级期望 2 个旧作答实际 0 个。随后只恢复生产，三项 3/3 通过，零跳过；生产文件与提交前完全一致，没有遗留临时错误。

- `LearningSharedDictationPresentationTests.cs` SHA-256：`2F2668EABCEB3506B25587E03A915EE9158678B90B02626357849E200096403D`
- `LearningSharedMigrationTests.cs` SHA-256：`A7B2CAA66A6CB36B89AC473C109D60B0309F950D8B1BFFE290CD1F8064143BF7`

两份测试哈希恢复后不变。没有修改之前冻结的测试或为 GREEN 放宽期望。

## 验证

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1776 --max-threads 4
git diff --check
```

最终构建零警告／错误，聚焦三项通过；全量 1776/1776，零失败／跳过，2 分 09 秒，diff 检查通过、冻结哈希不变。页面测试包含 STA Dispatcher、实际 XAML 和三层数据库链路；语音／播放器使用受控边界，不冒称正式模型音频或人工点击验收。不为完成记账再次运行已通过的测试。

本计划尚待唯一最终审查；完整 GOAL 尚有其它非 OCR 功能与最后 OCR，不合并、不推送、不覆盖正式词库、用户进度或 Resources。
