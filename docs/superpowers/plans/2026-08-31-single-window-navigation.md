# WPF 单窗口导航与 Import Inbox 页面化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将首页、查词、导入收件箱列表和详情统一迁入唯一的 `HomeWindow`，用 `ContentControl + UserControl` 实现可返回、保留状态并带方向动画的页面导航；业务提示使用主窗口内弹层，杜绝查词／收件箱独立 Window。

**Architecture:** `HomeWindow` 是纯壳层，绑定 `ShellViewModel`；具体 `NavigationService` 持有页面栈、固定语言、DI scope 和前进／返回方向；页面由独立 `UserControl + ViewModel` 构成并通过 WPF `DataTemplate` 映射。页面只调用语义导航方法，不引用其它页面或窗口。`OverlayService` 在壳层内处理确认、错误和忙碌状态。

**Tech Stack:** C# 14、.NET 10、WPF、Microsoft.Extensions.DependencyInjection、现有 Services/DTO、xUnit v3。

**Spec:** `docs/superpowers/specs/2026-08-31-single-window-chinese-lexicon-design.md`

**Depends on:** `docs/superpowers/plans/2026-08-31-chinese-runtime-lexicons.md` 全部完成且全量测试为绿。

## Global Constraints

- 当前未提交的 Task 2 文件属于待修正工作，不能整体丢弃、覆盖或回退；按本计划逐项转换后再提交。
- 最终唯一非抽象 WPF `Window` 类型是 `HomeWindow`。Windows 原生打开／保存文件对话框仍是允许的输入适配例外。
- 页面类型固定为 `HomePage`、`LexiconLookupPage`、`ImportInboxListPage`、`ImportInboxDetailPage`，均继承 `UserControl`。
- ViewModel 不引用 `Window`、`UserControl`、其它页面 ViewModel 或动画类型；页面 code-behind 只允许焦点、滚动和键盘等视图适配。
- 只有首页可更改语言。导航到查词或收件箱时复制并固定 `LearningLanguage`；子页面没有语言切换控件。
- 返回保留同一个上一页 ViewModel 实例，因此查询、结果、选择、筛选、勾选和滚动位置不丢失；弹出栈的页面及其 DI scope 在转场结束后释放。
- 不保留前进历史，不实现 URI、`Frame/Page`、消息总线或第三方导航/UI 框架。
- 动画固定约 200 ms：前进旧页向左、新页从右；返回方向相反；动画不执行业务逻辑。
- “暂无中文释义”只是显示文本。查词页可编辑中文快照；Inbox 详情可逐成员补写。空中文不能送入 Learning/Review。
- 每个业务 RED 遵循“先编译成功、再发现测试、再断言失败、冻结测试、只改生产实现转绿”；临时 mutation 不提交。
- 每个提交前检查当前分支仍为 `codex/dictation-refactor`，只暂存当前垂直片文件。

---

## File and Responsibility Map

### Navigation shell

- Create: `Dictation.Presentation/Navigation/NavigationDirection.cs`
- Create: `Dictation.Presentation/Navigation/NavigationTransitionEventArgs.cs`
- Create: `Dictation.Presentation/Navigation/NavigationService.cs`
- Create: `Dictation.Presentation/Shell/ShellViewModel.cs`
- Create: `Dictation.Presentation/Shell/OverlayService.cs`
- Create: `Dictation.Presentation/Behaviors/ScrollViewerOffsetBehavior.cs`
- Modify: `Dictation.Presentation/HomeWindow.xaml`
- Modify: `Dictation.Presentation/HomeWindow.xaml.cs`
- Modify: `Dictation.Presentation/App.xaml`
- Modify: `Dictation.Presentation/Program.cs`
- Modify: `Dictation.Presentation/DictationPresentationServiceCollectionExtensions.cs`

### Pages

- Create: `Dictation.Presentation/Views/HomePage.xaml(.cs)`
- Create: `Dictation.Presentation/Views/LexiconLookupPage.xaml(.cs)`
- Create: `Dictation.Presentation/Views/ImportInboxListPage.xaml(.cs)`
- Create: `Dictation.Presentation/Views/ImportInboxDetailPage.xaml(.cs)`
- Modify: `Dictation.Presentation/ViewModels/HomeViewModel.cs`
- Modify: `Dictation.Presentation/ViewModels/LexiconLookupViewModel.cs`
- Replace untracked `ImportInboxViewModel.cs` with `ImportInboxListViewModel.cs` and `ImportInboxDetailViewModel.cs`

### Remove superseded WPF windows

- Delete: `Dictation.Presentation/LexiconLookupWindow.xaml(.cs)`
- Delete: `Dictation.Presentation/LexiconLookupWindowFactory.cs`
- Do not add/retain: untracked `ImportInboxWindow.xaml(.cs)` and `ImportInboxWindowFactory.cs`
- Remove their DI registrations.
- Remove dormant legacy WinForms window/factory registrations from the active startup graph; legacy source files may remain until their later WPF migration, but they cannot be resolved or shown by the current application.

### Tests

- Create: `Dictation.Tests/Presentation/NavigationServiceTests.cs`
- Create: `Dictation.Tests/Presentation/OverlayServiceTests.cs`
- Create: `Dictation.Tests/Presentation/SingleWindowArchitectureTests.cs`
- Modify: `Dictation.Tests/Presentation/HomeViewModelTests.cs`
- Rename/rewrite: `LexiconLookupWindowInteractionTests.cs` → `LexiconLookupPageInteractionTests.cs`
- Rewrite: `LexiconLookupViewModelTests.cs`
- Replace: `ImportInboxViewModelTests.cs` with list/detail ViewModel tests
- Replace: `WpfMeaningRenderingTests.cs` with `LexiconLookupPageRenderingTests.cs` and `ImportInboxPageRenderingTests.cs`

## Fixed Navigation Contracts

```csharp
public enum NavigationDirection
{
    Forward,
    Backward
}

public sealed class NavigationService : INotifyPropertyChanged, IDisposable
{
    public object CurrentPage { get; }
    public string PageTitle { get; }
    public bool CanGoBack { get; }
    public NavigationDirection LastDirection { get; }

    public event EventHandler<NavigationTransitionEventArgs>? TransitionRequested;

    public void InitializeHome(HomeViewModel homeViewModel);
    public Task OpenLexiconLookupAsync(
        LearningLanguage language,
        CancellationToken cancellationToken = default);
    public Task OpenImportInboxAsync(
        LearningLanguage language,
        CancellationToken cancellationToken = default);
    public Task OpenImportInboxDetailAsync(
        ImportBatchDto batch,
        long itemId,
        CancellationToken cancellationToken = default);
    public Task GoBackAsync(CancellationToken cancellationToken = default);
    public void CompleteTransition();
}
```

`NavigationService` is concrete and registered once. Its private stack entry owns:

```csharp
private sealed record NavigationEntry(
    object ViewModel,
    string Title,
    LearningLanguage? Language,
    IServiceScope? Scope,
    Func<CancellationToken, Task>? ResumeAsync);
```

`ShellViewModel` only exposes the concrete navigation and overlay services. `HomeWindow` subscribes to `TransitionRequested`, feeds old/new ViewModels into two overlapping `ContentControl` presenters, runs the transform/opacity animations, then calls `CompleteTransition()`.

`App.xaml` maps ViewModels to views:

```xml
<DataTemplate DataType="{x:Type viewModels:HomeViewModel}">
    <views:HomePage />
</DataTemplate>
<DataTemplate DataType="{x:Type viewModels:LexiconLookupViewModel}">
    <views:LexiconLookupPage />
</DataTemplate>
<DataTemplate DataType="{x:Type viewModels:ImportInboxListViewModel}">
    <views:ImportInboxListPage />
</DataTemplate>
<DataTemplate DataType="{x:Type viewModels:ImportInboxDetailViewModel}">
    <views:ImportInboxDetailPage />
</DataTemplate>
```

---

### Task 1: Establish the single-window shell and stateful navigation

**Files:**
- Create/Modify: all Navigation shell files, `HomePage` and the three architecture/navigation test files
- Modify: `HomeViewModel.cs`, `App.xaml`, `Program.cs` and Presentation DI

- [ ] **Step 1: Write architecture and stack tests**

At this first checkpoint, `SingleWindowArchitectureTests` verifies the new shell and homepage only:

```csharp
Assert.True(typeof(UserControl).IsAssignableFrom(typeof(HomePage)));
Assert.Contains(
    FindVisualChildren<ContentControl>(homeWindow),
    control => control.Name == "CurrentPagePresenter");
```

Also assert `HomeViewModel` has no `Window`/`UserControl` dependency. The repository-wide “only HomeWindow derives WPF Window” assertion is intentionally added in Task 4, after Task 2 and Task 3 have removed the two superseded Windows; it cannot be a false RED at this checkpoint.

`NavigationServiceTests` cover: initialize exactly once; forward Home→Lookup; fixed language; Backward direction; no back at root; returning to the same previous VM instance; no forward history; popped scope remains alive during transition and is disposed only after `CompleteTransition`; application disposal releases every retained scope.

- [ ] **Step 2: Add compile surfaces and establish behavioral RED**

Add the minimum classes/methods needed for a successful test-project build. Record hashes of the new tests. Temporarily report `NavigationDirection.Forward` from `GoBackAsync`, then run:

```powershell
dotnet build Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.NavigationServiceTests
```

Expected RED: the back-direction assertion expects `Backward` but receives `Forward`. Restore only the production mutation.

- [ ] **Step 3: Move homepage content into a UserControl**

Move the current page body and its game-like cards from `HomeWindow.xaml` into `Views/HomePage.xaml`. Keep language/theme commands on `HomeViewModel`. Add `OpenLexiconLookupCommand` and `OpenImportInboxCommand` that call semantic methods on `NavigationService` with the current homepage language.

`HomeWindow` becomes a shell with:

- header back button, current title and existing current-language badge where appropriate;
- two overlapping `ContentControl` presenters;
- an overlay layer above the presenters;
- no query/inbox controls and no page-specific Service calls.

- [ ] **Step 4: Implement stack/scope lifetime and startup**

Register `NavigationService`, `OverlayService`, `ShellViewModel` and `HomeWindow` as singletons; register child page ViewModels as scoped. `NavigationService` creates one DI scope per child page and owns it. `HomeViewModel` remains singleton because `App` currently observes it for theme changes and it lives for the full application lifetime.

After existing settings initialization, `Program.Main` calls:

```csharp
navigationService.InitializeHome(homeViewModel);
application.Run(homeWindow);
```

No secondary Window is resolved during startup or navigation.

- [ ] **Step 5: Implement shell transition and scroll-state adapter**

`HomeWindow.xaml.cs` uses `TranslateTransform` and `DoubleAnimation` with 200 ms duration. It contains no destination-type switch and no Service call. `ScrollViewerOffsetBehavior.VerticalOffset` provides two-way attached state so each page ViewModel can retain its own offset without referencing a `ScrollViewer`.

- [ ] **Step 6: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.NavigationServiceTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.OverlayServiceTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.SingleWindowArchitectureTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "界面：建立WPF单窗口导航壳层"
```

---

### Task 2: Convert lookup Window to a fixed-language page

**Files:**
- Create: `Views/LexiconLookupPage.xaml(.cs)`
- Modify: `ViewModels/LexiconLookupViewModel.cs`
- Delete: lookup Window and factory files
- Rewrite: lookup ViewModel/interaction/rendering tests
- Modify: Presentation DI

**ViewModel additions:**

```csharp
public string ChineseMeaningDraft { get; set; }
public bool IsMeaningEdited { get; }
public string MeaningStatusLabel { get; }
public double ScrollOffset { get; set; }
public ICommand BackCommand { get; }
```

- [ ] **Step 1: Rewrite tests around the page**

Retain existing language initialization, blank query, replacement, cancellation, staging and duplicate coverage. Add:

- every rendered meaning is Chinese fixture content; English source definitions/glosses are absent;
- exact/broad/missing badges use DTO metadata;
- selecting a result initializes an editable one-meaning-per-line draft;
- editing supplies `ChineseMeaningOverride` to Inbox staging without modifying the lookup DTO or lexicon DB;
- missing meaning displays “暂无中文释义” but may still enter Inbox;
- language cannot change after page initialization;
- two consecutive Enter/search-button actions use the current query and do not clear it before Service invocation.

- [ ] **Step 2: Establish behavioral RED**

Build successfully and freeze rewritten tests. Temporarily reset `Query` before capturing the request in `SearchAsync`. Run the ViewModel and real-WPF interaction classes. Expected RED: the second query finds zero results or uses the previous text rather than the entered text. Restore the mutation.

- [ ] **Step 3: Implement the independent UserControl**

`LexiconLookupPage` has a parameterless constructor and uses only bindings/events for focus or Enter. Its result cards show headword, reading, POS, attributes, Chinese meanings, source/precision badge and complete forms. Use spacing/cards rather than a dense grid. The audio button remains an icon-only speaker button when wired later; do not label it “TTS”.

`LexiconLookupViewModel` depends on `LexiconLookupService`, `ImportInboxService`, `NavigationService` and `OverlayService` only. It never accesses `HomeWindow` or another page.

- [ ] **Step 4: Remove modal lookup path**

Delete `LexiconLookupWindow` and its factory, remove `ShowDialog` call sites and DI registrations, and replace the Home code-behind click handler with the Home ViewModel command. Re-entering lookup from Home creates a new page scope; Back disposes it after animation.

- [ ] **Step 5: GREEN and commit**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.LexiconLookupViewModelTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.LexiconLookupPageInteractionTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.LexiconLookupPageRenderingTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "词库：将查词迁入单窗口页面"
```

---

### Task 3: Split Import Inbox into list and detail pages

**Files:**
- Create: `Views/ImportInboxListPage.xaml(.cs)`
- Create: `Views/ImportInboxDetailPage.xaml(.cs)`
- Create: `ViewModels/ImportInboxListViewModel.cs`
- Create: `ViewModels/ImportInboxDetailViewModel.cs`
- Remove/replace: untracked Window, factory and combined ViewModel files
- Rewrite: Inbox ViewModel and rendering tests
- Modify: Navigation/Overlay integration and DI

**List contract:**

```csharp
public sealed class ImportInboxListViewModel : INotifyPropertyChanged
{
    public void Initialize(LearningLanguage language);
    public Task RefreshAsync(CancellationToken cancellationToken = default);
    public Task RefreshPreservingStateAsync(CancellationToken cancellationToken = default);
    public Task OpenSelectedDetailAsync(CancellationToken cancellationToken = default);
    public ObservableCollection<ImportBatchDto> Batches { get; }
    public ObservableCollection<ImportInboxItemViewModel> Items { get; }
    public bool? IsAllSelected { get; set; }
    public double ScrollOffset { get; set; }
}
```

**Detail contract:**

```csharp
public sealed class ImportInboxDetailViewModel : INotifyPropertyChanged
{
    public void Initialize(ImportBatchDto batch, long itemId);
    public ObservableCollection<ImportMemberEditorViewModel> Members { get; }
    public Task SaveMeaningsAsync(CancellationToken cancellationToken = default);
    public double ScrollOffset { get; set; }
}
```

- [ ] **Step 1: Write list-state and detail-edit tests**

List tests cover fixed language, pending batches, batch selection, tri-state top-level checkbox, duplicate non-selectability, partial decisions, cancellation and empty/error state. Detail tests cover:

- the selected item only, with every group member’s headword, reading, POS, attributes, forms, Chinese source and precision;
- one editable multi-line Chinese field per member;
- blank saves rejected; valid saves call `UpdateMeaningsAsync` and refresh metadata to User/UserProvided;
- Learning/Review remains blocked while any group member is empty;
- Discard confirmation uses the shell overlay.

- [ ] **Step 2: Test return-state preservation**

Navigate Home→Inbox list→detail. Set a non-first batch, partial checkbox selection, filter text and non-zero scroll offset. Edit a member, then Back. Assert the exact same list ViewModel reference returns, batch/filter/check/offset are preserved, and only refreshed DTO content changes.

- [ ] **Step 3: Establish behavioral RED**

After compile and test-hash freeze, temporarily clear selected item IDs in `RefreshPreservingStateAsync`. Expected RED: after returning from detail, the selected batch remains but partial checkbox state is lost. Restore the mutation.

- [ ] **Step 4: Implement list/detail navigation**

The list page shows airy cards and summary badges, not a master-detail ERP table. “全选” is the first checkbox and affects only pending items. Clicking a row/detail button calls `NavigationService.OpenImportInboxDetailAsync(batch, itemId)`.

The detail page edits the Inbox snapshot through `ImportInboxService`. On back, the retained list entry’s `ResumeAsync` delegate calls `RefreshPreservingStateAsync`; NavigationService itself owns that delegate and does not make the detail ViewModel reference the list ViewModel.

- [ ] **Step 5: Implement in-window overlay behavior**

`OverlayService` provides message, confirm and busy requests using `TaskCompletionSource` and shell-bound commands. Unexpected load/save/decision errors and Discard confirmation use it. Field validation and empty-list status may remain inline because they are part of the page, but no `MessageBox` or secondary business Window is allowed.

- [ ] **Step 6: Remove modal Inbox path and verify GREEN**

Do not retain the untracked `ImportInboxWindow` or factory. Remove their DI registrations. Then run:

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.ImportInboxListViewModelTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.ImportInboxDetailViewModelTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.NavigationServiceTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore -- --filter-class Dictation.Tests.Presentation.ImportInboxPageRenderingTests
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
git commit -m "导入：将收件箱拆为列表详情页面"
```

---

### Task 4: Run single-window acceptance and close corrected Task 2

**Files:**
- Modify only files required by failures found in this plan’s own scope
- Update: `docs/superpowers/plans/2026-08-27-unified-import-inbox.md` status note to point at the superseding spec/plans

- [ ] **Step 1: Static architecture gates**

First extend `SingleWindowArchitectureTests` now that both modal pages are gone:

```csharp
var windows = typeof(HomeWindow).Assembly
    .GetTypes()
    .Where(type => !type.IsAbstract && typeof(System.Windows.Window).IsAssignableFrom(type))
    .ToArray();

Assert.Equal(typeof(HomeWindow), Assert.Single(windows));
Assert.True(typeof(UserControl).IsAssignableFrom(typeof(LexiconLookupPage)));
Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ImportInboxListPage)));
Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ImportInboxDetailPage)));
```

Then run:

```powershell
rg -n "LexiconLookupWindow|ImportInboxWindow|LexiconLookupWindowFactory|ImportInboxWindowFactory" Dictation.Presentation Dictation.Tests
rg -n "System\.Windows\.MessageBox|MessageBox\.Show" Dictation.Presentation
rg -n "Frame|NavigationService\.NavigateUri|NavigationWindow" Dictation.Presentation
rg -n "ShowDialog\(this|ShowDialog\(owner" Dictation.Presentation
```

Expected: no business-window/factory, MessageBox, Frame/URI-navigation or owner-modal hits. Native `OpenFileDialog/SaveFileDialog` hits in dormant legacy adapters are allowed and must be reviewed, not blindly removed.

- [ ] **Step 2: Full automated verification**

```powershell
dotnet test Dictation.Tests/Dictation.Tests.csproj --configuration Debug --no-restore
dotnet build Dictation.sln --configuration Debug --no-restore
git diff --check
rg --files -g "*.db" -g "!**/bin/**" -g "!**/obj/**"
```

Expected: all tests pass, build has zero errors, no whitespace errors, and no source-scope database files.

- [ ] **Step 3: Real WPF smoke**

Run:

```powershell
dotnet run --project Dictation.Presentation/Dictation.Presentation.csproj --configuration Debug --no-build
```

Verify manually:

1. Startup shows one `HomeWindow` and no modal business window.
2. Language changes only on Home; lookup/inbox keep the chosen language.
3. Forward/back animations use opposite directions and remain around 200 ms.
4. Lookup search, selection, Chinese draft and Inbox staging work continuously.
5. Inbox list→detail→back preserves batch, checks, filter and scroll.
6. Missing Chinese cannot enter Learning/Review until edited.
7. Confirmation/error/loading appears inside the HomeWindow overlay.
8. Reopening the executable activates the existing HomeWindow without creating another.

- [ ] **Step 4: Final scope review and commit**

Inspect staged and unstaged diffs. Confirm current Task 2 files have been converted rather than discarded, only `HomeWindow` derives WPF Window, and no test remains frozen in a false-green state. Commit any acceptance-only documentation/static cleanup:

```powershell
git commit -m "界面：完成单窗口导入链路验收"
```

If Task 3 changes are already complete and no extra tracked change remains, do not create an empty commit.

## Self-Review

- **Spec coverage:** one Window, ContentControl/UserControl, central back stack, fixed child language, preserved state, page scopes, reverse transition, list/detail split, in-window overlay and Chinese editing all have explicit tests and implementation tasks.
- **Placeholder scan:** page names, methods, lifetime rules, RED mutations, commands and expected results are fixed; no TODO/TBD or undecided UI framework remains.
- **Coupling check:** only NavigationService knows destination ViewModel types; pages know semantic methods but not destination classes, views, Window or animation. Shell owns visual transition and overlay.
- **Simplicity:** one concrete navigation service, one concrete overlay service, WPF DataTemplates and two ContentControls; no interface hierarchy, Frame, URI log, message bus or third-party UI package.
- **Dirty-worktree safety:** existing Task 2 files are named explicitly and converted in place; unrelated TTS, updater, resource and user-data work is outside this plan.
