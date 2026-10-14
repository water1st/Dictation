# 日英语离线词库运行时查询 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将已经生成的日语、英语只读 SQLite 词库接入运行时，让用户从当前首页语言进入 WPF 查词页，以源语言词形正向检索、区分义项并浏览该义项所属词元的完整词形。

**Architecture:** `ApplicationPaths` 固定两个不可变词库路径；DataAccess 用两个具体 DataAccessor 和只读 SQLite 连接完成查询；单一 `LexiconLookupService` 负责输入规范化、语言分派和 PO→DTO 映射；Presentation 用模态 WPF 窗口固定当前语言，不允许在查词过程中切换。该切片只建立可运行的只读查询链路，不提前创建 Import Inbox 或用户词库表。

**Tech Stack:** C# 14、.NET 10、WPF、Dapper 2.1.79、Microsoft.Data.Sqlite 10.0.10、Microsoft.Extensions.DependencyInjection、xUnit v3。

**Spec:** `docs/requirements/RefactorSpecification.md`

## Global Constraints

- 运行时路径固定为 `Resources/Lexicons/JapaneseLexicon.db` 与 `Resources/Lexicons/EnglishLexicon.db`；查询代码不得创建、迁移、覆盖或写入这两个数据库。
- 只按 `Forms.Surface` 做完全匹配和前缀匹配，禁止查询 `Glosses.Text`、`Definitions.Text` 或 `Examples.Text` 作为入口，因此不提供中文或释义反向查词。
- 每次查询创建并释放 `Mode=ReadOnly;Pooling=False` 的 SQLite 连接；资源缺失、schema 不为 `1` 或数据库损坏时返回明确中文错误，但不阻止 WPF 主程序启动。
- DataAccessor 和 `LexiconLookupService` 都只有一个实现，使用具体类注入；禁止新增 Repository、泛型仓储、无扩展价值接口、EF、第二个 ORM 或在线 API。
- 日语 `EntryId`、英语 `SenseId` 是独立义项标识；英语相同 `LexemeId` 的多个 SenseId 共享词形，但不能合并成一个结果。短语只显示数据库中已有的 Base 形式。
- 本切片不递归派生关系，不写用户数据库，不确认学习库/复习库，不接入 TXT、拖放、启动参数或 MCP；这些属于后续统一 Import Inbox 切片。
- 测试只使用临时根目录和最小 SQLite fixture，不复制或提交真实 `.db`、JMdict、WordNet YAML 或用户 `Resources/`。
- 纯文档和 XAML 编译验证不制造无意义测试；业务逻辑仍须先证明测试能失败，再修正为全绿，临时错误不得提交。

---

## File Structure

### Runtime paths and DataAccess

- Modify: `Dictation.DataAccess/Runtime/ApplicationPaths.cs` — 只计算不可变 Resources 与两个词库路径，不创建资源目录。
- Create: `Dictation.DataAccess/Lexicon/LexiconSqliteConnectionFactory.cs` — 为两个固定路径创建未打开的只读连接并校验文件存在。
- Create: `Dictation.DataAccess/Lexicon/LexiconDataAccessException.cs` — 统一包装缺失资源、schema 和 SQLite 查询错误。
- Create: `Dictation.DataAccess/Lexicon/JapaneseLexiconLookupPo.cs` — 日语查询持久化读模型及词形 PO。
- Create: `Dictation.DataAccess/Lexicon/EnglishLexiconLookupPo.cs` — 英语查询持久化读模型及词形 PO。
- Create: `Dictation.DataAccess/Lexicon/JapaneseLexiconDataAccessor.cs` — 只查询日语 Forms→Entries/Glosses/PartsOfSpeech。
- Create: `Dictation.DataAccess/Lexicon/EnglishLexiconDataAccessor.cs` — 只查询英语 Forms→Lexemes/Senses/Definitions。
- Modify: `Dictation.DataAccess/DictationDataAccessServiceCollectionExtensions.cs` — 注册上述具体类。

### Services

- Create: `Dictation.Services/Lexicon/LexiconSearchRequestDto.cs` — 查询语言、原始文本与结果上限。
- Create: `Dictation.Services/Lexicon/LexiconLookupResultDto.cs` — UI 所需义项、词元、释义、属性与完整词形 DTO。
- Create: `Dictation.Services/Lexicon/LexiconLookupService.cs` — 规范化、语言分派、中文标签和 PO→DTO Mapper。
- Modify: `Dictation.Services/DictationServicesServiceCollectionExtensions.cs` — 注册具体 Service。

### Presentation

- Create: `Dictation.Presentation/ViewModels/LexiconLookupViewModel.cs` — 固定语言、查询状态、结果集合、选择项和错误状态。
- Create: `Dictation.Presentation/LexiconLookupWindow.xaml`
- Create: `Dictation.Presentation/LexiconLookupWindow.xaml.cs`
- Create: `Dictation.Presentation/LexiconLookupWindowFactory.cs` — 从 DI 获取 transient 窗口，设置 Owner/语言并模态显示。
- Modify: `Dictation.Presentation/HomeWindow.xaml` — 为当前语言增加“查词”入口。
- Modify: `Dictation.Presentation/HomeWindow.xaml.cs` — 仅做导航适配，不调用 SQL/Service。
- Modify: `Dictation.Presentation/DictationPresentationServiceCollectionExtensions.cs` — 注册 transient 窗口/ViewModel 与工厂。

### Tests and docs

- Create: `Dictation.Tests/Infrastructure/LexiconTestDatabases.cs` — 在临时 Resources 布局写入两套最小 schema/fixture。
- Modify: `Dictation.Tests/DataAccess/ApplicationPathsTests.cs`
- Create: `Dictation.Tests/DataAccess/JapaneseLexiconDataAccessorTests.cs`
- Create: `Dictation.Tests/DataAccess/EnglishLexiconDataAccessorTests.cs`
- Create: `Dictation.Tests/Services/LexiconLookupServiceTests.cs`
- Create: `Dictation.Tests/Presentation/LexiconLookupViewModelTests.cs`
- Modify: `docs/resources/ThirdPartyResources.md` — 记录固定运行时词库布局和只读约束。

---

### Task 1: 实现日英语只读查询核心

**Files:**
- Modify/Create: 上述 Runtime paths、DataAccess、Services 和对应测试文件。

**Interfaces:**

```csharp
public sealed class LexiconSqliteConnectionFactory
{
    public SqliteConnection CreateJapaneseConnection();
    public SqliteConnection CreateEnglishConnection();
}

public sealed class JapaneseLexiconDataAccessor
{
    public Task<IReadOnlyList<JapaneseLexiconLookupPo>> SearchAsync(
        string normalizedQuery, int limit, CancellationToken cancellationToken = default);
}

public sealed class EnglishLexiconDataAccessor
{
    public Task<IReadOnlyList<EnglishLexiconLookupPo>> SearchAsync(
        string normalizedQuery, int limit, CancellationToken cancellationToken = default);
}

public sealed record LexiconSearchRequestDto(
    LearningLanguage Language,
    string Query,
    int Limit = 20);

public sealed class LexiconLookupService
{
    public Task<IReadOnlyList<LexiconLookupResultDto>> SearchAsync(
        LexiconSearchRequestDto request,
        CancellationToken cancellationToken = default);
}
```

`LexiconLookupResultDto` 必须包含 `Language`、`SourceSenseId`、`SourceLexemeId`、`Headword`、可空 `Reading`、`PartOfSpeechLabel`、`AttributeLabel`、`IsPhrase`、有序 `Meanings` 和有序 `Forms`。每个 form DTO 包含 `Surface`、可空 `Reading`、中文 `FormLabel`、`DetailLabel`、`IsCommon`、`IsPrimary`；原始数据库字符串只保留在 PO 内。

- [ ] **Step 1: 写路径与最小 fixture 测试**

在 `ApplicationPathsTests` 字面断言：

```csharp
Assert.Equal(Path.Combine(root, "Resources"), paths.Resources);
Assert.Equal(Path.Combine(root, "Resources", "Lexicons"), paths.Lexicons);
Assert.Equal(Path.Combine(paths.Lexicons, "JapaneseLexicon.db"), paths.JapaneseLexiconDatabase);
Assert.Equal(Path.Combine(paths.Lexicons, "EnglishLexicon.db"), paths.EnglishLexiconDatabase);
paths.EnsureRuntimeDirectories();
Assert.False(Directory.Exists(paths.Lexicons));
```

`LexiconTestDatabases` 必须用 `Microsoft.Data.Sqlite` 创建与构建器 schema 同名的必要表和 `schema_version=1` metadata。日语 fixture 至少包含两个 `食べる` 义项、`食べます/たべます`、英文 gloss、词性和普通/礼貌/て/た形式；英语 fixture 至少包含 `go%v` 的两个 SenseId、`went/gone/goes/going`、两个 definition，以及短语 `take off` 只有 Base。

- [ ] **Step 2: 写 DataAccessor 行为测试**

日语测试必须覆盖：以 `食べます` 完全匹配返回两个独立 EntryId；`食` 前缀匹配；每个结果带英文 gloss、POS、元数据和完整 forms；输入 `to eat` 返回空，证明未按 gloss 反查；结果上限与稳定顺序；预取消 token；缺失数据库、schema 不符和损坏 SQLite 抛 `LexiconDataAccessException`。

英语测试必须覆盖：以任意词形 `went` 找回 `go` 的两个独立 SenseId；两者共享 `go%v` 和同一整组 forms；`go` 前缀匹配；输入 definition 文本返回空；短语只有 Base；上限/顺序/取消；缺失、旧 schema 和损坏数据库错误。

- [ ] **Step 3: 写 Service 映射与非法输入测试**

```csharp
var results = await service.SearchAsync(
    new LexiconSearchRequestDto(LearningLanguage.English, "  WENT  ", 20),
    cancellationToken);

Assert.All(results, item => Assert.Equal("go%v", item.SourceLexemeId));
Assert.Equal(new[] { "原形", "第三人称单数", "过去式", "过去分词", "现在分词" },
    results[0].Forms.Select(form => form.FormLabel).Distinct());
```

另测：日语不改大小写但执行 Trim+Unicode Form C；英语执行 Trim+Form C+`ToLowerInvariant()`；空白 query、limit 小于 1/大于 50、未定义语言枚举抛清楚的参数异常；不同 SenseId 不合并；DataAccess 异常不被伪装成“0 个结果”。

- [ ] **Step 4: 建立可编译骨架并证明 RED**

先只添加使测试编译的类型与方法。临时让英语候选 SQL 同时匹配 `Definitions.Text`，运行：

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.DataAccess.EnglishLexiconDataAccessorTests
```

预期：输入 definition 文本的测试失败，显示预期空集合、实际返回 `go`；这证明“禁止释义反查”测试能发现错误。立即删除 definition 匹配，临时错误不得提交。

- [ ] **Step 5: 实现最小只读查询**

`ApplicationPaths` 只计算路径。连接工厂使用：

```csharp
new SqliteConnectionStringBuilder
{
    DataSource = path,
    Mode = SqliteOpenMode.ReadOnly,
    Pooling = false,
    ForeignKeys = true
};
```

两个 DataAccessor 均先检查 `BuildMetadata.schema_version='1'`。候选 SQL 只能从 `Forms.Surface` 进入，使用参数化的 `Surface=@Query OR Surface LIKE @Prefix ESCAPE '\\'`；对 `%`、`_`、`\\` 转义；完全匹配优先，再按词头/来源顺序稳定排序并 `LIMIT @Limit`。候选 ID 得到后分别批量读取主行、释义/POS 和 forms，用字典组装 PO，不执行每结果一次连接或写操作。

日语 meanings 只读取 `Glosses.Language='eng'`；英语 meanings 从所选 SenseId 的 Synset definitions 读取。Service 用私有纯函数映射中文标签：日语 `Dictionary/Polite/Te/Ta`→`普通形/礼貌形/て形/た形`，英语各 FormKind→批准的语法名，POS/VerbClass/Transitivity/KanjiUsage 也映射为用户可读属性。

- [ ] **Step 6: 做 mutation 并恢复**

临时把 `LexiconLookupService` 的 English 分支调用 Japanese accessor，运行：

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Services.LexiconLookupServiceTests
```

预期：英语 `went` 映射测试失败。恢复语言分派后，以下全部通过：

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.DataAccess.JapaneseLexiconDataAccessorTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.DataAccess.EnglishLexiconDataAccessorTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Services.LexiconLookupServiceTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
```

- [ ] **Step 7: 提交查询核心**

只暂存本 Task 的路径、DataAccess、Services、测试和 DI 文件；确认没有 `.db` 后提交：

```powershell
git commit -m "词库：实现日英语运行时正向查询"
```

---

### Task 2: 打通 WPF 查词入口与结果浏览

**Files:**
- Create/Modify: 上述 Presentation、Presentation tests 和资源文档文件。

**Interfaces:**

```csharp
public sealed class LexiconLookupViewModel : INotifyPropertyChanged
{
    public void Initialize(LearningLanguage language);
    public Task SearchAsync(CancellationToken cancellationToken = default);
    public string Query { get; set; }
    public ObservableCollection<LexiconLookupResultDto> Results { get; }
    public LexiconLookupResultDto? SelectedResult { get; set; }
    public ICommand SearchCommand { get; }
}

public sealed class LexiconLookupWindowFactory
{
    public bool? ShowDialog(Window owner, LearningLanguage language);
}
```

- [ ] **Step 1: 写 ViewModel 全链路测试**

测试直接使用 `LexiconTestDatabases`、真实两个 DataAccessor 和真实 Service，不为单实现 Service 新建 fake 接口。覆盖：Initialize 只能调用一次且拒绝未知语言；日语/英语窗口各自固定语言；空输入不查询并显示“请输入”；成功查询替换旧结果、默认选中第一项并打开结果下拉；0 结果显示明确空状态；数据库缺失显示可读错误且不崩溃；预取消 token 向上抛；第二次查询不会残留第一次结果。

- [ ] **Step 2: 建立 ViewModel 骨架并证明 RED**

临时让 `Initialize(LearningLanguage.English)` 总是保存 Japanese，运行：

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.LexiconLookupViewModelTests
```

预期：英语窗口查询 `went` 的测试失败且错误显示日语结果为空。恢复固定语言逻辑，不保留临时错误。

- [ ] **Step 3: 实现 ViewModel 与模态窗口**

ViewModel 捕获普通查询异常并设置 `ErrorMessage="查词失败：..."`，但预取消继续抛出；每次查询先清空旧集合和选择项，成功后按 Service 顺序填充，首项成为 `SelectedResult`。`SearchCommand` 复用现有 `AsyncCommand`，所有业务仍在 Service。

`LexiconLookupWindow.xaml` 使用现有 DynamicResource 主题：顶部显示固定的“日语查词”或“英语查词”；可编辑 ComboBox 绑定 Query/Results/SelectedResult，点击搜索或按 Enter 后显示下拉结果；结果模板显示 Headword、Reading、POS 和第一条 meaning；下方详情显示全部 meanings、属性与 forms。页面不提供语言切换、学习/复习确认或虚假进度。

`LexiconLookupWindowFactory` 从 DI 解析 transient 窗口，调用 ViewModel.Initialize，设置 `Owner` 后 `ShowDialog()`。`HomeWindow` 仅在按钮 Click 中把 `viewModel.SelectedLanguage` 交给工厂；关闭查词页后仍停留首页。

- [ ] **Step 4: 注册完整 DI 并记录资源布局**

DataAccess 注册连接工厂和两个具体 DataAccessor；Services 注册具体 `LexiconLookupService`；Presentation 注册 transient ViewModel/Window 和 singleton 工厂。`ThirdPartyResources.md` 在词库验收段后记录：

```text
Resources/Lexicons/
├── JapaneseLexicon.db
└── EnglishLexicon.db
```

说明两个文件由构建器生成、运行时只读、缺失时仅禁用查词链路且不在线下载；不要把真实数据库加入 Git。

- [ ] **Step 5: 做 UI 状态 mutation 并恢复**

临时在成功查询后不设置 `SelectedResult`，运行 ViewModel 聚焦测试，确认“默认选中第一项”断言失败；恢复并运行：

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.LexiconLookupViewModelTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
```

- [ ] **Step 6: WPF 冒烟与泄漏检查**

运行：

```powershell
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj --configuration Debug --no-build
```

人工确认：首页日语/英语选择决定查词页语言；查词页没有语言切换；输入框可搜索并下拉选择；选择义项后 meanings/forms 刷新；关闭返回首页；缺少真实 Resources 时显示中文错误但主程序继续运行。再执行 `rg --files -g "*.db" -g "!**/bin/**" -g "!**/obj/**"`，预期仓库源码范围无数据库。

- [ ] **Step 7: 提交完整垂直链路**

只暂存 Presentation、对应测试、DI 和资源文档；确认 branch 仍为 `codex/dictation-refactor`、测试/构建通过后提交：

```powershell
git commit -m "词库：打通WPF离线查词入口"
```

## Self-Review

- Spec coverage: 本计划覆盖两种语言正向查词、不同义项独立、相同英语词元共享 forms、短语固定形式、首页语言固定、离线只读和资源缺失错误；Import Inbox、派生组扩展和中文翻译明确留给后续独立垂直片。
- Placeholder scan: 每个步骤都有确定文件、接口、失败证据和命令；无未定义的占位实现。
- Type consistency: DataAccessor 只返回 PO；Service 唯一接收 `LexiconSearchRequestDto` 并返回 `LexiconLookupResultDto`；ViewModel 与窗口只依赖 Service DTO，不引用 PO、SQL 或数据库连接。
- Simplicity: 复用现有 Dapper、SQLite、DI、AsyncCommand 和主题；只增加两个具体 DataAccessor、一个 Service 和一个 WPF 窗口，不增加接口、Repository、在线依赖或用户表。
