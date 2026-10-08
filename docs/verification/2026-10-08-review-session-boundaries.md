# 复习会话最终审查边界修正

## 范围

沿用 `Dictation` 主目录的 `codex/dictation-refactor`，不重新实现或复制已有功能。仅处理计划最终一次审查发现的三个问题；不增加依赖、不修改词库或用户数据。

- 删除最后一个未完成词组后，在同一事务内结束只剩已完成词组的活动会话；保留其余词组的作答、完成日期和记忆，不给被删除词组计分。仍有未完成形式则继续原会话，收尾失败必须整体回滚。
- 首题准备不再一直锁住导航；动画结束即可返回，题目未显示时也能取消。有 Dispatcher 上下文时后台执行业务，展示更新仍返回原上下文；取消后不发布迟到题目。旧页的作用域仍等退场动画结束才释放。
- 首页取消进度查询后，即使依赖迟到抛出普通异常，也不覆盖已有进度。

## 测试先行与冻结

新增 `ReviewDeletionBoundaryTests` 3 项及 `PracticePreparationBoundaryTests` 3 项。旧 DAL 测试仅在冻结前适配业务时钟时间参数。

构建成功（零警告、零错误）后实际执行 6 项：5 项行为断言失败、1 项通过、0 跳过。失败分别为会话状态仍为 InProgress、收尾失败未触发事务回滚、准备期无法返回、没有可见取消入口、取消后的普通异常未转为取消。随后只修改生产代码，同一批 6/6 通过。

RED 与最终实现的测试 SHA-256 一致：

| 文件 | SHA-256 |
| --- | --- |
| ReviewDeletionBoundaryTests.cs | E9D8ADDC0A6B57A6D0E10B7327575E9A5FCA3D076B0A5D218E84CFD2839D52FF |
| PracticePreparationBoundaryTests.cs | 81B57955C80B8575C39A8FA2EC664A02EFA33E3290EBE9A4808B64EF1C1F39E5 |
| ReviewPlanDataAccessorTests.cs | 4C18103FF5899E2200D76AA0C863A6B6C067E00E3E41543ED6F4288F2AC4E1C3 |

首次整合在 22 项时被旧同步 STA 宿主中的线程异常终止，不算通过或有效全量。原因是宿主没有 Dispatcher 消息循环，后台执行后无法返回 UI 线程。仅修正生产执行边界，没有改测试期望；正常 WPF 有 Dispatcher 时仍保持后台业务、前台展示。

## 最终验证

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1763 --max-threads 4
git diff --check
```

最终 solution 构建零警告、零错误；全量 1763/1763，零失败、零跳过，2 分 03 秒。最终 diff 检查通过，冻结测试哈希不变。不为任务记账再次运行已通过的验证。

复用原有隔离 STA 冒烟程序，最终线程修正后实际输出 `Passed=true SharedUserControl=true NativeModes=true ExtraWindows=0`。日语礼貌形 2 次、英语整组 4 次作答，各完成一组；首页分别为“复习 1/20”，学习保持“学习 0/20”。这不是人工点击、正式音频或 OCR 验收。

正式只读词库及原用户进度哈希不变：日语 `EE4411EA2766AFCD3261BFAEC7599DFD6F42AB184C3391C23496D1504999C734`，英语 `151F960A23FFE0008E445F893892321131466921E4A8FBB68A32972135A8302B`，用户数据库 `D10D7879DCE222E2A3FF34B2341BCE95EDAA220B53AAF7C01039BA3325FF4BD5`。

## 审查范围裁定

以下仍属于完整 GOAL，不因当前复习计划完成而取消：独立错题管理和人工复核 List/Detail、五十音、MCP、统计打卡、ZIP 更新发布、学习模式共享听写去重、真实模型与人工验收，以及用户要求最后处理的 OCR。

小候选池不足 3–7 道间隔题时保留既有调度回退，不为了凑数创建题目或使单词组永远无法完成；候选足够时仍遵循既定间隔。后续若明确要求严格禁止提前重练，再单独修订该边界，不在本次修复中悄悄改变规则。

没有进行合并、推送或发布，完整 GOAL 未完成。
