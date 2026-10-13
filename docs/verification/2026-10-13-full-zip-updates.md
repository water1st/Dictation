# 全量 ZIP 更新验证记录

## 范围与起点

落实已批准规格第 3 节，沿用 `D:\Source\Repos\Dictation` 和 `codex/dictation-refactor`，BASE `a10b862`。本轮不新增更新渠道、安装器、增量更新或在线词库同步；未推送、合并或发布 Release。原统计阶段的 2018 项基线不重做 RED。

## Task 1：全量包安装最小能力

包始终完整下载，文件清单只用于确定程序／不可变资源归属，不是增量更新协议。安装器校验 SHA-256、三段版本及 ZIP／清单一致性，使用 .NET 10 的流式异步 ZipFile 解压。同一个只读包句柄从哈希到解压保持打开，避免重新打开时取到不同内容。实际替换仅涉及旧／新清单文件，未知用户文件碰撞提前拒绝。

CONFIG、DATA、LOGS、BACKUPS、CACHE 从不进入受管理清单；原用户文件与目录保留。旧文件移动到独占 CACHE 任务备份，异常或取消后恢复已移动的内容；回滚不被取消打断，无法恢复的备份不删除。当前只验证安装失败的回滚，尚未接入父进程等待、重启确认及重启失败后的恢复，不把这些待做环节算成已完成。

| 最终冻结测试 | SHA-256 |
| --- | --- |
| UpdateInstallerTests.cs | 4DE8EEA1A5E1AC50E2C0659A11A3E3EA61A86AB7CB93FE14C0BEB57FBD7D4948 |
| UpdateInstallerPathBoundaryTests.cs | 0C097BDCF95F873274FE71D017CC1ACB99FD64F0C00706A32EF3064428412739 |

- 主测试 29 项先写后实现。初次非法版本夹具同时使用坏哈希，冻结作废；拆开非法字段后重新冻结，构建 0 警告／0 错误，29 项实际执行、28 个行为断言失败、1 项取消通过、0 跳过。
- 只补生产实现后，29/29 转绿，冻结测试未改。
- 路径边界追加 3 项。根目录两种斜杠已正确拒绝；junction 初次测试的清理异常掩盖断言，该轮作废。先仅补夹具 finally 移除自建链接，再冻结、构建 0 警告／0 错误，3 项实际执行、1 项“应拒绝却未抛异常”行为失败、2 通过、0 跳过。只修生产检查整个 CACHE/Updates 前缀后转绿。
- 最终联合 **32/32，通过、0 跳过**，构建 **0 警告／0 错误**，两个冻结 SHA 未变。真实文件锁证明旧文件可恢复；实际 Windows junction 验证不会把新程序或任务数据写出安装目录。

所有 ZIP、假程序、链接及锁均在测试独占 TEMP 内构造。没有对当前预览、源码目录、用户实际配置或数据库执行安装。完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2050 --max-threads 4` 实际 **2050/2050，通过、0 跳过**，5 分 56 秒；构建 0 警告／0 错误，冻结 SHA 未变，差异检查通过。

## 后续明确未完成

隐藏 Updater、父 PID 等待、启动确认／失败恢复、启动异步检查、非模态按钮、GitHub CI ZIP／SHA-256 发行、许可门槛以及最终真实预览更新均未在本阶段实施。完整 GOAL 还包括最后 OCR／人工复核和交付清理，不能用此安装器绿灯代替。

## Task 2：正式新版查询最小能力

安装器已提交 `7e1a7ed`。查询通过固定仓库的官方 API 获取元数据，过滤草稿、预发行、相同／旧版本及旧工作流的非数字标签，只返回同一标签下 ZIP／SHA-256 两份成品；缺资产、坏 JSON、非 HTTPS／外部仓库地址明确失败，不冒充“没有新版本”。404 表示还没有发行，其它服务错误传播；取消与检查超时停止 HTTP。

`UpdateService` 经唯一 Core 注册提供窄 `IUpdateReleaseQuery`。开发目录没有 package-manifest 时直接返回空，不联网或写数据库；发行目录以实际安装清单版本比较，不能用源码程序集默认版本误判降级。检查不下载任何更新包。生产提供器采用 BCL HttpClient，JSON 文档保持 internal、纯转换在对应 Mapper，无新依赖。

| 冻结测试 | SHA-256 |
| --- | --- |
| GitHubUpdateProviderTests.cs | E455825554B13EB74D3C40C61923E3AF44ECB6240F77129527F52477EFBF95D8 |
| UpdateReleaseQueryTests.cs | ECE9A238F63286B921E7FB9B390B764A91AB4B90621D3D26B3176A4D66BBB374 |

提供器可编译骨架：15 项实际执行、10 个行为断言失败、5 通过；业务入口骨架：3 项实际执行、2 个值断言失败、1 个开发目录用例通过。两次构建均 0 警告／0 错误，0 跳过。仅补生产实现后，联合 **18/18** 转绿、解决方案 0 警告／0 错误，冻结 SHA 不变。测试只替换 HTTP 传输，真实解析、Mapper、发行目录文件读取、DI 和版本判断均执行，未访问真实网络。

提交前完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2068 --max-threads 4` 实际 **2068/2068，通过、0 跳过**，5 分 58 秒；构建 0 警告／0 错误，两个冻结 SHA 不变、差异检查通过。本段不代表 UI、下载准备、隐藏进程、重启恢复或 CI 已实现；Task 2 整体仍未完成。

## Task 2：完整下载及独占更新器准备

用户点击后才调用准备能力。公开 DTO 的地址重新验证为固定仓库、同一三段发行；先读取有大小上限的 SHA-256 旁文件，再流式下载完整 ZIP，验证哈希和现有 Task 1 的包清单／路径／版本规则。不启动进程、不关闭窗口、不替换原文件。runner 只复制当前安装清单管理的 `Updater/` 文件，未知用户文件不跟随复制。下载任务独占 `CACHE/Updates/<随机标识>`，断流或取消只清理自己的任务。

- `UpdatePreparationTests.cs` 冻结 SHA：`E61E82B2C4B8EE9A1AAFE013969020E7C3E2DC460B5C8361AB8C29A07DE1D90A`。可编译骨架 0 警告／0 错误，12 项实际执行、11 个行为断言失败／1 取消通过；只补实现后 12/12。
- `UpdateDownloadInterruptionTests.cs` 冻结 SHA：`58BF08609220EF34FE713E396AC4EDCE1EB78BDBF8BA61026CE28BDE1EC42040`。临时省略生产任务清理，编译 0 警告／0 错误；真实流先写字节再断开／取消，两项均因残留包断言失败。恢复清理后联合 **14/14**，0 跳过，冻结未改。
- 联合测试参数采用 `--filter-class '*UpdatePreparationTests' '*UpdateDownloadInterruptionTests'`；两次错误的组合表达式被测试运行器拒绝、0 项执行，不作为任何测试成功或 RED 证据。

当前候选完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2082 --max-threads 4` 实际 **2082/2082**，0 失败／0 跳过，5 分 43 秒；解决方案构建 **0 警告／0 错误**。两份冻结 SHA 不变，差异检查通过。本段尚不代表隐藏进程、UI、重启确认及 CI 完成。

## Task 2：安装收据与启动失败恢复能力

下载准备已提交 `20832d3`。安装返回独占 CACHE 事务收据，旧文件备份保留到调用方确认启动成功。确认／回滚以两个窄接口映射到同一安装器；恢复记录与收据、根目录、版本匹配，拒绝外部目录和不安全路径。确认只删除该事务；恢复只改受管理程序文件，保留运行目录与未知用户文件。恢复使用旧文件副本，全部恢复成功才清理，锁冲突中断后可在解锁后重试。

- `UpdateRecoveryTests.cs` 冻结 `DA878137AE5643DB8FCA9ED83060FE105D7C0F07F9B0F0CFD47B8BC62D1B4D1E`。可编译骨架 0 警告／0 错误；7 项执行、6 行为断言失败／1 通过，随后只补生产实现。
- `UpdateRecoveryRetryTests.cs` 最终冻结 `32343129AD7F9C6B4D7D6248AEBDD4DAC7CF5E0B6280D2AFB9537491B67F8955`。首轮只命中 Windows 锁异常类别差异，作废该轮；修正测试规范，明确检查备份存在后重新冻结。生产临时把 Copy 改为 Move，编译成功，1 项因旧备份应存在但不存在的行为断言失败；恢复 Copy 后转绿。冻结文件未改。
- 与原包安装及路径边界联合 **40/40**，0 跳过；最终解决方案构建 **0 警告／0 错误**。真实 Windows 文件锁验证恢复中途失败不丢旧备份，解锁后旧程序与清单均可恢复。

当前收据能力完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2090 --max-threads 4` 实际 **2090/2090**，0 失败／0 跳过，5 分 43 秒；解决方案构建 0 警告／0 错误，两份冻结SHA未变，差异检查通过。隐藏runner的父进程等待／重启／就绪通知和WPF工具栏仍待接入，不把Core恢复能力当作整条更新链路已经完成。

## Task 2：单窗口入口与真实隐藏更新器

恢复能力已提交 `ba4791e`。主窗口组合独立 UpdateViewModel 与 Core 就绪通知，不把更新状态塞入语言页面或导航 Shell。Loaded 后先写有效事务的就绪信号，再异步检查；只显示工具栏按钮和页内错误，不弹框、不自动下载。用户点击才准备完整包；只有隐藏 runner 真正启动成功才正常关闭主窗口。关闭时取消并屏蔽晚到响应。

Updater 为独立 WinExe，使用微软 Generic Host、命令行配置绑定与同一 Core 注册入口；没有新业务层或数据库旁路。等待原 PID 正常退出，调用原安装能力，启动新版并等待迁移／Host／Loaded 确认；失败先停自己启动但未就绪的新版，再恢复旧程序并重启。所有错误记入 LOGS；新版已经就绪时若备份清理失败，只记日志保留备份，不杀正在运行的新版回滚。

| 冻结测试 | SHA-256 | 可编译行为 RED |
| --- | --- | --- |
| UpdateViewModelTests.cs | 506F3062072947AE9503405F99B863C2969F5168E764B48D7A46B380006ED32B | 8项，5失败／3通过 |
| UpdateLauncherTests.cs | BF323AD2600ED09572ED3BEE7A5FCF34A8AFF2C062591D35D9D4CFDBD910345A | 2项，2行为失败 |
| UpdateStartupNotifierTests.cs | 973330350C00E893339E94517C72E90660D57CBE5A81885C06C887AC24574449 | 4项，3失败／1普通启动通过 |
| UpdaterProcessTests.cs | 22F165B332AF6F7664228127BF869A20188AC99B0A3E05DE1AC2F4BDA5CBE565 | 2项，2错误成功码断言失败 |
| UpdateToolbarTests.cs | 6862BEFFBFA8CE23A78D5D9449098591CA28DBAA16CA8ABAE2072E5C3EECE802 | 2项，2真实入口缺失断言失败 |

各轮构建均 0 警告／0 错误，测试实际执行、0 跳过；只补生产实现转绿。原三个直接窗口构造调用仅做新 DI 参数的编译适配，不改原导航或 MCP 业务断言。联合新 18 项及原单窗口／MCP 19 项 **37/37**，0 跳过，解决方案构建 **0 警告／0 错误**。

真实成功冒烟使用本计划独占 scratch 目录：复制本次程序输出，模型／词库只借用已下载资源的只读链接，不修改旧预览、重新构建词库或重新验证音频。父探针仍运行时安装版本保持 1.0.1；隐藏 Updater 窗口句柄为 0；原进程退出后安装 1.0.2，真实 WPF 主窗口 Loaded 并确认，Updater 退出码 **0**；五个运行目录的 `keep.bin` 均不变，安装事务确认后为 **0**。主窗口正常 Close、WaitForExit 成功，不声称未取得的主程序退出码。成功测试目录和资源链接已清理，没有修改实际用户数据。

本候选完整命令 `dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2108 --max-threads 4` 实际 **2108/2108**，0 失败／0 跳过，5 分 31 秒；解决方案构建 **0 警告／0 错误**，五份冻结SHA未变，差异检查通过。CI、正式自包含 ZIP、许可门槛及最终预览交付仍属于后续 Task 3，完整 GOAL 也仍包含最后 OCR 与收尾清理，不能以本段结束整项目目标。
