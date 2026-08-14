# Repository Guidelines

## 当前状态与批准目标

当前仓库仍只有 `Dictation.Core`（`netstandard2.1`）和 `Dictation.Presentation`（`net8.0-windows` WinForms）两个项目。批准的重构目标是 `.NET 10 WPF`，目标项目在迁移完成前可能尚不存在；新增目录或项目必须以实际代码为准，不得把目标结构当成当前状态。

## 目标结构与迁移方式

目标结构包括 `Dictation.Presentation`、`Dictation.Services`、`Dictation.DataAccess`，支持程序 `Dictation.TtsHost`、`Dictation.Updater`，构建工具 `Dictation.LexiconBuilder`，以及按需拆分的测试项目。按可运行、可验证的垂直切片逐步迁移，每个切片都交付真实业务链路；禁止先一次性铺设没有行为的空壳分层。

## 架构边界

采用传统三层边界和一个 Generic Host 管理的单一 DI 容器：

- `Presentation` 只负责 WPF UI、ViewModel、绑定和输入输出适配，不承载业务规则或 SQL。
- `Services` 只负责业务编排和用例流程；UI 与 MCP 共用这些 Services，不得绕过它们实现业务。
- `DataAccess` 只负责 CRUD 和 SQL，使用 Dapper 与 `Microsoft.Data.Sqlite`。

DTO、业务模型和持久化对象（PO）按职责分离；跨边界需要转换时使用简单 Mapper。DataAccessor 和只有一个实现的 Service 默认以具体类注入；仅 TTS、OCR、更新源、系统时钟等确有替换需求的 Provider seam 使用接口。禁止引入 DDD、CQRS、`Repository`/`Repository<T>`，也不要为没有扩展价值的接口或抽象增加复杂度。

## 数据与资源

程序启动时创建根目录运行数据目录：`CONFIG/`、`DATA/`、`LOGS/`、`BACKUPS/`，必要时创建 `CACHE/`。这些目录及其中的用户数据不能被发布更新覆盖。不可变词库和 TTS/OCR 模型放在 `Resources/`。发布采用自包含 `win-x64` 全量 ZIP 时，必须保留上述用户数据目录，不能把配置或用户数据打包成会覆盖现有内容的更新文件。

## 开发环境与命令

目标环境为 Windows 11、稳定版 Visual Studio 2026 和 .NET 10 SDK。以下命令用于当前基线；重构后按实际项目路径更新：

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.sln --configuration Debug
dotnet test Dictation.sln
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj
```

当前没有测试项目；重构第一切片必须先补齐测试基础设施，之后 `dotnet test` 必须纳入每个切片的验证。开发不应依赖 Edge、Windows TTS、Node、Docker、SQL Server、EF 或 CUDA。

## 编码约定

C# 使用四空格缩进、Allman 大括号和块级命名空间。类型、方法、属性使用 `PascalCase`；局部变量和私有字段使用 `camelCase`；启用可空引用并显式处理 `null`。WPF 表现层不写业务逻辑或 SQL。保持实现简单、职责清晰，避免为单一实现过度抽象。

## 测试与提交循环

每个业务垂直切片严格按以下循环执行：

1. 先写覆盖正常、边界、非法输入、异常和状态变化的测试。
2. 临时在实现中制造错误并实际运行测试，证明测试会失败；随后立即修正，该临时错误绝不能提交。
3. 修正最小实现，使相关测试全绿后做第一次中文提交。
4. 再将该业务从 WPF/ViewModel 经 Service、DTO/Mapper、DataAccessor/PO 打通到数据库或外部 Provider。
5. 全部测试、构建和相关运行冒烟通过后做第二次中文提交，然后开始下一个切片。

文档或纯配置变更不制造无意义的单元测试，改用与改动相称的静态检查和构建验证。任何提交都不得保留临时红状态。

## Git 与 PR

所有开发分支必须以 `codex/` 开头，禁止直接在 `master` 开发。单个垂直切片控制在约 2–3 人/小时，每次提交只包含一个可验证工作单元；提交摘要使用具体的中文动作句。提交前测试、构建和相关冒烟必须通过。PR 目标为 `master`，描述行为变化并列出实际验证命令；涉及 UI 时附前后截图。

## Agent 规则

没有用户明确授权不得启动 GOAL。环境缺失时只列出所需环境清单并等待用户处理，不擅自安装依赖或工具。实现前先确认已批准的需求和计划。保留无关的已有改动及未跟踪文件，不得擅自清理。

## 安全与发布

MCP 仅监听 `127.0.0.1`，不得绕过 Services 直接写数据库。不得提交令牌或其他凭据。发布为自包含 `win-x64` 全量 ZIP；配置和用户数据目录必须在更新时保留且不被覆盖。
