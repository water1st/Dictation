using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    public sealed class ImportInboxListViewModelTests
    {
        [Fact]
        public void Initialize_RejectsUnsupportedLanguageAndDuplicateInitialization()
        {
            using var environment = new TestEnvironment();
            var viewModel = environment.CreateViewModel();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                viewModel.Initialize((LearningLanguage)99));

            viewModel.Initialize(LearningLanguage.English);
            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Throws<InvalidOperationException>(() =>
                viewModel.Initialize(LearningLanguage.Japanese));
        }

        [Fact]
        public async Task OperationsBeforeInitialization_Throw()
        {
            using var environment = new TestEnvironment();
            var viewModel = environment.CreateViewModel();

            await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.RefreshAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                viewModel.RefreshPreservingStateAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                viewModel.OpenSelectedDetailAsync());
        }

        [Fact]
        public async Task RefreshAsync_UsesFixedLanguageAndMapsPendingItems()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var service = environment.ImportInboxService;
            await StageAsync(service, new ImportSelectionDto("went", "go%2:01:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);

            await viewModel.RefreshAsync();

            var batch = Assert.Single(viewModel.Batches);
            var item = Assert.Single(viewModel.Items);
            Assert.Equal(batch.BatchId, item.Batch.BatchId);
            Assert.Equal("went", item.SourceText);
            Assert.Equal(ImportItemDecision.Pending, item.Decision);
            Assert.True(item.IsSelectable);
            Assert.True(viewModel.CanDecide == false);
            Assert.False(viewModel.IsAllSelected);
        }

        [Fact]
        public async Task FilterText_FiltersVisibleItemsWithoutDroppingHiddenSelection()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await StageAsync(
                environment.ImportInboxService,
                new ImportSelectionDto("went", "go%2:01:00::"),
                new ImportSelectionDto("take off", "take_off%2:38:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();

            var selected = viewModel.Items.Single(item => item.SourceText == "went");
            selected.IsSelected = true;
            viewModel.FilterText = "take";

            Assert.Single(viewModel.Items);
            Assert.Equal("take off", viewModel.Items[0].SourceText);
            Assert.True(viewModel.CanDecide);
            viewModel.FilterText = string.Empty;
            Assert.Contains(viewModel.Items, item => item.ItemId == selected.ItemId && item.IsSelected);
        }

        [Fact]
        public async Task IsAllSelected_IsTriStateAndNeverSelectsDuplicateItems()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var service = environment.ImportInboxService;
            var firstBatch = await StageAsync(
                service,
                new ImportSelectionDto("went", "go%2:01:00::"));
            await service.DecideAsync(
                new DecideImportBatchRequestDto(
                    firstBatch.Batch.BatchId,
                    new[] { Assert.Single(firstBatch.Batch.Items).ItemId },
                    ImportDecision.Learning));
            await StageAsync(
                service,
                new ImportSelectionDto("went", "go%2:01:00::"),
                new ImportSelectionDto("symbols", "missing:symbols:v"));

            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();
            var duplicate = Assert.Single(viewModel.Items, item => !item.IsSelectable);
            var pending = Assert.Single(viewModel.Items, item => item.IsSelectable);

            pending.IsSelected = true;
            Assert.True(viewModel.IsAllSelected);
            viewModel.IsAllSelected = false;
            Assert.False(pending.IsSelected);
            Assert.False(duplicate.IsSelected);
            viewModel.IsAllSelected = true;
            Assert.True(pending.IsSelected);
            Assert.False(duplicate.IsSelected);

            pending.IsSelected = false;
            duplicate.IsSelected = true;
            Assert.False(duplicate.IsSelected);
            Assert.False(viewModel.CanDecide);
        }

        [Fact]
        public async Task DecideAsync_PartialDecisionKeepsBatchAndProcessesOnlySelectedItems()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await StageAsync(
                environment.ImportInboxService,
                new ImportSelectionDto("went", "go%2:01:00::"),
                new ImportSelectionDto("take off", "take_off%2:38:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();

            var batchId = Assert.Single(viewModel.Batches).BatchId;
            var first = viewModel.Items.Single(item => item.SourceText == "went");
            first.IsSelected = true;
            await viewModel.DecideAsync(ImportDecision.Learning);

            Assert.Equal(batchId, viewModel.SelectedBatch?.BatchId);
            Assert.False(Assert.Single(viewModel.Items, item => item.ItemId == first.ItemId).IsSelectable);
            var remaining = Assert.Single(viewModel.Items, item => item.IsSelectable);
            Assert.Equal("take off", remaining.SourceText);
            Assert.Equal(ImportItemDecision.Pending, remaining.Decision);

            remaining.IsSelected = true;
            await viewModel.DecideAsync(ImportDecision.Review);
            Assert.Empty(viewModel.Batches);
            Assert.Empty(viewModel.Items);
        }

        [Fact]
        public async Task DecideAsync_WithoutSelectionAndInvalidDecisionOnlyShowInlineValidation()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await StageAsync(environment.ImportInboxService, new ImportSelectionDto("went", "go%2:01:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();

            await viewModel.DecideAsync((ImportDecision)99);
            Assert.Contains("决定", viewModel.ErrorMessage, StringComparison.Ordinal);
            await viewModel.DecideAsync(ImportDecision.Learning);
            Assert.Contains("选择", viewModel.ErrorMessage, StringComparison.Ordinal);
            Assert.Single(await environment.ImportInboxService.GetPendingBatchesAsync(LearningLanguage.English));
        }

        [Fact]
        public async Task Discard_CancelLeavesDatabaseAndSelectionUnchanged()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await StageAsync(environment.ImportInboxService, new ImportSelectionDto("went", "go%2:01:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();
            var item = Assert.Single(viewModel.Items);
            item.IsSelected = true;

            var decision = viewModel.DecideAsync(ImportDecision.Discard);
            await WaitForOverlayAsync(environment.Overlay);
            environment.Overlay.Cancel();
            await decision;

            Assert.True(item.IsSelected);
            Assert.Equal(ImportItemDecision.Pending, item.Decision);
            Assert.Single(await environment.ImportInboxService.GetPendingBatchesAsync(LearningLanguage.English));
        }

        [Fact]
        public async Task Discard_AcceptRemovesTheSelectedPendingGroup()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await StageAsync(environment.ImportInboxService, new ImportSelectionDto("went", "go%2:01:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();
            viewModel.Items[0].IsSelected = true;

            var decision = viewModel.DecideAsync(ImportDecision.Discard);
            await WaitForOverlayAsync(environment.Overlay);
            environment.Overlay.Accept();
            await decision;

            Assert.Empty(viewModel.Batches);
            Assert.Empty(viewModel.Items);
            Assert.Empty(await environment.ImportInboxService.GetPendingBatchesAsync(LearningLanguage.English));
        }

        [Fact]
        public async Task RefreshPreservingState_PreservesBatchItemFilterAndScrollState()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await StageAsync(environment.ImportInboxService, new ImportSelectionDto("went", "go%2:01:00::"));
            await StageAsync(environment.ImportInboxService, new ImportSelectionDto("take off", "take_off%2:38:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();

            var oldViewModel = viewModel;
            var selectedBatch = viewModel.Batches.Single(batch => batch.Items[0].SourceText == "take off");
            viewModel.SelectedBatch = selectedBatch;
            var selectedItem = Assert.Single(viewModel.Items);
            selectedItem.IsSelected = true;
            viewModel.FilterText = "take";
            viewModel.ScrollOffset = 83.5;
            var oldBatch = selectedBatch;
            var oldItem = selectedItem;

            await viewModel.RefreshPreservingStateAsync();

            Assert.Same(oldViewModel, viewModel);
            Assert.NotSame(oldBatch, viewModel.SelectedBatch);
            Assert.Equal(oldBatch.BatchId, viewModel.SelectedBatch?.BatchId);
            Assert.Equal("take", viewModel.FilterText);
            Assert.Equal(83.5, viewModel.ScrollOffset);
            var refreshedItem = Assert.Single(viewModel.Items);
            Assert.Equal(oldItem.ItemId, refreshedItem.ItemId);
            Assert.True(refreshedItem.IsSelected);
            Assert.Same(viewModel.SelectedItem, refreshedItem);
        }

        [Fact]
        public async Task RefreshUnexpectedError_UsesMessageOverlayAndPreservesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = CreateService(paths);
            var overlay = new OverlayService();
            var navigation = new NavigationService(new TestScopeFactory());
            var viewModel = new ImportInboxListViewModel(service, service, navigation, overlay);
            viewModel.Initialize(LearningLanguage.English);
            var refresh = viewModel.RefreshAsync();
            await WaitForOverlayAsync(overlay);
            Assert.Equal(OverlayKind.Message, overlay.CurrentRequest?.Kind);
            overlay.Dismiss();
            await refresh;
            Assert.Contains("收件箱加载失败", viewModel.ErrorMessage, StringComparison.Ordinal);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                viewModel.RefreshPreservingStateAsync(cancellation.Token));
        }

        [Fact]
        public async Task DecideAsync_PreCanceledDoesNotChangeThePendingItem()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await StageAsync(environment.ImportInboxService, new ImportSelectionDto("went", "go%2:01:00::"));
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync();
            viewModel.Items[0].IsSelected = true;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                viewModel.DecideAsync(ImportDecision.Learning, cancellation.Token));
            Assert.Equal(ImportItemDecision.Pending, viewModel.Items[0].Decision);
            Assert.True(viewModel.Items[0].IsSelected);
        }

        private static async Task<StageImportBatchResultDto> StageAsync(
            ImportInboxService service,
            params ImportSelectionDto[] selections)
        {
            return await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    selections));
        }

        private static async Task WaitForOverlayAsync(OverlayService overlay)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!overlay.IsVisible && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1);
            }

            Assert.True(overlay.IsVisible, "等待收件箱弹层显示超时。");
        }

        private static ImportInboxService CreateService(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            return new ImportInboxService(
                new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(factory),
                    new EnglishLexiconDataAccessor(factory)),
                new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
        }

        private sealed class TestEnvironment : IDisposable
        {
            private readonly TemporaryDirectory temporaryDirectory;

            public TestEnvironment()
            {
                temporaryDirectory = new TemporaryDirectory();
                Paths = new ApplicationPaths(temporaryDirectory.Path);
                ImportInboxService = CreateService(Paths);
                Overlay = new OverlayService();
                Navigation = new NavigationService(new TestScopeFactory());
            }

            private TestEnvironment(
                TemporaryDirectory temporaryDirectory,
                ApplicationPaths paths,
                ImportInboxService importInboxService)
            {
                this.temporaryDirectory = temporaryDirectory;
                Paths = paths;
                ImportInboxService = importInboxService;
                Overlay = new OverlayService();
                Navigation = new NavigationService(new TestScopeFactory());
            }

            public ApplicationPaths Paths { get; }

            public ImportInboxService ImportInboxService { get; }

            public OverlayService Overlay { get; }

            public NavigationService Navigation { get; }

            public static async Task<TestEnvironment> CreateAsync()
            {
                var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                paths.EnsureRuntimeDirectories();
                LexiconTestDatabases.CreateBoth(paths);
                await new SchemaMigrationRunner(
                    paths,
                    new SqliteConnectionFactory(paths)).MigrateAsync();
                return new TestEnvironment(temporaryDirectory, paths, CreateService(paths));
            }

            public ImportInboxListViewModel CreateViewModel()
            {
                return new ImportInboxListViewModel(
                    ImportInboxService,
                    ImportInboxService,
                    Navigation,
                    Overlay);
            }

            public void Dispose()
            {
                Navigation.Dispose();
                Overlay.Dispose();
                temporaryDirectory.Dispose();
            }
        }

        private sealed class TestScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
            {
                throw new NotSupportedException("该测试不执行页面创建导航。");
            }
        }
    }
}
