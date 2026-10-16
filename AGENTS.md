# Repository Guidelines

## 项目架构

解决方案采用 `.NET 10`：`Dictation.Presentation` 使用 WPF，`Dictation.Core` 承载业务服务，`Dictation.DataAccess` 负责持久化；另有 `Dictation.TtsHost`、`Dictation.Updater`、`Dictation.LexiconBuilder` 和 `Dictation.Tests`。开发和文档均以当前仓库代码、正式需求及已批准的后续任务为准。

## 项目结构与实现方式

物理三层固定为 `Dictation.Presentation → Dictation.Core → Dictation.DataAccess`；业务按 `Core/Configuration`、`Core/Importing`、`Core/Learning`、`Core/Lexicon`、`Core/Tts` 等目录内聚组织。支持程序包括隐藏的 `Dictation.TtsHost` 和 `Dictation.Updater`，后者复用 Core 更新能力，不直接写用户数据库。按可运行、可验证的垂直切片实现，不铺设没有行为的空壳。

## 架构边界

采用传统三层边界和一个 Generic Host 管理的单一 DI 容器：

- `Presentation` 只负责 WPF UI、ViewModel、绑定和输入输出适配，不承载业务规则或 SQL。
- `Core` 负责业务编排和用例流程，以及对应 DTO、Mapper、Provider 合同；UI 与 MCP 共用 Core 内的业务服务，不得绕过业务层。
- `DataAccess` 只负责 CRUD 和 SQL，使用 Dapper 与 `Microsoft.Data.Sqlite`。

`AddDictationCore()` 是唯一业务注册入口，只在组合根注册一次；禁止重建独立 Services 项目、兼容命名空间或第二套容器。DataAccess 不得反向引用 Core。

DTO、业务模型和持久化对象（PO）按职责分离，不要求机械地为每个 PO 再复制一套业务模型。纯模型转换放在对应业务目录的 Mapper 中，不能散落在 Service、ViewModel 或 DataAccessor 中；Mapper 不查询数据库、不编排业务、不修改状态。Service 负责业务校验与用例编排，DataAccessor 负责 SQL、连接和事务；普通请求 DTO 创建、UI 属性绑定及数据库查询本身不等于需要额外包装的映射。纯转换默认使用简单的内部静态 Mapper，不建立通用 IMapper 或全局映射中心。

禁止引入 DDD、CQRS、`Repository`/`Repository<T>`；不要为形式增加接口、基类、兼容层或框架。但“不过度设计”不等于取消职责划分，“减少抽象”不等于禁止有实际价值的接口。减少类和文件数量不是设计目标，不能把 DTO、映射、业务、基础设施等不相关职责揉进一个类。

## 能力接口、组合与可见性

- 接口以实际抽象价值为依据，而非实现数量。有明确能力边界、跨模块复用、实现替换或依赖隔离需求时应使用接口，即使当前只有一个实现；不存在这些需求时直接通过 DI 注入具体类，不机械配对 `IXxx + Xxx`。
- 按调用方需要的能力拆分接口。同一实现可以实现多个小接口；调用方只依赖真正使用的能力，不应被迫接收完整实现的所有操作。有真实组合需求时可定义组合接口，但不得因此建立类继承体系或预建没有消费者的接口。
- 优先通过 DI 组合能力，不通过无必要的类继承复用实现或字段。框架要求的继承、异常分类和确实成立的类型关系可保留，不能一律禁止继承。
- 多个能力接口映射到同一实现时，保持原有实例身份、生命周期和释放责任；在唯一组合根注册，不为每个接口创建不同的有状态实例。简单 DataAccessor、纯算法组件等没有抽象价值时继续使用具体类。
- 使用满足实际访问需求的最小可见性。跨程序集合同可为 `public`，仅内部使用的实现、Mapper 和辅助类型应为 `internal` 或 `private`。不能为了测试方便扩大产品 API；按需使用内部可见性授权。调整 WPF 类型时必须验证 XAML 创建、绑定和资源引用，不机械收窄。

## 内聚性与依赖选型

代码按业务职责保持高内聚、低耦合；同一能力的相关操作应在对应业务目录内聚组织，不得为了分层形式散落到互不相干的命名空间。统一职责归属不等于把所有操作放进一个类或一个大接口，内部按职责组合，外部按能力隔离。封装可以统一入口或隔离第三方 API，但内部必须复用成熟实现，不能复制已有库的核心功能。

新增能力按以下顺序选型：

1. 优先使用 .NET BCL、`Microsoft.Extensions.*` 和仓库已有依赖。
2. 仍有缺口时，选择刚好覆盖实际需求、主流、活跃维护、许可证兼容的轻量库。
3. 不为少量功能引入覆盖范围远超需求的重型框架，也不并存功能重叠的多个库。
4. 仅在成熟方案确实不提供所需能力时实现最小缺口，不自行重写其已经提供的部分。

每个新包必须能说明其必要性，并检查维护状态、离线可用性、包体积、传递依赖及 .NET 10/Windows 兼容性；无法证明必要性时不引入。

## 数据与资源

程序启动时创建根目录运行数据目录：`CONFIG/`、`DATA/`、`LOGS/`、`BACKUPS/`，必要时创建 `CACHE/`。这些目录及其中的用户数据不能被发布更新覆盖。不可变词库和 TTS/OCR 模型放在 `Resources/`。发布采用自包含 `win-x64` 全量 ZIP 时，必须保留上述用户数据目录，不能把配置或用户数据打包成会覆盖现有内容的更新文件。

## 开发环境与命令

环境以 Windows 11、稳定版 Visual Studio 2026 和 .NET 10 SDK 为最低基线；`global.json` 允许向前选择能构建本项目的较新稳定 SDK，不锁定某个补丁版本。常用验证命令：

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.sln --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 2108 --max-threads 4
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj
```

`Dictation.Tests` 使用 xUnit v3 与 Microsoft.Testing.Platform，测试命名采用 `行为_场景_预期`。聚焦测试用 `--filter-class` 或 `--filter-method`，并指定合理的 `--minimum-expected-tests`；零测试不算通过。全量数量不得少于当前基线 2108 项；有意删除失效测试时必须记录原因及数量变化，不能用跳过测试或降低门槛掩盖回归。开发不应依赖 Edge、Windows TTS、Node、Docker、SQL Server、EF 或 CUDA。

## 编码约定

C# 使用四空格缩进、Allman 大括号和块级命名空间。类型、方法、属性使用 `PascalCase`；局部变量和私有字段使用 `camelCase`；启用可空引用并显式处理 `null`。WPF 表现层不写业务逻辑或 SQL。

类、接口及其他业务类型需要简明的中文 XML 职责注释；公开方法和有实际业务意义的方法说明用途、参数含义及不直观的返回值，构造函数说明注入依赖的职责。实现合同可用 `inheritdoc` 复用准确的接口说明。核心算法和复杂流程在必要处解释处理思路、原因、边界或不变量，不逐行翻译代码，不用空泛模板凑注释；简单代码无需额外内部注释。

## Agent 规则

没有用户明确授权不得启动 GOAL。环境缺失时只列出所需环境清单并等待用户处理，不擅自安装依赖或工具。实现前先确认已批准的需求和计划。保留无关的已有改动及未跟踪文件，不得擅自清理。

涉及规则修订的批准计划先修规则、再改代码。不借原则审查扩展无关功能；超过批准范围时先说明，不自行扩大。

## 安全与发布

MCP 仅监听 `127.0.0.1`，不得绕过 Core 业务服务直接写数据库。不得提交令牌或其他凭据。发布为自包含 `win-x64` 全量 ZIP；配置和用户数据目录必须在更新时保留且不被覆盖。

## Rule Priority

规则冲突时按以下顺序：1. `AGENTS.md`；2. 与当前任务最直接相关的 `rules/*.md`。同一层级更具体的规则优先。

## Development Hub 工程执行规则

项目元数据见 `ProjectInfo.json`；任务管理与项目标签见 `rules/project-management.md`，Git/TDD/PR 见 `rules/workflow.md`，C# 风格见 `rules/code-style.md`，文档维护见 `rules/design-documents.md`，工具与 SDK 兼容见 `rules/tooling.md`，项目及数据库结构见 `rules/architecture.md`。

- 修改仓库前必须有用户授权和对应 Trello Task；Trello 是仓库修改 Task 生命周期状态的权威来源；讨论、审计和未批准设计不得自动产生开发 Task。
- Task Checklist 仅包含该任务的特有交付内容，不记录创建分支、Commit、PR 或常规验证等标准流程；每个 Task 必须有完整可执行的原生 Trello Checklist，各单元与 Execution Plan 的具体 HOW 一一映射。写入前核对已批准工作完整覆盖、完成条件明确，且只包含现行最终状态。
- Task ID 使用 `task-{ProjectName}_{10位小写十六进制}`；项目名称取自 ProjectInfo，分支、PR 与 Trello 追踪同一完整 ID。
- 开始修改前，Task 必须完整到没有聊天或模型记忆的 Agent 仅凭 Task 引用、执行计划、Checklist 和 Git 状态就能完成工作。
- Task 执行范围严格限于 Task 已写明的内容；没写就不做，没让优化就不优化，没让删除就不删除；发现无关问题只报告。
- 每个项目有独立执行槽，不同项目可并行；父子 Task 按单分支、单 PR 管理。
- 默认仅在原 Git 工作目录开发；额外 Clone 或 Worktree 需用户当次明确批准。
- 用户对话是输入来源而非直接发布的内容；Task、正式设计文档和规格只保留现行最终决定、有效约束和已核验事实，不转写聊天、背景理由、新旧方案对照或废弃设定。
- C# 手写类型的一文件一类型及嵌套限制见 `rules/code-style.md`；构造函数与程序入口显式定义，不使用 Primary Constructor 或 Top-Level Statements。
- 适用的较新稳定 SDK 只要实际兼容目标框架就可使用，不锁死精确补丁号。
- 业务代码修改遵守既有 xUnit v3 TDD 与验证规则；纯规则和文档无需制造 RED。
- GitHub PR、push、merge 不能自行发布应用或创建 Release，正式发布须用户单独授权。
- 项目 Skills 存于 `.agents/skills/`。
