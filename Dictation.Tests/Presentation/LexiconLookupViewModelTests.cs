using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Services.Configuration;
using Dictation.Services.Importing;
using Dictation.Services.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    public sealed class LexiconLookupViewModelTests
    {
        [Fact]
        public void Initialize_OnlyAcceptsSupportedLanguageAndCannotRepeat()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));

            var undefinedLanguage = Assert.Throws<ArgumentOutOfRangeException>(() =>
                viewModel.Initialize((LearningLanguage)99));
            Assert.Contains("语言", undefinedLanguage.Message, StringComparison.Ordinal);

            viewModel.Initialize(LearningLanguage.English);
            var duplicateInitialization = Assert.Throws<InvalidOperationException>(() =>
                viewModel.Initialize(LearningLanguage.Japanese));
            Assert.Contains("初始化", duplicateInitialization.Message, StringComparison.Ordinal);
            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Equal("英语查词", viewModel.WindowTitle);
        }

        [Fact]
        public async Task SearchAsync_BeforeInitialize_ThrowsClearStateException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                viewModel.SearchAsync(TestContext.Current.CancellationToken));

            Assert.Contains("尚未初始化", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_JapaneseUsesFixedLanguageAndReturnsFoodEntry()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.Japanese);
            viewModel.Query = " 食べます ";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.Equal("日语查词", viewModel.WindowTitle);
            Assert.Equal(2, viewModel.Results.Count);
            Assert.All(viewModel.Results, result => Assert.Equal("食べる", result.Headword));
        }

        [Fact]
        public async Task SearchAsync_EnglishUsesFixedLanguageAndNormalizesWent()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = " WENT ";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Equal("英语查词", viewModel.WindowTitle);
            Assert.Equal(2, viewModel.Results.Count);
            Assert.All(viewModel.Results, result => Assert.Equal("go", result.Headword));
        }

        [Fact]
        public async Task SearchAsync_BlankQueryWithMissingDatabaseOnlyShowsPrompt()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = " \t\r\n ";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Empty(viewModel.Results);
            Assert.Null(viewModel.SelectedResult);
            Assert.False(viewModel.IsResultDropDownOpen);
            Assert.Null(viewModel.ErrorMessage);
            Assert.Contains("请输入", viewModel.EmptyStateText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_SuccessSelectsFirstResultAndOpensDropDown()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(viewModel.Results);
            Assert.Same(viewModel.Results[0], viewModel.SelectedResult);
            Assert.True(viewModel.IsResultDropDownOpen);
            Assert.Null(viewModel.ErrorMessage);
            Assert.Equal(string.Empty, viewModel.EmptyStateText);
        }

        [Fact]
        public async Task SearchAsync_NoResultsClearsSelectionAndShowsEmptyState()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            viewModel.Query = "not-in-dictionary";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Empty(viewModel.Results);
            Assert.Null(viewModel.SelectedResult);
            Assert.False(viewModel.IsResultDropDownOpen);
            Assert.Contains("未找到", viewModel.EmptyStateText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_MissingDatabaseShowsChineseErrorAndKeepsWindowUsable()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Contains("查词失败：", viewModel.ErrorMessage, StringComparison.Ordinal);
            Assert.Empty(viewModel.Results);
            Assert.Null(viewModel.SelectedResult);
            Assert.False(viewModel.IsResultDropDownOpen);
        }

        [Fact]
        public async Task SearchAsync_PreCanceledTokenPropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                viewModel.SearchAsync(cancellation.Token));
        }

        [Fact]
        public async Task SearchAsync_SecondQueryReplacesFirstResultSet()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            viewModel.Query = "take off";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            var result = Assert.Single(viewModel.Results);
            Assert.Equal("take off", result.Headword);
            Assert.Equal("take off%v", result.SourceLexemeId);
            Assert.Same(result, viewModel.SelectedResult);
            Assert.True(viewModel.IsResultDropDownOpen);
        }

        [Fact]
        public async Task StageSelectedAsync_WithoutSelectionDoesNotPersist()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);

            var result = await viewModel.StageSelectedAsync(TestContext.Current.CancellationToken);

            Assert.Null(result);
            Assert.False(viewModel.CanStageSelected);
            Assert.Contains("选择", viewModel.ImportErrorMessage, StringComparison.Ordinal);
            Assert.Empty(await CreateService(paths).GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task StageSelectedAsync_UsesSelectedResultAndLastSuccessfulQuery()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(viewModel.SelectedResult);
            var selected = viewModel.SelectedResult!;

            viewModel.Query = "not-the-last-search";
            var staged = await viewModel.StageSelectedAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(staged);
            var item = Assert.Single(staged!.Items);
            Assert.Equal("went", item.SourceText);
            Assert.Equal(selected.SourceSenseId, item.AnchorSourceSenseId);
            Assert.Equal(1, staged.AddedCount);
            Assert.Equal(0, staged.DuplicateCount);
            Assert.Contains("新增", viewModel.ImportStatusMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task StageSelectedAsync_DuplicateUsesDifferentInlineMessage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);
            await viewModel.StageSelectedAsync(TestContext.Current.CancellationToken);

            var duplicate = await viewModel.StageSelectedAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(duplicate);
            Assert.Equal(0, duplicate!.AddedCount);
            Assert.Equal(1, duplicate.DuplicateCount);
            Assert.Equal("词组已存在，未重复添加。", viewModel.ImportStatusMessage);
            Assert.DoesNotContain("已加入导入收件箱", viewModel.ImportStatusMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task StageSelectedAsync_DataErrorIsInlineAndCancellationPropagates()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);
            File.Delete(paths.UserDataDatabase);

            var errorResult = await viewModel.StageSelectedAsync(TestContext.Current.CancellationToken);

            Assert.Null(errorResult);
            Assert.Contains("加入导入收件箱失败：", viewModel.ImportErrorMessage, StringComparison.Ordinal);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                viewModel.StageSelectedAsync(cancellation.Token));
        }

        [Fact]
        public async Task SearchAsync_ClearsPreviousStageFeedback()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);
            await viewModel.StageSelectedAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(viewModel.ImportStatusMessage);

            viewModel.Query = "take off";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Null(viewModel.ImportStatusMessage);
            Assert.Null(viewModel.ImportErrorMessage);
        }

        [Fact]
        public void SelectedResult_PreservesDtoMeaningsWithoutChangingDto()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));
            viewModel.Initialize(LearningLanguage.English);
            var result = new LexiconLookupResultDto(
                LearningLanguage.English,
                "render%2:30:00::",
                "render%v",
                "render",
                null,
                "动词",
                "动词",
                false,
                new[] { "第一义项", "第二义项" },
                Array.Empty<LexiconLookupFormDto>());

            viewModel.SelectedResult = result;

            Assert.Same(result, viewModel.SelectedResult);
            Assert.Equal(new[] { "第一义项", "第二义项" }, viewModel.SelectedResult!.Meanings);
            Assert.Equal(new[] { "第一义项", "第二义项" }, result.Meanings);
        }

        [Fact]
        public void SelectedResult_WithoutMeaningsRemainsSelectable()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));
            viewModel.Initialize(LearningLanguage.Japanese);

            viewModel.SelectedResult = new LexiconLookupResultDto(
                LearningLanguage.Japanese,
                "missing",
                "missing",
                "かな",
                "かな",
                "名词",
                "无汉字",
                false,
                Array.Empty<string>(),
                Array.Empty<LexiconLookupFormDto>());

            Assert.NotNull(viewModel.SelectedResult);
            Assert.Empty(viewModel.SelectedResult!.Meanings);
        }

        [Fact]
        public void ScrollOffset_RaisesPropertyChangedAndRetainsValue()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

            viewModel.ScrollOffset = 128.5;

            Assert.Equal(128.5, viewModel.ScrollOffset);
            Assert.Contains(nameof(LexiconLookupViewModel.ScrollOffset), changedProperties);
        }

        [Fact]
        public async Task BackCommand_UsesCentralNavigationService()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var services = new ServiceCollection();
            services.AddSingleton<OverlayService>();
            services.AddSingleton<NavigationService>();
            services.AddScoped<LexiconLookupViewModel>(serviceProvider =>
                new LexiconLookupViewModel(
                    CreateLexiconService(paths),
                    CreateImportService(paths),
                    serviceProvider.GetRequiredService<NavigationService>(),
                    serviceProvider.GetRequiredService<OverlayService>()));
            using var provider = services.BuildServiceProvider();
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = new HomeViewModel(
                new SettingsService(BuildConfiguration(paths), paths),
                navigation);
            navigation.InitializeHome(home);
            await navigation.OpenLexiconLookupAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);
            navigation.CompleteTransition();
            var lookup = Assert.IsType<LexiconLookupViewModel>(navigation.CurrentPage);
            var returned = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigation.TransitionRequested += (_, args) => returned.TrySetResult(args.CurrentPage);

            lookup.BackCommand.Execute(null);
            var page = await returned.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Same(home, page);
            Assert.Same(home, navigation.CurrentPage);
            navigation.CompleteTransition();
        }

        private static LexiconLookupViewModel CreateViewModel(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            var lexiconService = new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
            var importService = new ImportInboxService(
                lexiconService,
                new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
            return new LexiconLookupViewModel(
                lexiconService,
                importService,
                new NavigationService(new TestScopeFactory()),
                new OverlayService());
        }

        private static LexiconLookupService CreateLexiconService(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            return new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
        }

        private static ImportInboxService CreateImportService(ApplicationPaths paths)
        {
            return new ImportInboxService(
                CreateLexiconService(paths),
                new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
        }

        private static IConfigurationRoot BuildConfiguration(ApplicationPaths paths)
        {
            return new ConfigurationBuilder()
                .SetBasePath(paths.Root)
                .AddJsonFile(source =>
                {
                    source.Path = Path.Combine("CONFIG", "settings.json");
                    source.Optional = true;
                    source.ReloadOnChange = false;
                })
                .Build();
        }

        private sealed class TestScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
            {
                throw new NotSupportedException("该测试不执行页面创建导航。");
            }
        }

        private static ImportInboxService CreateService(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            var lexiconService = new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
            return new ImportInboxService(
                lexiconService,
                new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
        }

        private static async Task<ApplicationPaths> CreateEnvironmentAsync(TemporaryDirectory temporaryDirectory)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths)).MigrateAsync(TestContext.Current.CancellationToken);
            return paths;
        }
    }
}
