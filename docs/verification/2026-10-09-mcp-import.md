# 本地 MCP 导入验收记录

## 范围与实现

主目录 `D:\Source\Repos\Dictation`，分支 `codex/dictation-refactor`；基线 `7137962`，旧功能与旧 RED 不重做。Task1增加官方 SDK Streamable HTTP 输入适配，复用唯一 Generic Host、Core 导入能力及原 SQLite 数据访问；无第二容器、协议手工解析或外挂数据库写入。

默认关闭，`http://127.0.0.1:47821/mcp`；端口可配置但地址不能扩大。六个工具为 `dictation_capabilities`、`dictation_lexicon_search`、`dictation_import_prepare`、`dictation_import_stage`、`dictation_import_status`、`dictation_diagnostics`。暂存固定来源 MCP，整组进入待确认 Inbox；不能确认、删除或写学习进度。请求上限1MiB，暂存每批1–100个选择，不截断词组。

独立 `CONFIG/mcp.json` 仍通过 IConfiguration 读取及现有原子写回扩展保存；不覆盖主题。损坏 JSON 保留失败状态而不阻止 WPF；重新保存有效配置可恢复。主实例迁移数据库后才启动 Host，次实例不会绑定端口。

## 严格 RED → GREEN

冻结后的测试 SHA-256：

| 测试文件 | SHA-256 |
| --- | --- |
| Core/McpSettingsTests.cs | D211E7C9127F52BC18BFE7F51BD6DED926257689EB72A02C979336430460B489 |
| Core/ImportBatchReaderTests.cs | 053DA0930FF8002BF7FF24B66BBB9370D61D3EE704D136B7BEDB64684CA01D47 |
| Mcp/McpHttpIntegrationTests.cs | 998D07DFD32E57ECF58A1F620E9212560EAB75A0B07206B03D859B99E64FE1B4 |

Core骨架：项目编译成功后11项实际执行，11项行为断言失败；仅完善生产实现后11/11通过。HTTP故障及冻结前夹具编译适配不计 RED。

HTTP冻结后仅临时绕过生产 Host／Origin 校验：build成功，0警告0错误；`WrongHostOrOrigin_IsRejectedBeforeAnyToolWrite` 8项全部失败，均为期望 `Forbidden`、实际 `OK`。恢复生产校验后同一测试8/8通过，全部新增聚焦30/30通过，冻结哈希不变。损坏 JSON另有真实断言 RED：期望 Failed、实际 Disabled；实现读取错误标记后转绿。临时错误已撤除，不提交。

真实 SDK 客户端与 TCP 验证 initialize／发现／查词／准备／暂存／批次回读；日中、英中释义及完整词形、重复、已丢弃批次、同源／异源／多Origin、预检、非法参数、未知工具、取消、1MiB限制、默认关闭、端口冲突重试、改端口和在途请求停止均有断言。学习 Placement／学习答题记录零写入。

## 依赖与发布成本

- 官方 `ModelContextProtocol.AspNetCore`／`ModelContextProtocol`／`ModelContextProtocol.Core` 2.2.0，Apache-2.0；完整许可证随程序 `LICENSES/mcp-sdk.txt` 发布。[官方项目](https://github.com/modelcontextprotocol/csharp-sdk)。
- SDK传递依赖包括 `Microsoft.Extensions.AI.Abstractions` 10.8.3 与微软 Extensions；没有云模型适配器或运行时在线请求。删除被 ASP.NET Core 共享框架提供的4个重复 Presentation Extensions 包引用，restore／build无 NU1510。
- 自包含 `win-x64` Release实际发布到独立 `artifacts/mcp-task1`。运行配置包含 `.NET`、WindowsDesktop、ASP.NET Core 10.0.12；使用离线本机协议，不要求用户预装运行时。
- 与已存在 `artifacts/preview-56ec99f` 根文件名称比较，新增116个文件共27,358,728字节（约26.1MiB），单独ZIP压缩12,082,832字节（约11.5MiB）。这是运行库／SDK新增文件成本，不把旧版本到当前业务程序集变化误称为MCP成本；不包含资源模型。ASP.NET自包含会携带框架的MVC等程序集，但程序不注册MVC、Swagger、认证或额外Web服务。
- 初次HTTP启动缺 DiagnosticListener，由实际异常及[微软宿主源码](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Hosting/Hosting/src/GenericHost/GenericWebHostBuilder.cs)核实后注册必要框架组件，不建立第二宿主。
- 首次全量1868项：1863通过、5项TTS生命周期失败。读取真实子进程stderr确认DI程序集缺失：共享框架让主输出不再复制DI，而TTS原声明仅基础NETCore。TTS也声明使用同一已发布ASP.NET共享框架，并移除重复DI包；不添加Web服务或第二业务容器，不修改旧测试。修正后验证待填写。

## 验证命令

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1868 --max-threads 4
dotnet publish Dictation.Presentation/Dictation.Presentation.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/mcp-task1 --disable-build-servers -m:1
```

Task1运行库修正后：38项聚焦全部通过；解决方案0警告0错误；全量1868/1868通过、跳过0，耗时3分19秒。三份冻结测试哈希保持不变。实际自包含产物的TTS控制管道返回Pong、ShutdownAcknowledged，子进程退出码0；许可证输出存在。最初PowerShell构造Nullable枚举发生参数转换错误，该脚本结果不作产品失败或验收证据；使用已核验控制帧及产品解码器完成上述实际冒烟。

Task2设置页、Codex注册及用户确认UI冒烟尚未完成。此记录不代表整个GOAL完成。
