# 错题页面链路验证

## 范围与复用

验收目录为 `D:\Source\Repos\Dictation`，分支为 `codex/dictation-refactor`。旧工作区提交 `d16d64b` 已包含在当前历史中，旧工作区没有未提交的代码改动；不重复复制旧版或重做已经验证的功能。

新增首页错题入口和同窗口 List/Detail，语言固定为首页选择。列表顶端全选，详情展示中文义项、对应词形、原错误、正确答案、题型和日期；喇叭只播放内存音频。复习错题通过原复习页面作答、更新原记忆；学习错题继续原学习，不提前迁库。取消、返回和作用域释放不发布迟到异常。

## 测试有效性

首次页面测试发现夹具精确类型断言和未布局读取绑定的问题，该轮证据作废。修正夹具后重新冻结，不降低业务期望。新增入口测试在冻结前完成现有 `IsEmptyVisible` 属性的编译适配，编译失败不算 RED。

冻结后，仅在生产实现临时制造错误：

- 模式标题变为普通复习，且取消后仍发布迟到异常：编译成功，8 项实际执行，4 失败、4 通过、0 跳过。
- 错题启动错误地调用普通到期复习：编译零警告／错误，新增真实数据库链路 1 项实际执行并失败，`IsEmptyVisible` 期望 `False`，实际 `True`。

恢复生产实现后，同批聚焦测试 9/9 通过、0 失败／跳过。两份测试的最终 SHA-256 与冻结值一致：

| 测试 | SHA-256 |
| --- | --- |
| WrongAnswerPresentationTests.cs | 0AC8084538842D723F2704BF678C30F2CDD1815B60F751F526714B52F41964EB |
| WrongAnswerWorkflowBoundaryTests.cs | 600779590B484E8127638E1F15964AA3744460A357481BF615137D36DC3F00A5 |

旧三个测试仅在冻结前适配首页导航接口；没有修改既有业务期望。新增测试通过真实 DI、SQLite、STA Dispatcher 和 XAML 渲染验证日英入口、详情、全选、学习续练、重练反馈与结算、原 FSRS 更新、同窗口返回、主题资源及取消异常；TTS／音频为受控外部边界，不冒称已验证真实模型。

## 命令与验收边界

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
& ./Dictation.Tests/bin/Debug/net10.0-windows/Dictation.Tests.exe --filter-class '*WrongAnswerPresentationTests' --filter-class '*WrongAnswerWorkflowBoundaryTests' --minimum-expected-tests 9 --max-threads 4
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1799 --max-threads 4
git diff --check
```

解决方案构建零警告／错误；全量验收 1799/1799、0 失败／跳过，持续 2 分 39 秒；冻结哈希和 diff 检查通过。此页面是历史错题查看，不是 OCR 人工复核；五十音、MCP、统计打卡、更新发布及最后 OCR 仍属于未完成的完整目标。

## 唯一最终审查

只读审查范围为 `aed2659..01cb0d9`，不重复测试、构建或审查旧计划。五项 Review Focus 均已核对，无 Critical／Important，结论为可保留本轮提交。

两项非阻塞页面问题暂留，避免在收尾阶段扩大改动：

- 全选控件的用户三态循环会先进入 `null`，而 ViewModel 忽略该值；全选后直接点击一次不能清空，需要再点一次。后续可禁用用户三态循环，仍以绑定的 `null` 表示部分选中，并补真实控件点击测试。
- 历史作答时间按 UTC 格式化但未注明 UTC，和本地到期时间不一致；中国时区相差八小时。后续应统一本地显示或明确 UTC 标识，不涉及当前记忆调度时间的计算。

本轮判断：历史错误保留用于易错回顾和审计，不把重试正确当作删除；代价是列表必须明确其并非“仍未纠正”的队列。OCR、其他未完成目标及真实模型／人工视觉验收仍保留在原目标内，未因本轮审查范围而取消。当前保留 `codex/dictation-refactor`，不提前合并、推送或宣布完整目标达成。
