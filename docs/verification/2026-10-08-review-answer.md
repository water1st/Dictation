# 复习客观答题与原子记忆验证

验收目录：`D:\Source\Repos\Dictation`；分支：`codex/dictation-refactor`；本次起点：`3cd8640`。

## 复用与新增边界

旧工作区的 `d16d64b` 已是当前主目录 HEAD 的祖先，全部既有代码已包含在这里，没有重新复制、重写或逐项重跑旧功能。新增的是尚未实现的复习答案提交，不把旧代码搬运算作新开发。

- 直接组合现有客观判题、动态题目调度和 FSRS；学习成绩转换移动为共用纯组件，不复制算法。学习与复习仍独立保存成绩。
- 每个形式连续正确两次完成；本轮曾错过的形式即使随后答对，FSRS 仍采用 Again。已存在的记忆续算，尚未到期的其他形式不改变。
- 完全相同的听写观察合并，成绩和记忆仍按各自词形记录；删除其中一组不会删除另一组自己的观察。这个新增共享提交目前接入复习模式，不宣称学习提交也已接入。
- SQLite 同一事务保存答案、词形成绩、记忆、下一题和整组完成日；过期／重复／并发提交不能重复计数，任何中途失败全部回滚。
- 新增迁移 11，不修改已提交迁移 10；旧测试的迁移版本和表清单在冻结前完成适配。无新依赖包，不重建词库，不更改用户数据。

## 编译成功的行为 RED

20 项新增真实 DI／SQLite 测试覆盖正常、空答案、错误重练、动态渠道字形覆盖、独立形式、旧记忆续算、重复／并发、事务故障、重启、听写去重、删除隔离、穿插、非法会话和取消。

测试冻结 SHA-256：`6D9FF74EFA6AF6039DF52DFC5B94219B6504292A85E4D9A9E82015252DCAE932`（`ReviewAnswerServiceTests.cs`）。

骨架编译通过后，直接执行同一已编译 MTP 测试程序：

```powershell
& ./Dictation.Tests/bin/Debug/net10.0-windows/Dictation.Tests.exe --filter-class '*ReviewAnswerServiceTests' --minimum-expected-tests 20 --max-threads 4
```

结果 20 项实际执行、20 项行为断言失败、0 跳过，退出码 2。不是编译红灯。此前默认构建异常退出，以及 SDK 测试入口因命名管道权限被拒绝而零测试，均不计为 RED。此后仅实现生产行为，测试文件哈希不变。

## 整合 GREEN

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1718 --max-threads 4
git diff --check
```

最终构建成功：0 警告、0 错误。全量 1718/1718 通过，0 失败、0 跳过，约 4 分 35 秒；冻结哈希保持一致，差异检查通过。

首次整合运行在受限沙箱中实际执行 1718 项，1678 通过、40 失败，涉及命名管道拒绝、配置／词库临时文件替换拒绝及相关 UI／TTS 后续失败。没有修改代码、断言或降低门槛；正常本地权限下同一构建的上述全量命令全部通过。没有随后为记账再次运行测试。

这是计划 Task 2 的后端答题部分；Task 3 的复习单窗口 UI／浏览／首页进度仍未完成，不能据此声称完整 GOAL 达成。
