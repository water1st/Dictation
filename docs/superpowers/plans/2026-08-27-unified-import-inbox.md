# 统一 Import Inbox Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans task-by-task.

**Goal:** 建立日语、英语共用的导入收件箱业务链路：任意词形解析为完整学习组，重复项静默跳过，用户按批选择后送入学习库、复习库或丢弃；手动、文本文件、拖放、启动参数以及后续 MCP 只调用同一个 Service。

**Architecture:** 不可变词库只负责解析权威词条；用户 SQLite 保存独立快照，后续更新词库不会改变已导入内容。`ImportInboxDataAccessor` 只执行 Dapper/SQLite CRUD 与事务，`ImportInboxService` 负责解析、去重结果和确认用例，WPF 只负责输入适配、选择状态与导航。单实现类使用具体类型注入，不增加 Repository 或无替换价值的接口。

**Tech Stack:** C# 14、.NET 10、WPF、Dapper、Microsoft.Data.Sqlite、Microsoft.Extensions.DependencyInjection、xUnit v3。

**Spec:** `docs/requirements/RefactorSpecification.md` 第 4 节。

## 全局约束

- 词组结构固定为 `WordGroup -> Member(POS + sense) -> meanings/forms`。日语一个 JMdict Entry 是一个成员；英语锚定义项加直接派生义项组成一组。
- 英语派生关系按无向直接边读取，只扩一层，禁止递归；锚定短语永远单独成组，普通词组也不得吸收短语成员。
- 不同义项仍是不同候选；没有 Derivations 直接边的同词元多个 SenseId 不得自动合并。
- 用户数据只保存词库 DTO 的快照，不能对 `Resources/Lexicons/*.db` 建外键或运行时写入。
- `WordGroupMembers(Language, SourceSenseId)` 全局唯一。新组只要与现有 Inbox/学习/复习组有成员重叠，就整组视为重复，不做部分合并。
- `Inbox`、`Learning`、`Review` 是同一 WordGroup 的位置状态。丢弃删除整组；以后从用户库删除也删除整组及级联数据，不存在归档。
- ImportBatch 保留来源与最终结果，供后续 MCP 查询批次状态；它不保存已删除词组的完整副本。
- 任何确认必须在一个 SQLite 事务中更新词组位置、批次项目和批次状态；非法 ID、跨批选择或已处理项目不得部分生效。
- 本计划不实现学习进度、FSRS、词集、OCR 或 MCP Host；它们后续引用稳定的 WordGroupId/WordGroupFormId。
- 测试使用临时根目录和最小词库 fixture；不得复制或提交真实数据库或用户 `Resources/`。

## 目标文件

### DataAccess

- Modify: `Dictation.DataAccess/Runtime/SchemaMigrationRunner.cs`
- Create: `Dictation.DataAccess/Importing/ImportInboxDataAccessor.cs`
- Create: `Dictation.DataAccess/Importing/ImportInboxPo.cs`
- Create: `Dictation.DataAccess/Importing/ImportInboxDataAccessException.cs`
- Modify: `Dictation.DataAccess/Lexicon/JapaneseLexiconDataAccessor.cs`
- Modify: `Dictation.DataAccess/Lexicon/EnglishLexiconDataAccessor.cs`
- Modify: `Dictation.DataAccess/DictationDataAccessServiceCollectionExtensions.cs`

### Services

- Create: `Dictation.Services/Importing/ImportInboxService.cs`
- Create: `Dictation.Services/Importing/ImportInboxDto.cs`
- Modify: `Dictation.Services/Lexicon/LexiconLookupService.cs`
- Modify: `Dictation.Services/DictationServicesServiceCollectionExtensions.cs`

### Presentation

- Modify: `Dictation.Presentation/ViewModels/LexiconLookupViewModel.cs`
- Modify: `Dictation.Presentation/LexiconLookupWindow.xaml`
- Create: `Dictation.Presentation/ViewModels/ImportInboxViewModel.cs`
- Create: `Dictation.Presentation/ImportInboxWindow.xaml`
- Create: `Dictation.Presentation/ImportInboxWindow.xaml.cs`
- Create: `Dictation.Presentation/ImportInboxWindowFactory.cs`
- Modify: `Dictation.Presentation/HomeWindow.xaml`
- Modify: `Dictation.Presentation/HomeWindow.xaml.cs`
- Modify: `Dictation.Presentation/DictationPresentationServiceCollectionExtensions.cs`
- Modify: `Dictation.Presentation/Program.cs`

### Tests

- Modify: `Dictation.Tests/Infrastructure/LexiconTestDatabases.cs`
- Modify/Create: `Dictation.Tests/DataAccess/*LexiconDataAccessorTests.cs`
- Modify: `Dictation.Tests/DataAccess/SchemaMigrationRunnerTests.cs`
- Create: `Dictation.Tests/DataAccess/ImportInboxDataAccessorTests.cs`
- Create: `Dictation.Tests/Services/ImportInboxServiceTests.cs`
- Modify: `Dictation.Tests/Presentation/LexiconLookupViewModelTests.cs`
- Create: `Dictation.Tests/Presentation/ImportInboxViewModelTests.cs`

---

## Task 1：整组解析、持久化与确认核心

### Schema v2

迁移 2 创建六张表：

- `ImportBatches`：BatchId、Language、SourceKind、Status、CreatedAtUtc、CompletedAtUtc。
- `ImportBatchItems`：ItemId、BatchId、SourceText、AnchorSourceSenseId、可空 WordGroupId、Decision；删除词组时 `WordGroupId` 置空。
- `WordGroups`：WordGroupId、Language、AnchorSourceSenseId、Placement、CreatedAtUtc。
- `WordGroupMembers`：成员义项快照及稳定 SortOrder，并以 `(Language, SourceSenseId)` 唯一。
- `WordGroupMeanings`：按成员和 MeaningIndex 保存释义。
- `WordGroupForms`：按成员和 FormIndex 保存词形、读音、标签、常用/主词形标记。

所有状态使用 CHECK 约束，外键按需要 `CASCADE` 或 `SET NULL`，并为待处理批次和组内子项建立最小索引。

### Core API

```csharp
public sealed record ImportSelectionDto(string SourceText, string SourceSenseId);

public sealed record StageImportBatchRequestDto(
    LearningLanguage Language,
    ImportSourceKind SourceKind,
    IReadOnlyList<ImportSelectionDto> Selections);

public sealed class ImportInboxService
{
    public Task<StageImportBatchResultDto> StageAsync(
        StageImportBatchRequestDto request,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<ImportBatchDto>> GetPendingBatchesAsync(
        LearningLanguage language,
        CancellationToken cancellationToken = default);

    public Task DecideAsync(
        DecideImportBatchRequestDto request,
        CancellationToken cancellationToken = default);
}
```

- [ ] 为迁移 v2 写 schema、约束、幂等、备份和回滚测试。
- [ ] 为日语按 EntryId 精确取组、英语按 SenseId 取锚点加一层直接派生写测试；覆盖反向边、无递归、短语独立、未知 ID 和取消。
- [ ] 写 DataAccessor 测试，覆盖多成员快照、同批/跨批/已入库重复、部分选择、学习/复习/丢弃、全批完成、非法选择事务回滚和数据库错误。
- [ ] 写 Service 测试，覆盖语言/来源/空选择/重复 SourceSenseId 校验、权威词库重新解析、DTO/PO 映射、缺词、取消和 DataAccess 异常。
- [ ] 建立可编译骨架；临时让英语解析递归加入二跳 `conversion`，运行聚焦测试证明“仅一层”断言失败，随后立即恢复。
- [ ] 实现最小查询与事务。SQL 全部参数化，每次操作独立连接，批次内解析顺序稳定；DataAccessor 不引用 Service DTO。
- [ ] 临时取消丢弃事务中的 WordGroup 删除，运行聚焦测试证明整组删除断言失败，随后恢复。
- [ ] 运行聚焦测试、全量测试、Debug build、`git diff --check` 和源码 `.db` 泄漏检查；提交 `导入：建立整组收件箱核心`。

---

## Task 2：WPF 手动暂存与按批确认

- [ ] 扩展查词 ViewModel 测试：无选择不能暂存；选中候选后只提交其 SourceSenseId；成功、重复、词库/数据库失败和取消状态清楚；搜索后重置旧提示。
- [ ] 在查词详情增加“加入导入收件箱”，不把学习/复习选择塞进查词页。
- [ ] `ImportInboxViewModel` 固定当前语言，按批展示；选中一个批次后列出词组，顶部全选 checkbox 控制当前批次，至少一个项目被选中才能决定。
- [ ] 覆盖全选/取消全选、切换批次、部分确认、全批确认、学习/复习/丢弃、刷新、空状态、错误与取消测试。
- [ ] 首页增加当前语言“导入收件箱”入口；窗口内不得切换语言。列表显示组头、成员数和来源，详情显示成员义项、释义与全部词形。
- [ ] 临时让全选漏掉最后一项，运行 ViewModel 测试证明失败，随后恢复。
- [ ] 运行聚焦与全量测试、Debug build、`git diff --check`；真实 WPF 冒烟确认查词暂存、重复提示、批次全选、三种决定、关闭返回首页和暗色主题可读性。
- [ ] 提交 `导入：打通WPF收件箱确认流程`。

---

## Task 3：统一文本、TXT、拖放与启动参数入口

- [ ] 在 Service 增加批量源词解析：Trim、Unicode NFC、去空行、批内去重；只接受源语言精确词形，不用前缀或释义反查。一个输入命中多个义项时全部进入同一批次供用户确认，未命中词原样返回。
- [ ] 批量查询按 SQLite 参数上限分块，800 个输入不逐词创建 800 次连接；日语与英语均保持输入顺序及词库稳定顺序。
- [ ] WPF 增加多行粘贴、TXT 文件选择和 `.txt` 拖放；Presentation 只读取 UTF-8/带 BOM 文本并把词行交给同一 Service。
- [ ] `Program.Main(args)` 在主实例初始化后把 `.txt` 路径或直接源词参数交给同一入口并打开 Inbox；第二实例仍只激活首实例，不转交参数。
- [ ] 覆盖空文件、重复行、混合换行、无匹配、错误语言文本、超过上限、读取失败、取消和 800 词批量 fixture；不得引入新的解析库。
- [ ] 运行全量测试、Debug build、`git diff --check` 和真实 WPF TXT/拖放/启动参数冒烟；提交 `导入：统一文本与文件导入入口`。

## 验收命令

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
rg --files -g "*.db" -g "!**/bin/**" -g "!**/obj/**"
```

每个 Task 提交前还要确认当前分支为 `codex/dictation-refactor`、暂存区只包含该 Task 文件，且没有遗留 `Dictation.Presentation`、`Dictation.TtsHost` 进程。
