# FSRS 记忆与复习队列验证记录

本计划分片执行；当前仅完成数值内核，不表示复习答题、队列页面或每日计划已经交付。最终验收仍在原 `Dictation` 目录，OCR 后置。

## 数值内核

固定参考来源与七组官方黄金向量见 `docs/resources/FsrsReference.md`。`FsrsSchedulerTests.cs` 冻结 SHA-256 为 `D1BCD41C89A924E03AB88EC5C59646F61A04024AB59A6CEDB8C76A456DDABFCD`。

可编译生产骨架返回错误稳定度／计数：测试项目构建成功，0 警告／错误；实际执行 30 项，25 个数值、非法输入、时间或状态断言失败，5 项通过。此后不改测试，仅移植生产公式，聚焦 30/30；冻结哈希相同。

全量 **1524/1524**，无跳过；solution Debug 构建 **0 警告、0 错误**。随包 MIT 许可证已出现在 WPF 输出 `LICENSES/py-fsrs.txt`。算法是内部纯计算组件，无 Python／网络运行依赖、额外调度服务或新 NuGet 包。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class '*FsrsSchedulerTests' --minimum-expected-tests 30
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1524
```

## 待接通的下一步

学习完成的原子记忆初始化、初次错误保留、重复保护及失败回滚；随后接通风险复习词库页面。复习答题、错题、五十音、MCP、统计、完整 ZIP 更新发布和 OCR 仍属于未完成 GOAL，不在本次数值内核证据中声称完成。
