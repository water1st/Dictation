# 首页复习进度垂直切片验证

目录 `D:\Source\Repos\Dictation`，分支 `codex/dictation-refactor`，起点 `9c9aa36`。

## 已接通的行为

首页仅注入 `IReviewProgressReader`，实际复用已有 Core 服务及按日整组 SQL。启动、语言切换和导航返回时刷新学习与复习各自的计数；显示当前语言的“复习 X/Y”。查询失败显示独立行内错误，不伪装为零进度；取消保留原计数。同语言重复刷新和语言切换后的过期成功／失败结果均不能覆盖最新页面状态。

新增 11 项测试包含真实词组导入与客观答题完成后的计数、语言独立目标、真实单窗口导航返回、数据库失败、取消、异步乱序、错误语言，以及 WPF 浅／深主题的实际绑定渲染。没有重写已验证的判题、词库或记忆算法。

## 严格 RED→GREEN 与证据边界

冻结前完成原有 12 处首页构造调用的新增能力适配；新测试主题命名空间拼写错误修正后才冻结，编译失败不计 RED。首轮新增测试 RED→GREEN 后全量 1739/1740，唯一失败为 `PresentationShellAndHome_OnlyRequestTheirRoleCapabilities`：原构造能力清单遗漏了新增的只读复习能力。

只补齐该能力清单，不放宽任何业务期望，作废首轮整体证据并重新冻结八份测试／夹具文件。新业务测试 SHA-256 仍为 `14CCD95DA6617CCDAA547EB6D2BF686EB05506F8EB174147667DD9A6B17E9F72`；架构测试为 `488B724EED2171B47AEC0D16BA6E47CF1C6A64296B3174AC7A3573AAAE6F37A8`。

编译通过后，仅临时让生产刷新方法读取后丢弃结果，实际执行 11 项，全部因值或异常断言失败，0 跳过。随后仅恢复生产方法；八份文件哈希不变。生产 XAML 初次标签闭合错误也已修正，没有计作 RED，没有提交任何临时错误。

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
& ./Dictation.Tests/bin/Debug/net10.0-windows/Dictation.Tests.exe --filter-class '*HomeReviewProgressTests' --minimum-expected-tests 11 --max-threads 4
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1740 --max-threads 4
git diff --check
```

重新冻结后的 RED：11/11 行为失败；最终全量 GREEN：1740/1740，0 失败、0 跳过，约 1 分 57 秒。构建 0 警告、0 错误；差异检查通过。实际 WPF 绑定、返回刷新与三层答题计数测试均已执行，不为记账重复运行。

这是 Task 3 的首页每日进度部分；复习答题入口、共用练习页面／浏览／音频及整个完整 GOAL 仍未完成。
