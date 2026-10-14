# Core 与 Services 合并实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 删除独立的 `Dictation.Services` 项目，将全部业务服务、DTO 和 Provider 合同迁入唯一的 `Dictation.Core` 业务层，同时移除已被 WPF 与新 TTS 链路取代的旧 Core/WinForms 代码。

**Architecture:** 最终物理依赖固定为 `Presentation -> Core -> DataAccess`，`TtsHost -> Core`；DataAccess 不反向引用 Core。迁移是一次原子结构重构：先用可编译但失败的架构测试锁定目标，再同时移动文件、改命名空间、合并 DI 和更新项目引用，避免提交不可构建的中间状态。

**Tech Stack:** .NET 10、C#、WPF、Microsoft.Extensions.DependencyInjection、xUnit v3、PowerShell、Git

**Spec:** `docs/superpowers/specs/2026-09-06-core-services-consolidation-design.md`

## Global Constraints

- 只在 `codex/dictation-refactor` 分支和 `D:\Source\Repos\Dictation-refactor` 工作树操作，不推送或合并。
- 不改变 SQLite schema、用户数据、TTS 命名管道协议、学习业务行为或 UI 行为。
- `Dictation.Services` 不保留转发程序集、类型别名、兼容命名空间或第二个 DI 入口。
- 删除仅由旧 WinForms 链路使用的 Edge/System TTS、绘图、旧听写和多窗口代码，不保留无用兼容实现。
- 所有结构测试必须先在测试项目成功编译后产生断言失败；编译失败或零测试不算 RED。
- 迁移后全量测试不得少于基线 1367 项，加上本计划 5 项架构测试后不得少于 1372 项。

---

### Task 1：用架构测试锁定唯一 Core 业务层

**Files:**
- Create: `Dictation.Tests/Architecture/CoreLayerArchitectureTests.cs`
- Modify: `Dictation.Tests/Presentation/SingleWindowArchitectureTests.cs`

**Interfaces:**
- Consumes: 实际编译的 Core、Presentation、TtsHost、DataAccess 程序集和真实 DI 容器。
- Produces: 五项程序集／DI 约束，以及 Presentation 中不存在 WinForms 类型的约束。

- [x] **Step 1：记录迁移前测试基线**

`dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore --minimum-expected-tests 1367` 实际通过 1367/1367。

- [x] **Step 2：新增可编译的架构测试**

采用运行时程序集元数据和真实 DI 解析，替代原计划仅扫描源码文本的测试，以验证真实依赖和业务服务可用性：

1. `CoreAssembly_OwnsBusinessServicesAndSharedContracts`：业务服务及共享 DTO／Provider 合同实际归属于 Core，不存在 Services 命名空间。
2. `CompiledDependencies_UseCoreWithoutReversingTheThreeLayers`：UI、TtsHost 引用 Core；Core 引用 DataAccess；DataAccess 不反向依赖。
3. `CoreAssembly_RunsOnNet10WithoutLegacyDesktopDependencies`：实际目标为 .NET 10，程序集不引用旧桌面／TTS 依赖。
4. `AddDictationCore_ResolvesBusinessServicesOnceAndSupportsTheHomeViewModel`：每项业务服务只注册一次且生命周期正确；真实解析依赖并初始化首页，唯一业务入口为 AddDictationCore。
5. `AddDictationCore_RejectsNullCollection`：空容器输入抛出 ArgumentNullException。

已有单窗口测试改为反射检查实际 Presentation 程序集不存在 Form 子类和 WindowFactory，避免直接引用待删除类型。其余业务测试仅允许必要的命名空间／注册入口改名和无用 using 清理，不改断言。

- [x] **Step 3：证明测试是行为 RED，而不是编译 RED**

实际运行：

```powershell
dotnet build Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class Dictation.Tests.Architecture.CoreLayerArchitectureTests --minimum-expected-tests 5
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-method Dictation.Tests.Presentation.SingleWindowArchitectureTests.PresentationAssemblyContainsNoLegacyWinFormsTypes --minimum-expected-tests 1
```

迁移前 build 0 errors；架构类实际执行 5 项，4 项因 Core 类型归属／程序集依赖／目标框架／服务注册断言失败；单窗口测试因残留 Form 子类失败。空容器异常测试正常通过。没有把编译失败或零测试算作 RED。

迁移后另做一次生产 DI mutation：临时将 SettingsService 的 Singleton 改为 Transient。先 build 0 errors，随后相同 5 项测试中 1 项因 `Expected Singleton / Actual Transient` 断言失败。仅恢复生产注册后全绿；冻结架构测试和 DI 源码的 SHA-256 在该次 mutation 前后完全一致。

---

### Task 2：原子合并项目、命名空间和 DI

**Files:**
- Move: `Dictation.Services/Configuration/` -> `Dictation.Core/Configuration/`
- Move: `Dictation.Services/Importing/` -> `Dictation.Core/Importing/`
- Move: `Dictation.Services/Learning/` -> `Dictation.Core/Learning/`
- Move: `Dictation.Services/Lexicon/` -> `Dictation.Core/Lexicon/`
- Move: `Dictation.Services/SingleInstance/` -> `Dictation.Core/SingleInstance/`
- Move: `Dictation.Services/Startup/` -> `Dictation.Core/Startup/`
- Move: `Dictation.Services/Tts/` -> `Dictation.Core/Tts/`
- Move: `Dictation.Tests/Services/` -> `Dictation.Tests/Core/`
- Modify: `Dictation.Core/Dictation.Core.csproj`
- Modify: `Dictation.Core/DictationCoreServiceCollectionExtensions.cs`
- Modify: `Dictation.Presentation/Dictation.Presentation.csproj`
- Modify: `Dictation.TtsHost/Dictation.TtsHost.csproj`
- Modify: `Dictation.Tests/Dictation.Tests.csproj`
- Modify: `Dictation.sln`
- Modify: production and test `.cs` files importing `Dictation.Services.*`
- Delete: `Dictation.Services/DictationServicesServiceCollectionExtensions.cs`
- Delete: `Dictation.Services/Dictation.Services.csproj`
- Delete: `Dictation.Core/DictationManager.cs`
- Delete: `Dictation.Core/EdgeTTSPlayer.cs`
- Delete: `Dictation.Core/ILanguageSettableTTSPlayer.cs`
- Delete: `Dictation.Core/ITTSPlayer.cs`
- Delete: `Dictation.Core/LanguageRecognizer.cs`
- Delete: `Dictation.Core/SystemTTSPlayer.cs`
- Delete: `Dictation.Core/TTSFactory.cs`
- Delete: `Dictation.Core/TTSOption.cs`
- Delete: `Dictation.Core/TTSProxy.cs`
- Delete: `Dictation.Core/WordCollection .cs`
- Delete: `Dictation.Core/WordDrawing.cs`
- Delete: `Dictation.Core/WordDrawingCollection.cs`
- Delete: `Dictation.Presentation/WindowFactory.cs`
- Delete: `Dictation.Presentation/MainWindow.cs`, `MainWindow.Designer.cs`, `MainWindow.resx`
- Delete: `Dictation.Presentation/DictationWindow.cs`, `DictationWindow.Designer.cs`, `DictationWindow.resx`
- Delete: `Dictation.Presentation/ReviewWindow.cs`, `ReviewWindow.Designer.cs`, `ReviewWindow.resx`
- Delete: `Dictation.Presentation/ResultWindow.cs`, `ResultWindow.Designer.cs`, `ResultWindow.resx`
- Delete: `Dictation.Presentation/Properties/Resources.Designer.cs`, `Properties/Resources.resx`

**Interfaces:**
- Consumes: 原 `Dictation.Services.*` 类型及 `AddDictationServices()` 注册行为。
- Produces: 等价的 `Dictation.Core.*` 类型以及唯一的 `AddDictationCore(IServiceCollection)` 注册入口。

- [x] **Step 1：移动业务模块与测试目录**

Run:

```powershell
git mv Dictation.Services/Configuration Dictation.Core/Configuration
git mv Dictation.Services/Importing Dictation.Core/Importing
git mv Dictation.Services/Learning Dictation.Core/Learning
git mv Dictation.Services/Lexicon Dictation.Core/Lexicon
git mv Dictation.Services/SingleInstance Dictation.Core/SingleInstance
git mv Dictation.Services/Startup Dictation.Core/Startup
git mv Dictation.Services/Tts Dictation.Core/Tts
git mv Dictation.Tests/Services Dictation.Tests/Core
```

实际使用逐项校验源／目标绝对路径后的 PowerShell Move-Item 完成上述迁移，Git 在暂存后识别重命名；没有保留两套源码。

- [x] **Step 2：更新 Core 项目清单**

将 `Dictation.Core.csproj` 改为 `net10.0`、启用 `ImplicitUsings`，只合并仍需要的 Services 依赖：

```xml
<TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>

<ProjectReference Include="..\Dictation.DataAccess\Dictation.DataAccess.csproj" />
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.10" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.10" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.10" />
```

不要添加 EF、Repository、兼容程序集或新的第三方库。

- [x] **Step 3：机械迁移命名空间并移动测试命名空间**

对以下明确根目录中、排除 `bin/obj` 的 `.cs` 文件执行一次机械替换：

```text
Dictation.Services.       -> Dictation.Core.
namespace Dictation.Services -> namespace Dictation.Core
Dictation.Tests.Services -> Dictation.Tests.Core
```

覆盖 `Dictation.Core`、`Dictation.Presentation`、`Dictation.TtsHost`、`Dictation.Tests`；不得修改历史文档或 DataAccess SQL。替换后运行：

```powershell
rg -n "namespace Dictation\.Services|using Dictation\.Services|Dictation\.Tests\.Services" Dictation.Core Dictation.Presentation Dictation.TtsHost Dictation.Tests -g '*.cs' -g '!**/bin/**' -g '!**/obj/**'
```

Expected: 无输出。

- [x] **Step 4：合并为唯一 DI 入口**

在 `DictationCoreServiceCollectionExtensions.cs` 中删除旧 WinForms/Core 注册，只保留以下原 Services 注册；删除 `DictationServicesServiceCollectionExtensions.cs`：

```csharp
ArgumentNullException.ThrowIfNull(services);
services.AddSingleton<SettingsService>();
services.AddSingleton<StartupInitializer>();
services.AddSingleton<SingleInstanceCoordinator>();
services.AddSingleton<TtsService>();
services.AddSingleton<LexiconLookupService>();
services.AddSingleton<ImportInboxService>();
services.AddSingleton(TimeProvider.System);
services.AddSingleton(Random.Shared);
services.AddSingleton<LearningPlanService>();
services.AddSingleton<LearningSessionService>();
services.AddSingleton<AnswerEvaluationService>();
```

将 Presentation 和测试中的 `.AddDictationServices().AddDictationDataAccess().AddDictationCore()` 改为只调用一次业务入口和一次数据访问入口：`.AddDictationDataAccess().AddDictationCore()`。

- [x] **Step 5：更新项目引用并删除 Services 项目**

具体变更：

- Presentation 删除 `Dictation.Services.csproj` 引用，保留对 Core 和 DataAccess 的直接引用。
- TtsHost 将 Services 引用替换为 Core。
- Tests 删除 Services 引用并新增对 Core 的直接引用。
- Presentation 删除 `UseWindowsForms`、旧强类型资源的 `Compile`/`EmbeddedResource` 项，并删除上方列出的旧 WinForms 文件。
- Core 删除上方列出的旧听写、绘图、Edge/System TTS 文件及相应包引用。
- 执行 `dotnet sln Dictation.sln remove Dictation.Services/Dictation.Services.csproj`。
- 删除剩余的 `Dictation.Services.csproj` 和空的 `Dictation.Services` 目录。

Run:

```powershell
dotnet restore Dictation.sln
dotnet build Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
```

Expected: restore 成功；测试项目 build 0 errors。若出现循环引用，检查并保持 DataAccess 不引用 Core，禁止用抽取第四个共享项目规避。

- [x] **Step 6：让架构测试由红转绿**

Run:

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-class Dictation.Tests.Architecture.CoreLayerArchitectureTests --minimum-expected-tests 5
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --filter-method Dictation.Tests.Presentation.SingleWindowArchitectureTests.PresentationAssemblyContainsNoLegacyWinFormsTypes --minimum-expected-tests 1
```

Expected: 5/5 和 1/1 通过；只能修正迁移实现，不能放宽断言或排除生产目录。

- [x] **Step 7：验证行为未变**

Run:

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1372
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
```

Expected: 至少 1372 项全部通过；解决方案 0 errors；差异格式检查无错误。

- [x] **Step 8：静态核对依赖与遗留项**

Run:

```powershell
dotnet sln Dictation.sln list
rg -n "Dictation\.Services|AddDictationServices" Dictation.sln Dictation.Core Dictation.DataAccess Dictation.Presentation Dictation.TtsHost Dictation.Tests/Dictation.Tests.csproj -g '!**/bin/**' -g '!**/obj/**'
rg -n "Dictation\.Core" Dictation.DataAccess/Dictation.DataAccess.csproj
rg -n "UseWindowsForms|Edge_tts_sharp|System\.Speech|WindowFactory|class (MainWindow|DictationWindow|ReviewWindow|ResultWindow)" Dictation.Core Dictation.Presentation -g '!**/bin/**' -g '!**/obj/**'
rg --files -g '*.db' -g '!**/bin/**' -g '!**/obj/**'
```

Expected: 解决方案无 Services 项目；三次 `rg` 无输出；源码树无 `.db`。架构测试中作为禁止依赖断言的旧名称字符串有意保留，不计作生产依赖。`HomeWindow`、WPF `UserControl` 与新 `Dictation.TtsHost` 仍存在。

- [x] **Step 9：提交原子架构纠偏**

```powershell
git add Dictation.sln Dictation.Core Dictation.Presentation Dictation.TtsHost Dictation.Tests Dictation.Services
git commit -m "架构：将业务服务并入核心层"
```

已提交 `b65d75c`：项目合并、遗留代码清理、命名空间、DI、项目引用和架构测试；不包含 Task 5 学习 UI。独立只读审查未发现需要修复的问题。

---

### Task 3：同步权威架构文档并恢复学习计划入口

执行记录还需更新本计划与对应设计中的验证说明，历史已完成计划不改写。

**Files:**
- Modify: `AGENTS.md`
- Modify: `docs/requirements/RefactorSpecification.md`
- Modify: `docs/superpowers/plans/2026-09-04-learning-mode.md`

**Interfaces:**
- Consumes: Task 2 已落地的 Presentation/Core/DataAccess 三层路径。
- Produces: 后续 Task 5 及新 Agent 使用的唯一正确路径和命名空间说明。

- [x] **Step 1：更新权威架构描述**

将当前目标结构和架构边界统一为：

```text
Dictation.Presentation：WPF、ViewModel、绑定和输入输出适配
Dictation.Core：业务编排、DTO/Mapper、Provider 合同、启动与设置服务
Dictation.DataAccess：Dapper/SQLite CRUD 与 SQL
```

明确 UI 与未来 MCP 共用 Core 的业务服务；只有可替换 Provider 使用接口。把当前需求规格中的 `Dictation.Services.Configuration` 改为 `Dictation.Core.Configuration`。

- [x] **Step 2：修正仍在执行的学习模式计划路径**

在 `2026-09-04-learning-mode.md` 中将尚未完成任务及文件清单里的：

```text
Dictation.Services/ -> Dictation.Core/
Dictation.Services.* -> Dictation.Core.*
Dictation.Tests/Services/ -> Dictation.Tests/Core/
```

已完成的其他历史计划保留作为当时执行记录，不批量改写。

- [x] **Step 3：验证文档与代码一致**

Run:

```powershell
rg -n "目标结构包括.*Dictation\.Services|Presentation.*Services.*DataAccess|Dictation\.Services/|Dictation\.Services\." AGENTS.md docs/requirements/RefactorSpecification.md docs/superpowers/plans/2026-09-04-learning-mode.md
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-build --minimum-expected-tests 1372
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
```

Expected: `rg` 无陈旧架构命中；至少 1372 项全部通过；解决方案 0 errors；差异格式检查无错误。

- [x] **Step 4：提交文档同步**

```powershell
git add AGENTS.md docs/requirements/RefactorSpecification.md docs/superpowers/plans/2026-09-04-learning-mode.md docs/superpowers/plans/2026-09-06-core-services-consolidation.md docs/superpowers/specs/2026-09-06-core-services-consolidation-design.md
git commit -m "文档：同步核心层三层架构"
```

- [x] **Step 5：回到学习模式 Task 5**

续接入口已切回 `2026-09-04-learning-mode.md` 的 Task 5“完成 WPF 学习会话、浏览与纯内存音频链路”，该任务尚未实现；本计划文档提交并确认工作树干净后开始，使用 `Dictation.Core.*`。

## 本轮验证记录（2026-09-06）

- 迁移前基线：1367/1367；迁移后 Core 架构 5/5、单窗口架构 7/7、全量 1372/1372，均无跳过。
- Debug 解决方案构建：0 errors；独立临时目录 publish：0 errors、0 warnings。
- 最终对照 15016ec 的 81 个既有业务与测试文件，在命名空间／注册入口改名及无用 using 标准化后无非机械差异。新增架构测试和有意加强的单窗口测试单独验证，不混入此统计；既有业务测试断言未改。
- DI 生命周期 mutation 的源码 SHA-256 前后均为 `DB38E647967834A88B3FA80B5AE7EF7AE8EFB60BAE1E7632CC6CD443B5366CE4`；冻结架构测试前后均为 `A944B4CEBEABF31546B29E64A92E754B453040C7337072785B1E9B522EC88BCD`。
- 隔离发布启动冒烟：真实首页就绪、首次创建 DATA/UserData.db、唯一 TtsHost 子进程启动；关闭首页后 WPF 返回 0，子进程退出。首次用 WaitForInputIdle 取样过早；改为等待真实主窗口和数据库就绪后通过，未改生产实现。
- Services 目录及其旧 bin/obj 已清除；无反向项目引用；发布依赖清单无旧 Services／Edge/System TTS；源码树无数据库文件。用户运行数据、下载资源未修改。
- 下一业务任务仍为学习模式 Task 5；本次合并不实现新学习 UI，也不标记整个重构 GOAL 完成。
