using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证计划选项绑定、保存、学习库选择和启动请求。
    /// </summary>
    public sealed class LearningPlanViewModelTests
    {
        [Fact]
        public async Task InitializeAndRefresh_LoadFixedLanguageDefaultsAndOnlyItsLearningGroups()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageAndDecideAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1", ImportDecision.Learning);
            await environment.StageAndDecideAsync(LearningLanguage.English, "gone", "go%2:01:00::", ImportDecision.Learning);
            var viewModel = environment.CreateViewModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.Initialize((LearningLanguage)99));
            viewModel.Initialize(LearningLanguage.Japanese);
            Assert.Throws<InvalidOperationException>(() => viewModel.Initialize(LearningLanguage.English));
            await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.Equal("日语学习计划", viewModel.WindowTitle);
            Assert.Equal(20, viewModel.DailyNewTarget);
            Assert.Equal(20, viewModel.SessionWordLimit);
            Assert.Equal(LearningSelectionMode.Random, viewModel.SelectionMode);
            Assert.Contains(viewModel.FormOptions, option => option.Code == "Dictionary" && option.IsSelected);
            Assert.Contains(viewModel.OutputOptions, option => option.Code == "Kana" && option.IsSelected);
            Assert.Contains(viewModel.OutputOptions, option => option.Code == "Kanji" && option.IsSelected);
            var item = Assert.Single(viewModel.LibraryItems);
            Assert.Equal("食べる", item.PrimaryMember?.Headword);
        }

        [Fact]
        public async Task Selection_IsTriStateAndManualStartUsesSelectedWholeGroups()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageAndDecideAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1", ImportDecision.Learning);
            await environment.StageAndDecideAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1", ImportDecision.Learning);
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.Japanese);
            await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
            viewModel.SelectionMode = LearningSelectionMode.Manual;

            await viewModel.StartAsync(TestContext.Current.CancellationToken);
            Assert.Contains("选择", viewModel.ErrorMessage, StringComparison.Ordinal);

            viewModel.LibraryItems[0].IsSelected = true;
            Assert.Null(viewModel.IsAllSelected);
            viewModel.IsAllSelected = true;
            Assert.All(viewModel.LibraryItems, item => Assert.True(item.IsSelected));
            Assert.True(viewModel.IsAllSelected);
            viewModel.LibraryItems[1].IsSelected = false;
            Assert.Null(viewModel.IsAllSelected);

            await viewModel.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.Japanese, environment.LearningNavigation.LastLanguage);
            Assert.Equal(new[] { viewModel.LibraryItems[0].WordGroupId },
                environment.LearningNavigation.LastSelectedWordGroupIds);
        }

        [Fact]
        public async Task SaveAsync_PersistsVisibleLanguagePlanAndSelectedOptions()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.Japanese);
            await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
            viewModel.DailyNewTarget = 35;
            viewModel.SessionWordLimit = 12;
            viewModel.SelectionMode = LearningSelectionMode.Sequential;
            SetSelected(viewModel.FormOptions, "Dictionary", "Polite", "Te");
            SetSelected(viewModel.ChannelOptions, "MeaningWriting");
            SetSelected(viewModel.OutputOptions, "Kanji");

            await viewModel.SaveAsync(TestContext.Current.CancellationToken);

            var saved = await environment.LearningPlanService.LoadAsync(
                LearningLanguage.Japanese,
                TestContext.Current.CancellationToken);
            Assert.Equal(35, saved.DailyNewTarget);
            Assert.Equal(12, saved.SessionWordLimit);
            Assert.Equal(LearningSelectionMode.Sequential, saved.SelectionMode);
            Assert.Equal(new[] { "Dictionary", "Polite", "Te" }, saved.FormCodes);
            Assert.Equal(new[] { LearningChannel.MeaningWriting }, saved.Channels);
            Assert.Equal(new[] { LearningOutputMode.Kanji }, saved.OutputModes);
            Assert.Contains("保存", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Null(viewModel.ErrorMessage);
        }

        [Fact]
        public async Task SearchExamplesAsync_UsesEnglishSourceLookupAndSavesSelectedParadigm()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal("do", viewModel.ExampleQuery);
            SetSelected(viewModel.FormOptions, "Base", "ThirdPersonSingular", "Past", "PastParticiple", "PresentParticiple");
            viewModel.ExampleQuery = "go";

            await viewModel.SearchExamplesAsync(TestContext.Current.CancellationToken);

            var go = Assert.Single(viewModel.ExampleCandidates, item => item.SourceSenseId == "go%2:01:00::");
            Assert.All(viewModel.ExampleCandidates, item =>
            {
                Assert.Equal("动词", item.PartOfSpeechLabel);
                Assert.False(item.IsPhrase);
            });
            viewModel.EnglishFormLabelMode = EnglishFormLabelMode.ExampleParadigm;
            viewModel.SelectedExample = go;
            await viewModel.SaveAsync(TestContext.Current.CancellationToken);

            var saved = await environment.LearningPlanService.LoadAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);
            Assert.Equal("go%2:01:00::", saved.EnglishExampleSourceSenseId);
            Assert.Equal(new[] { "go", "goes", "went", "gone", "going" },
                saved.EnglishExampleParadigm?.Forms.Select(form => form.Surface));
        }

        [Fact]
        public async Task DeleteSelectedAsync_CancelPreservesAndAcceptRemovesWholeGroup()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageAndDecideAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1", ImportDecision.Learning);
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.Japanese);
            await viewModel.RefreshAsync(TestContext.Current.CancellationToken);
            viewModel.LibraryItems[0].IsSelected = true;

            var canceled = viewModel.DeleteSelectedAsync(TestContext.Current.CancellationToken);
            await WaitForOverlayAsync(environment.Overlay);
            environment.Overlay.Cancel();
            await canceled;
            Assert.Single(viewModel.LibraryItems);

            var accepted = viewModel.DeleteSelectedAsync(TestContext.Current.CancellationToken);
            await WaitForOverlayAsync(environment.Overlay);
            environment.Overlay.Accept();
            await accepted;

            Assert.Empty(viewModel.LibraryItems);
            var page = await environment.LearningPlanService.GetLibraryPageAsync(
                LearningLanguage.Japanese,
                1,
                20,
                TestContext.Current.CancellationToken);
            Assert.Empty(page.Groups);
        }

        [Fact]
        public async Task RefreshAsync_PreCanceledTokenPropagatesWithoutChangingLanguage()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var viewModel = environment.CreateViewModel();
            viewModel.Initialize(LearningLanguage.English);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => viewModel.RefreshAsync(cancellation.Token));

            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Empty(viewModel.LibraryItems);
        }

        private static void SetSelected(
            IEnumerable<LearningPlanOptionViewModel> options,
            params string[] selectedCodes)
        {
            var selected = selectedCodes.ToHashSet(StringComparer.Ordinal);
            foreach (var option in options)
            {
                option.IsSelected = selected.Contains(option.Code);
            }
        }

        private static async Task WaitForOverlayAsync(OverlayService overlay)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!overlay.IsVisible && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1);
            }

            Assert.True(overlay.IsVisible, "等待学习库删除确认弹层超时。");
        }

        private sealed class TestEnvironment : IDisposable
        {
            private readonly TemporaryDirectory temporaryDirectory;

            private TestEnvironment(
                TemporaryDirectory temporaryDirectory,
                LearningPlanService learningPlanService,
                LexiconLookupService lexiconLookupService,
                ImportInboxService importInboxService)
            {
                this.temporaryDirectory = temporaryDirectory;
                LearningPlanService = learningPlanService;
                LexiconLookupService = lexiconLookupService;
                ImportInboxService = importInboxService;
                Navigation = new NavigationService(new TestScopeFactory());
                LearningNavigation = new RecordingLearningSessionNavigation();
                Overlay = new OverlayService();
            }

            public LearningPlanService LearningPlanService { get; }
            public LexiconLookupService LexiconLookupService { get; }
            public ImportInboxService ImportInboxService { get; }
            public NavigationService Navigation { get; }
            public RecordingLearningSessionNavigation LearningNavigation { get; }
            public OverlayService Overlay { get; }

            public static async Task<TestEnvironment> CreateAsync()
            {
                var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                paths.EnsureRuntimeDirectories();
                LexiconTestDatabases.CreateBoth(paths);
                await new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)).MigrateAsync(
                    TestContext.Current.CancellationToken);
                var lexiconFactory = new LexiconSqliteConnectionFactory(paths);
                var lexiconService = new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(lexiconFactory),
                    new EnglishLexiconDataAccessor(lexiconFactory));
                var importService = new ImportInboxService(
                    lexiconService,
                    DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths)));
                var planService = new LearningPlanService(
                    DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths)),
                    lexiconService,
                    TimeProvider.System);
                return new TestEnvironment(temporaryDirectory, planService, lexiconService, importService);
            }

            public LearningPlanViewModel CreateViewModel()
            {
                return new LearningPlanViewModel(
                    LearningPlanService,
                    LearningPlanService,
                    LearningPlanService,
                    LearningNavigation,
                    LexiconLookupService,
                    Navigation,
                    Overlay);
            }

            public async Task<string> StageAndDecideAsync(
                LearningLanguage language,
                string sourceText,
                string sourceSenseId,
                ImportDecision decision)
            {
                var staged = await ImportInboxService.StageAsync(
                    new StageImportBatchRequestDto(
                        language,
                        ImportSourceKind.Manual,
                        new[] { new ImportSelectionDto(sourceText, sourceSenseId) }),
                    TestContext.Current.CancellationToken);
                var item = Assert.Single(staged.Batch.Items);
                await ImportInboxService.DecideAsync(
                    new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, decision),
                    TestContext.Current.CancellationToken);
                return Assert.IsType<string>(item.WordGroupId);
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
                throw new NotSupportedException("该测试不执行页面导航。");
            }
        }

        private sealed class RecordingLearningSessionNavigation : ILearningSessionNavigation
        {
            public LearningLanguage? LastLanguage { get; private set; }
            public IReadOnlyList<string>? LastSelectedWordGroupIds { get; private set; }

            public Task OpenLearningSessionAsync(
                LearningLanguage language,
                IReadOnlyList<string>? selectedWordGroupIds,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastLanguage = language;
                LastSelectedWordGroupIds = selectedWordGroupIds;
                return Task.CompletedTask;
            }
        }
    }
}
