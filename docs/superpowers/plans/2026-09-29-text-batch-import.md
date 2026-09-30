# 文本批量导入 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让用户在单窗口中粘贴或选择 UTF-8 TXT 词表，逐项确认日文／英文来源义项，再把整组词形批量暂存到统一导入收件箱。

**Architecture:** Core 复用现有 `ILexiconSearch` 与 `IImportStaging`，负责文本解析、精确词形候选和暂存选择；Presentation 负责文件读取、候选选择和页面导航。模糊或多义项不自动猜测，不新增数据库、仓储或第二套导入通道。

**Tech Stack:** .NET 10、WPF、Dapper/SQLite、xUnit v3；无新增 NuGet 包。

**Spec:** `docs/requirements/RefactorSpecification.md` 第 2、4、6、7、11 节；用户 2026-09-29 要求先提供可用词库、OCR 后置。

## Global Constraints

- 固定语言为日语或英语；只按源语言正向查词，绝不把英文释义当中文或执行中文反查。
- 来源文本每行一词，空行和 `#` 注释忽略；保留首次出现顺序，按语言规范化后去重。TXT 使用 UTF-8，可有 BOM；不解析旧版“中文_外语”格式以免误导义项。
- 候选必须有与输入完全相同的词头或词形；零候选保留为未匹配，多候选必须由用户明确选一个，单候选可默认选中。不能因为前缀命中而导入另一词。
- 暂存仅选中且仍属于本批候选的稳定 `SourceSenseId`，调用现有 `IImportStaging.StageAsync`，来源为 `Text`；学习／复习／丢弃仍只能在统一 Inbox 确认。
- 单窗口 `ContentControl + UserControl`；Presentation 不写 SQL 或业务判定。每个主要类型单独文件，注释和 DI 遵循 `AGENTS.md`。
- 测试编译必须成功；通过临时生产 mutation 证明行为 RED，恢复后 GREEN。构建、全量测试和真实离线词库冒烟通过才提交完整垂直切片。

## Review Focus

1. `go` 等多义词不能静默选第一义项；测试要求用户选择前不进入 Inbox。
2. `went` 等变形查询必须落到包含该形的完整词组；测试检查稳定锚点与整组快照。
3. 前缀 `cat` 不得误把 `catalog` 当作精确词；测试检查候选过滤。
4. 重复行、空行、注释和 Unicode 正规化不能产生重复 Inbox 项；测试检查顺序及去重计数。
5. 缺失词库、取消、无匹配、TXT 编码错误不能悄悄导入部分错误词；测试检查明确错误或未匹配状态。

---

### Task 1: Core 文本准备与精确候选

**Files:** 新增 `Dictation.Core/Importing/TextImportLineDto.cs`、`TextImportPreparationDto.cs`、`TextImportPreparationService.cs`、`ITextImportPreparation.cs`；修改 `Dictation.Core/DictationCoreServiceCollectionExtensions.cs`；新增 `Dictation.Tests/Core/TextImportPreparationServiceTests.cs`。候选直接复用现有 `LexiconLookupResultDto`，不复制一套相同字段和 Mapper。

**Interfaces:** Produces `ITextImportPreparation.PrepareAsync(LearningLanguage language, string text, CancellationToken cancellationToken)` → `TextImportPreparationDto`。每个非重复来源行返回原文、行号、精确候选列表及默认选择（仅单候选）；Service 只读词库，不暂存。

- [x] **Step 1：写测试。** 覆盖日语假名／汉字、英语 `went`、多义项、前缀误命中、重复／注释／空行、空文本、非法语言、取消、缺失词库异常。
- [x] **Step 2：有效 RED。** 建立可编译最小骨架并冻结测试；在候选精确过滤或去重处临时制造行为错误。运行 `dotnet build Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore` 成功，再用 `dotnet test ... --filter-class '*TextImportPreparationServiceTests' --minimum-expected-tests 8` 得到目标行为断言失败；恢复 mutation。
- [x] **Step 3：GREEN。** 使用现有 `ILexiconSearch.SearchAsync`，以 NFC／英语大小写规范化比较词头及 `Forms.Surface`；只保留精确候选，稳定排序并去重。运行聚焦与全量测试、解决方案构建。
- [x] **Step 4：中文提交。** 只提交本任务的 Core、DTO、DI 与测试，记下 RED→GREEN 证据。

### Task 2: 单窗口候选选择与整组暂存

**Files:** 新增 `Dictation.Presentation/ViewModels/TextImportViewModel.cs`、`TextImportLineViewModel.cs`、`Dictation.Presentation/Views/TextImportPage.xaml`、`TextImportPage.xaml.cs`；修改 `IHomeNavigation.cs`、`NavigationService.cs`、`DictationPresentationServiceCollectionExtensions.cs`、`HomeViewModel.cs`、`HomePage.xaml`、`App.xaml`；新增 Presentation 单元与真实 SQLite 导航测试。

**Interfaces:** Consumes Task 1 的 `ITextImportPreparation`；调用已有 `IImportStaging.StageAsync(StageImportBatchRequestDto)`；produces `TextImportViewModel.LoadTextAsync(string text, CancellationToken cancellationToken)` 与 `IHomeNavigation.OpenTextImportAsync(LearningLanguage language, string? initialFilePath = null, CancellationToken cancellationToken = default)`。页面固定首页选择的语言，输入多行文本；每行展示匹配／未匹配及候选中文、词性、词形，只有明确选择才能加入 Inbox。

- [x] **Step 1：先写 VM 和导航测试。** 断言多义未选不能暂存、选中后只暂存该义项、无匹配保留在页面、重复项被忽略、取消不写库、日英隔离及单窗口返回栈。
- [x] **Step 2：有效 RED。** 测试编译成功后，对生产暂存选择过滤做临时 mutation，聚焦测试须执行并因行为断言失败；恢复 mutation。
- [x] **Step 3：GREEN 与页面。** 最小 ViewModel、导航、XAML；文件读取和 SQL 不放 ViewModel。真实 DI＋SQLite 测试使用词库 fixture，核对 `Text` 批次与整组快照，且未替用户决定去学习或复习。
- [x] **Step 4：验证并提交。** 全量测试、构建、WPF 单窗口冒烟、真实日中／英中词库各一条导入；中文提交。

### Task 3: TXT 文件、拖放与启动参数复用同一页面

**Files:** 新增 `Dictation.Presentation/Importing/Utf8TextFileReader.cs`；修改 `TextImportPage.xaml`、`TextImportPage.xaml.cs`、`TextImportViewModel.cs`、`NavigationService.cs`、`Program.cs`；新增文件读取、拖放和启动参数测试。

**Interfaces:** `Utf8TextFileReader.ReadAsync(string path, CancellationToken cancellationToken)` 使用严格 UTF-8 解码；页面文件选择／拖放及 `Program` 首实例启动参数最终都调用 `TextImportViewModel.LoadTextAsync(string text, CancellationToken cancellationToken)`。文件来源只负责获取文本，不能另写数据库，也不能绕过候选确认和 Inbox。

- [x] **Step 1：写测试。** 覆盖 UTF-8 BOM／无 BOM、空文件、无效 UTF-8、拖入非 TXT、文件不存在、启动参数、第二进程不转交参数、取消和重复加载。
- [x] **Step 2：有效 RED。** 编译成功后在生产文件读取／路由处临时破坏一种行为，确认聚焦测试实际失败再恢复。
- [x] **Step 3：实现与验证。** `OpenFileDialog`、单窗口拖放和首进程启动参数都只打开同一页面；异常显示在页面，已有 Inbox 与进度不变。全量测试、构建、真实 TXT 冒烟。
- [x] **Step 4：中文提交。** 更新用户说明，保留 `CONFIG/`、`DATA/` 等运行目录；不把原始词库塞入源码或 ZIP。

## 完成记录

Core `bf05e68`、页面与文件入口 `c2bcf37`；独立审查发现的精确候选截断和 UI 阻塞／取消生命周期问题已统一修正。全部 1494 项测试通过，构建零警告／错误；真实正式日语「こう」86 个候选，最后义项可整组暂存。完整证据与通用取舍见 `docs/verification/2026-09-30-text-batch-import.md`。此计划完成不代表复习、五十音、MCP、发布更新或 OCR 的整项目标完成。
