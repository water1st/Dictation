# 本地 MCP 导入 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans，在主目录现有 `codex/dictation-refactor` 串行执行；不派实现代理，每个计划只做一次最终只读审查。Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** AI 可发现离线查词、准备和整组暂存导入能力；导入后由用户在现有单窗口收件箱确认，不建立外挂数据库链路。

**Architecture:** Presentation 中的 MCP 是输入输出适配器，调用 Core 原服务。唯一 Generic Host／DI 容器管理可选 HTTP 监听器；使用微软 Kestrel、路由和官方 MCP SDK，不创建第二个 WebApplication／ServiceProvider，不手写 JSON-RPC。

**Tech Stack:** .NET 10／WPF、现有 IConfiguration／Dapper／SQLite、官方 `ModelContextProtocol.AspNetCore` 2.2.0；Codex TOML 使用 Tomlyn，不用正则或自制解析器。版本、许可证和实际发布体积在引入依赖时核验。

**Spec:** `docs/requirements/RefactorSpecification.md` 第2、4、9、11节。现有查词、暂存、用户确认和主题不重做。此计划具体实现方案待用户确认；确认前不改产品代码或安装依赖。

## Global Constraints

- 最终目录 `D:\Source\Repos\Dictation`；保持当前分支，不合并、推送、创建新 GOAL 或修改用户 Resources。
- 默认关闭；默认地址 `http://127.0.0.1:47821/mcp`，端口可设置为1024–65535，不自动漂移端口。绑定仅限 IPv4 Loopback，无 HTTPS、token、CORS、stdio 子进程或旧 SSE。
- 仅接受匹配当前地址和端口的 Host。Origin 缺失允许；存在时必须为该地址同源，`null`、其它网站、多个 Origin 拒绝。验证在 SDK 处理前，不放宽为任意 localhost／局域网地址。
- 所有查询／写入经过 Core；工具没有用户确认、学习库写入、进度修改或删除能力。客户端不能指定 SourceKind，暂存固定 `Mcp`。
- 两个设置文件都在根 `CONFIG/`：原 `settings.json` 不动；新增 `mcp.json` 保存 `Mcp.Enabled/Port`。仍由同一 IConfiguration 读取、同一 ConfigurationWriteExtensions 原子写回，避免首页主题保存覆盖 MCP 选项。
- MCP 注册只在用户点击设置按钮时修改其 Codex 配置；开发验证全部使用临时目录，不能修改本机真实 Codex 配置。
- 真实行为 RED 必须编译成功、测试执行后断言失败；冻结后只改实现。每阶段一次全量回归，当前最低1838项，增加测试后更新门槛，不重做旧 RED。

## 接入方案与生命周期

`McpHostedService` 使用注入的服务和 Kestrel，HTTP 请求由一个小型 `IHttpApplication<HttpContext>` 适配器委派到微软路由及 SDK `MapMcp("/mcp")`。适配器只负责创建／释放请求上下文和请求作用域，不解析协议，也不创建容器。Kestrel 实例由唯一容器注册的工厂创建；关闭时释放监听器，重新开启创建新实例，不尝试重启已停止的 Kestrel 对象。

设置保存后应用开关／端口变更，串行停止旧监听器再启动新监听器。启动根据持久化设置自动应用；退出停止接收请求并释放资源。端口被占用、配置无效或启动失败只进入“启动失败”状态，WPF仍可启动并提供修正／重试入口，不弹模态错误，也不假装服务已开启。配置成功保存与实际运行状态分别呈现。

`Program.Main` 仍先判定主实例；数据库初始化成功后再启动 Host，避免请求早于迁移。第二进程在 Host.Start 前退出，不抢占端口。不能因为 MCP 生命周期调整破坏原隐藏 TTS 的启动／退出。

## Review Focus

1. 端口冲突、重复开启／关闭和退出时有在途请求；不能拖死 WPF、泄漏端口或产生第二个业务实例。Task1真实TCP测试。
2. 错误 Host／Origin、跨站预检、未知工具及恶意工具参数；不能进入暂存服务。Task1真实SDK／HTTP测试。
3. 同一词组重复请求、跨语言请求及取消；只能暂存一次整组，不写 Placement／FSRS。Task1真实SQLite测试。
4. 用户已确认／丢弃的批次；状态查询仍可回读，不能仅从 Pending 列表推断“消失”。Task1独立批次读取测试。
5. 首页保存主题、Codex已有其它服务器／同名非本应用服务器、损坏TOML和并发修改；不覆盖无关配置。Task2真实文件与STA绑定测试。

### Task 1：真实本地 MCP 协议与导入链路

**Files:**
- 新建 `Core/Configuration/McpSettingsDto.cs`、`McpSettingsDocument.cs`、`McpConfigurationDocument.cs`、`McpSettingsService.cs`、`McpSettingsMapper.cs`；类型分别存放，读取复用 IConfiguration。
- 新建 `Core/Importing/IImportBatchReader.cs`；修改 `ImportInboxService.cs` 和唯一 `DictationCoreServiceCollectionExtensions.cs`。DAO现有 GetBatchAsync不重写。
- 新建 `Presentation/Mcp/` 中工具、注册、HostedService、HTTP适配、最小运行控制／状态合同和对应状态类型；一文件一个主要类型。
- 修改 `Presentation/Program.cs`、`Dictation.Presentation.csproj`；测试 `Tests/Core/McpSettingsTests.cs`、`ImportBatchReaderTests.cs`、`Tests/Mcp/McpHttpIntegrationTests.cs` 和需要的隔离夹具。

**Interfaces:**
- `McpSettingsService.Load()->McpSettingsDto`、`SaveAsync(McpSettingsDto,token)`；Enabled=false、Port=47821为缺失配置默认，非法持久化值报错，不能静默放宽绑定。
- `IImportBatchReader.GetBatchAsync(string batchId,token)->ImportBatchDto`；共享原 ImportInboxService 实例，映射复用 ImportInboxMapper，不存在批次报业务错误，取消传播。
- `IMcpStatusReader.GetStatus()->McpStatusDto`；Disabled／Starting／Running／Failed／Stopping，提供当前端点和简明错误，不泄露本机路径或堆栈。
- `IMcpHostControl.ApplyAsync(McpSettingsDto,token)`；控制监听生命周期，不承担导入业务。

**公开工具（SDK生成输入schema与描述）：**

| 工具 | 输入／结果与委派 |
| --- | --- |
| dictation_capabilities | 无输入；返回支持日英、正向查词、整组导入、需用户确认及禁止能力 |
| dictation_lexicon_search | language、query、limit=20、offset=0；委派 ILexiconSearch，返回中文义项与稳定source sense标识 |
| dictation_import_prepare | language、每行一词text；委派 ITextImportPreparation，返回未知／歧义候选，不写库 |
| dictation_import_stage | language、明确选择的ImportSelectionDto数组；委派 IImportStaging，固定Mcp，返回batchId／Added／Duplicate／待确认状态 |
| dictation_import_status | batchId；委派 IImportBatchReader，包含已完成批次，不执行决定 |
| dictation_diagnostics | 无输入；返回应用版本、监听状态、端点及确认流程说明，不读取用户教材、令牌或完整配置 |

- [x] **测试先行：** 缺失／旧配置默认关闭，合法保存重读，非法端口／类型、取消写入不改文件；同一个容器的UI／MCP暂存身份相同。实际SDK客户端 initialize→tools/list→search→prepare→stage→status；整组成员／中文／重复／用户待确认、已处理批次回读、非法／未知批次、取消及学习进度零写入。真实HTTP错误Host／Origin／预检不能产生批次；默认关闭不占端口，冲突不阻止宿主，关闭后端口释放。
- [x] **编译与RED：** 先完成SDK必要编译适配后冻结测试；仅生产临时绕过Host校验或省略整组成员，构建成功，观察上述相应行为断言失败，记录数量和哈希，绝不提交临时错误。
- [x] **最小实现GREEN：** 实现配置、只读批次能力、六个工具和同容器HTTP适配。限请求体1MiB、暂存每批最多100个选择；上限是协议批次，不截断单词组。验证边界／错误由工具转成清晰工具错误，内部异常只显示诊断码。复用现有业务校验，不添加第二套词形规则。
- [x] **验证并首次中文提交：** solution零错误、所有新增聚焦测试及唯一全量通过；运行实际HTTP客户端证明同进程／同容器／待用户确认，不以直接调用工具方法代替协议冒烟。记录依赖版本、许可证、传递依赖、发布体积及工具清单。

### Task 2：单窗口设置、Codex发现与用户确认

**Files:** 新建 `Presentation/ViewModels/McpSettingsViewModel.cs`、`Views/McpSettingsPage.xaml/.cs`；现有首页、导航、模板、注册只增加设置入口。新建 `Core/Configuration/CodexMcpRegistrationService.cs` 和必要的注册结果类型；新增 `Tests/Presentation/McpSettingsPageTests.cs`、`Tests/Core/CodexMcpRegistrationTests.cs`。文档 `docs/verification/2026-10-09-mcp-import.md`。

**Interfaces:**
- 设置VM只注入McpSettingsService、IMcpHostControl／IMcpStatusReader和注册服务；公开Enabled、原始PortText、运行状态、端点、错误、保存、重试、注册／注销命令。原始端口文本转整数失败时不得保存旧值。
- 注册服务 `RegisterAsync(string configPath,Uri endpoint,token)`／`UnregisterAsync(string configPath,token)`：只维护 `[mcp_servers.dictation]` 的HTTP url，不添加token或其它权限。Tomlyn验证语法并保留其它配置／注释，临时文件后原子替换。已存在不同URL同名服务器不覆盖；注销仅移除本应用所有且匹配的项。提交前确认原文件内容未变，变更则报重试错误，不覆盖外部新修改。
- 真实配置路径由组合根根据CODEX_HOME（只读）或用户目录解析，测试显式临时路径；不绑定本机绝对路径。端口变更后显示“需要更新Codex注册”，不擅自改用户配置。

- [ ] **测试先行并有效RED：** 真实STA／XAML测试默认关闭、端口非法编辑、保存状态与启动冲突、开关／重试、同窗口返回、浅深主题、迟到结果隔离。TOML测试创建／重复注册／注销、保留其它服务器和注释、不同URL冲突、语法错误、取消及外部并发修改均不得破坏原文件；冻结后仅生产制造相应行为错误，编译成功再实际断言RED。
- [ ] **打通GREEN：** 首页新增“AI导入设置”入口，沿用ContentControl导航／动画，无新Window。注册按钮由用户触发；成功提示客户端重载配置，并提供可复制端点。MCP导入进入现有Inbox，用户返回／进入列表正常刷新即可，不添加事件总线或轮询；通过真实客户端暂存后，从真实列表读取并由原确认服务送入学习／复习验证。
- [ ] **验证并第二次中文提交：** 新增聚焦、solution构建、唯一完整回归和协议→SQLite→UI收件箱冒烟通过；提高测试门槛，中文提交。对本计划整体仅一次最终只读审查，Important／Critical集中一次TDD修正，Minor记录；证据归档后只清理本计划临时目录。完整GOAL仍含统计／更新发布／实际模型验收／最后OCR，不因此宣称完成。

## 选型依据与实施门槛

[官方SDK HTTP文档](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html)提供HTTP、工具schema和协议处理；[2.2.0发布源码](https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/v2.2.0/src/ModelContextProtocol.AspNetCore/McpEndpointRouteBuilderExtensions.cs)的MapMcp依赖IEndpointRouteBuilder，而非必须创建WebApplication。微软提供[Kestrel](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.core.kestrelserver?view=aspnetcore-10.0)和[IHttpApplication](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.hosting.server.ihttpapplication-1?view=aspnetcore-10.0)公共接入合同。

本方案会把ASP.NET Core运行库带入自包含ZIP，这是真实成本，不能只报SDK的NuGet文件体积。实施先测量发布增量，不引入MVC、Swagger、认证框架或其它Web服务。若公共API不能在唯一容器下通过实际协议测试，停止这一接入方案复核，不偷偷改成第二容器或手写MCP。

Codex配置格式依据[官方OpenAI文档](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)及本机只读 `codex mcp add --help`，使用 `mcp_servers.<name>.url`。产品不依赖本机Node／Codex CLI；TOML解析优先[Tomlyn](https://github.com/xoofx/Tomlyn)，引包前核验当前稳定版与语法树保留能力。该阶段只写计划，不运行注册命令、不改真实Codex设置。
