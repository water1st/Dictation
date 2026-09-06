# Repository Guidelines

## 当前状态与批准目标

当前解决方案已迁入 `.NET 10`：`Dictation.Presentation` 使用 WPF，`Dictation.Core` 承载业务服务，`Dictation.DataAccess` 负责持久化；另有 `Dictation.TtsHost`、`Dictation.LexiconBuilder` 和 `Dictation.Tests`。旧 WinForms、Edge/System TTS 与独立 Services 项目已移除。其余目标功能仍按已批准计划分片实现，不得把计划中的项目或功能当成已存在。

## 目标结构与迁移方式

物理三层固定为 `Dictation.Presentation → Dictation.Core → Dictation.DataAccess`；业务按 `Core/Configuration`、`Core/Importing`、`Core/Learning`、`Core/Lexicon`、`Core/Tts` 等目录内聚组织。支持程序包括现有 `Dictation.TtsHost` 和待实现的 `Dictation.Updater`。按可运行、可验证的垂直切片迁移，不铺设没有行为的空壳；已被新链路取代且没有引用的旧实现应删除，不留无用兼容层。

## 架构边界

采用传统三层边界和一个 Generic Host 管理的单一 DI 容器：

- `Presentation` 只负责 WPF UI、ViewModel、绑定和输入输出适配，不承载业务规则或 SQL。
- `Core` 负责业务编排和用例流程，以及对应 DTO、Mapper、Provider 合同；UI 与 MCP 共用 Core 内的业务服务，不得绕过业务层。
- `DataAccess` 只负责 CRUD 和 SQL，使用 Dapper 与 `Microsoft.Data.Sqlite`。

`AddDictationCore()` 是唯一业务注册入口，只在组合根注册一次；禁止重建独立 Services 项目、兼容命名空间或第二套容器。DataAccess 不得反向引用 Core。

DTO、业务模型和持久化对象（PO）按职责分离；跨边界需要转换时使用简单 Mapper。DataAccessor 和只有一个实现的 Service 默认以具体类注入；仅 TTS、OCR、更新源、系统时钟等确有替换需求的 Provider seam 使用接口。禁止引入 DDD、CQRS、`Repository`/`Repository<T>`，也不要为没有扩展价值的接口或抽象增加复杂度。

## 内聚性与依赖选型

代码按业务职责保持高内聚、低耦合；同一能力的读取、写入和校验应通过一个清晰入口对调用方呈现，不得为了分层形式把紧密相关的操作散落到互不相干的命名空间。封装可以统一入口或隔离第三方 API，但封装内部必须复用成熟实现，不能复制已有库的核心功能。

新增能力按以下顺序选型：

1. 优先使用 .NET BCL、`Microsoft.Extensions.*` 和仓库已有依赖。
2. 仍有缺口时，选择刚好覆盖实际需求、主流、活跃维护、许可证兼容的轻量库。
3. 不为少量功能引入覆盖范围远超需求的重型框架，也不并存功能重叠的多个库。
4. 仅在成熟方案确实不提供所需能力时实现最小缺口，不自行重写其已经提供的部分。

每个新包必须能说明其必要性，并检查维护状态、离线可用性、包体积、传递依赖及 .NET 10/Windows 兼容性；无法证明必要性时不引入。

## 数据与资源

程序启动时创建根目录运行数据目录：`CONFIG/`、`DATA/`、`LOGS/`、`BACKUPS/`，必要时创建 `CACHE/`。这些目录及其中的用户数据不能被发布更新覆盖。不可变词库和 TTS/OCR 模型放在 `Resources/`。发布采用自包含 `win-x64` 全量 ZIP 时，必须保留上述用户数据目录，不能把配置或用户数据打包成会覆盖现有内容的更新文件。

## 开发环境与命令

环境为 Windows 11、稳定版 Visual Studio 2026 和 `global.json` 指定的 .NET 10 SDK。常用验证命令：

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1372
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj
```

`Dictation.Tests` 使用 xUnit v3 与 Microsoft.Testing.Platform，测试命名采用 `行为_场景_预期`。聚焦测试用 `--filter-class` 或 `--filter-method`，并指定合理的 `--minimum-expected-tests`；零测试不算通过。全量数量不得少于当前基线 1372 项。开发不应依赖 Edge、Windows TTS、Node、Docker、SQL Server、EF 或 CUDA。

## 编码约定

C# 使用四空格缩进、Allman 大括号和块级命名空间。类型、方法、属性使用 `PascalCase`；局部变量和私有字段使用 `camelCase`；启用可空引用并显式处理 `null`。WPF 表现层不写业务逻辑或 SQL。保持实现简单、职责清晰，避免为单一实现过度抽象。

## 测试与提交循环

每个业务垂直切片严格按以下循环执行：

1. 先写覆盖正常、边界、非法输入、异常和状态变化的测试。
2. 在可编译的最小生产骨架上冻结测试文件，仅在生产实现中临时制造行为错误并实际运行测试。RED 必须是项目编译成功、目标测试确实执行，并因行为断言失败；编译失败、零测试或测试基础设施错误都不算 RED。
3. 不修改已冻结的测试，只修正生产实现，使同一批测试由红转绿后做第一次中文提交；临时错误绝不能提交。
4. 再将该业务从 WPF/ViewModel 经 Service、DTO/Mapper、DataAccessor/PO 打通到数据库或外部 Provider。
5. 全部测试、构建和相关运行冒烟通过后做第二次中文提交，然后开始下一个切片。

若发现测试规范或断言本身错误，必须作废该轮 RED 证据；先修正测试并重新冻结，再从“编译成功但行为断言失败”开始完整重跑 RED→GREEN。文档或纯配置变更不制造无意义的单元测试，改用与改动相称的静态检查和构建验证。任何提交都不得保留临时红状态。

## Git 与 PR

所有开发分支必须以 `codex/` 开头，禁止直接在 `master` 开发。单个垂直切片控制在约 2–3 人/小时，每次提交只包含一个可验证工作单元；提交摘要使用具体的中文动作句。提交前测试、构建和相关冒烟必须通过。PR 目标为 `master`，描述行为变化并列出实际验证命令；涉及 UI 时附前后截图。

## Agent 规则

没有用户明确授权不得启动 GOAL。环境缺失时只列出所需环境清单并等待用户处理，不擅自安装依赖或工具。实现前先确认已批准的需求和计划。保留无关的已有改动及未跟踪文件，不得擅自清理。

## 安全与发布

MCP 仅监听 `127.0.0.1`，不得绕过 Core 业务服务直接写数据库。不得提交令牌或其他凭据。发布为自包含 `win-x64` 全量 ZIP；配置和用户数据目录必须在更新时保留且不被覆盖。
