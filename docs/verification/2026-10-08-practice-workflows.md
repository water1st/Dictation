# 共用练习页面的模式适配验证

## 范围

只增加表现层的学习／复习 workflow 适配和绑定 Mapper。两个模式调用各自既有 Core 合同，不重写已验证的判题、记忆、SQL 或音频，不合并进度。共用页面和实际复习入口将在下一部分接入；本记录不表示 Task 3 已完成。

## 测试先行与证据边界

新增 `PracticeSessionWorkflowTests` 共 9 项，覆盖真实三层启动、错误后重练结算、模式成绩隔离、成员错误关联、复习计划快照、只读浏览、无候选、取消、非法手选以及缺失会话异常。

首轮骨架编译成功，8 项行为失败、1 项通过。但实现后发现新夹具使用错误表名、集合引用整体比较及错误的原业务异常分类；该轮证据作废。修正夹具并重新冻结，不改变客观业务期望、不修改原业务异常。

重新冻结后，仅在生产 Mapper 中临时把两种模式的反馈答案改为“临时错误”。构建成功、0 警告／错误；9 项实际执行，2 项值断言失败、7 项通过、0 跳过。两个失败均为期望“たべます”、实际“临时错误”，不是编译／基础设施失败。随后只恢复生产映射。临时错误不提交。

冻结及恢复后测试 SHA-256 均为 `D6EBCA178AC4705EA47DE5CAD923A6A534BAE75646C43BDC16AC99C76B38EC76`。

## 最终验证

在 `D:\Source\Repos\Dictation` 执行：

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1749 --max-threads 4
git diff --check
```

构建 0 警告、0 错误；全量一次整合回归 1749/1749，0 失败、0 跳过，约 2 分钟。新增测试覆盖两种模式的真实 SQLite 启动与错误重练，不以模型替身声称正式 TTS/OCR 已验收。未修改 `Resources/` 或已有用户运行数据。
