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
- 首次全量1868项：1863通过、5项TTS生命周期失败。读取真实子进程stderr确认DI程序集缺失：共享框架让主输出不再复制DI，而TTS原声明仅基础NETCore。TTS也声明使用同一已发布ASP.NET共享框架，并移除重复DI包；不添加Web服务或第二业务容器，不修改旧测试。修正后1868/1868通过。

## 验证命令

```powershell
dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1868 --max-threads 4
dotnet publish Dictation.Presentation/Dictation.Presentation.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/mcp-task1 --disable-build-servers -m:1
```

Task1运行库修正后：38项聚焦全部通过；解决方案0警告0错误；全量1868/1868通过、跳过0，耗时3分19秒。三份冻结测试哈希保持不变。实际自包含产物的TTS控制管道返回Pong、ShutdownAcknowledged，子进程退出码0；许可证输出存在。最初PowerShell构造Nullable枚举发生参数转换错误，该脚本结果不作产品失败或验收证据；使用已核验控制帧及产品解码器完成上述实际冒烟。

## Task2：单窗口设置和Codex发现

首页“AI 导入设置”沿用原ContentControl、页面作用域与前进／返回动画。设置页展示默认关闭、原始端口文本、只读可复制地址、实际运行状态及四个命令。保存成功与监听成功分别显示；非法文本不保存旧端口，冲突后按已保存配置重试。返回时取消页面操作，迟到结果不更新旧页面。

注册与注销仅由用户点击触发。配置路径由组合根只读解析CODEX_HOME或用户目录，测试明确提供临时路径；整个开发过程未改真实Codex配置。端口变更显示“需要更新Codex注册”，不自动写客户端文件；旧端点需主动注销后重注册。TOML同名非本应用配置不覆盖，损坏语法／非法编码／取消／锁定／外部快照变化均保留原文件。

Core注册服务负责用例编排，内部Tomlyn语法编辑器不做IO，文件写入器只负责快照和原子替换。Windows隔离探针证实持有防写句柄时File.Move覆盖报AccessDenied，File.Replace可以成功；生产据此选择BCL原子替换，不自行实现TOML解析或文件事务框架。

新增Tomlyn 2.10.1，[官方版本源码](https://github.com/xoofx/Tomlyn/tree/2.10.1)，BSD-2-Clause，许可证随包LICENSES/tomlyn.txt。.NET10目标无新增传递依赖，缓存中运行DLL为323,072字节，约0.31MiB；依赖已还原后运行完全离线。不引入CLI或另外的注册框架。

### Task2冻结与行为RED

| 测试文件 | SHA-256 |
| --- | --- |
| Core/CodexMcpRegistrationTests.cs | 4457B2CE83EA1678ED3791835D2D2C54D1BBA7DE213A47C9D2F9E0BD64B123A5 |
| Core/CodexMcpSyntaxTests.cs | 834DDC1ABDA939394C913E5A481334D34458FA6231735FAA37C4457B47D3A995 |
| Presentation/McpSettingsPageTests.cs | DC1AE841CD0D3259E565908A9F6FF14CD39E5F6D9E6B2C1EBDC5CD96E65F3492 |
| Architecture/CoreLayerArchitectureTests.cs | BA19B4FE9D43FE4D28471E0979BEE1E7C90BCB870784F397D52A265F2B9349CC |

注册骨架编译成功后20/20行为断言失败；合法TOML变体5/5行为断言失败。仅完善生产代码后25/25通过，哈希不变。

UI首轮测试未布局便输入，绑定未真实生效，明确作废。修正真实STA布局及输入绑定来源断言后重新冻结；仅生产SaveAsync临时空操作，build零警告／错误，7项7失败（非法输入应有错误，保存后状态应变化）。恢复生产保存后37项新增聚焦全部通过。

首次全量1905项：1904通过，1项旧架构反射断言仍限定Shell两个接口。按批准方案增加最小IMcpSettingsNavigation，更新该合同清单并断言接口只有一个方法；没有删业务期望或测试。重新冻结后仅生产导航临时不前进，build零警告／错误，真实页面测试1/1断言失败：期望McpSettingsViewModel、实际HomeViewModel。恢复导航后再次验收，临时错误均不提交。

### Task2真实链路与渲染

37项新增聚焦包含真实SDK暂存日／英组→SQLite→原Inbox列表／详情→用户确认学习／复习，确认后批次Completed、对应整组Placement准确。未改已有导入规则或词形逻辑。STA测试还覆盖动态浅深主题、端口冲突／重试、实际绑定、页面作用域释放和迟到结果。

隔离STA工具用正式App DataTemplate生成UserControl，输出`Passed=true AppTemplate=true SingleWindowNavigation=true ClientConfigUntouched=true LightAndDark=true`。浅深渲染截图已实际查看；这是无窗口模板／导航冒烟，不冒充人工整机点击验收。工具初次编译缺测试友元授权及ThemeMode别名，修正工具后成功，不计产品RED。

渲染证据：[首页新增入口](images/2026-10-09-mcp-home.png)、[浅色设置页](images/2026-10-09-mcp-light.png)、[深色设置页](images/2026-10-09-mcp-dark.png)。截图内的客户端路径是隔离临时路径，不是用户真实Codex配置。

此记录只验收MCP切片，不代表整个GOAL完成；统计／更新发布／正式模型和最后OCR仍按完整目标执行。

Task2最终验收：`dotnet build Dictation.sln --configuration Debug --no-restore --disable-build-servers -m:1`零警告／错误；`dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1905 --max-threads 4`实际1905/1905通过、跳过0，3分27秒。上述冻结哈希保持不变。自包含Release发布成功，Tomlyn.dll为323,072字节，LICENSES/tomlyn.txt存在；未打包CONFIG／DATA等用户运行目录。
