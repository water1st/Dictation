using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.DataAccess.Learning;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证首页设置状态、语言主题切换及导航入口。
    /// </summary>
    public sealed class HomeViewModelTests
    {
        [Fact]
        public async Task InitializeAsync_WhenSettingsAreMissing_UsesSystemAndJapaneseDefaults()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var viewModel = CreateViewModel(paths);

            await viewModel.InitializeAsync(CancellationToken.None);

            Assert.Equal(ThemeMode.System, viewModel.SelectedThemeMode);
            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.Null(viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LearningProgress_TracksSelectedLanguageAndChangedDailyTarget()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var target = 20;
            var reader = new StubLearningProgressReader
            {
                ReadAsync = (language, _) => Task.FromResult(new Dictation.Core.Learning.LearningDailyProgressDto(
                    language,
                    language == LearningLanguage.Japanese ? 3 : 5,
                    language == LearningLanguage.Japanese ? 2 : 4,
                    target))
            };
            var viewModel = new HomeViewModel(
                CreateSettingsService(new ApplicationPaths(temporaryDirectory.Path)),
                new RejectingHomeNavigation(),
                reader,
                new StubLearningPlanReader(),
                new StubLearningSessionReader(),
                new StubReviewProgressReader(), new EmptyKanaProgressReader());
            var changes = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.Equal("学习 2/20", viewModel.LearningProgressLabel);
            await viewModel.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            Assert.Equal("学习 4/20", viewModel.LearningProgressLabel);
            target = 8;
            await viewModel.RefreshLearningProgressAsync(TestContext.Current.CancellationToken);
            Assert.Equal("学习 4/8", viewModel.LearningProgressLabel);
            Assert.Contains(nameof(HomeViewModel.LearningProgressLabel), changes);
        }

        [Fact]
        public async Task LearningProgress_WhenDataAccessFails_ShowsInlineErrorWithoutOldCount()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var reader = new StubLearningProgressReader();
            var viewModel = new HomeViewModel(
                CreateSettingsService(new ApplicationPaths(temporaryDirectory.Path)),
                new RejectingHomeNavigation(),
                reader,
                new StubLearningPlanReader(),
                new StubLearningSessionReader(),
                new StubReviewProgressReader(), new EmptyKanaProgressReader());
            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
            reader.ReadAsync = (_, _) => Task.FromException<Dictation.Core.Learning.LearningDailyProgressDto>(
                new LearningSessionDataAccessException("数据库不可用", new IOException("模拟故障")));

            await viewModel.RefreshLearningProgressAsync(TestContext.Current.CancellationToken);

            Assert.Equal("学习进度暂不可用", viewModel.LearningProgressLabel);
            Assert.Contains("读取学习进度失败", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task LearningProgress_WhenCancelled_PropagatesAndKeepsPreviousLabel()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var reader = new StubLearningProgressReader();
            var viewModel = new HomeViewModel(
                CreateSettingsService(new ApplicationPaths(temporaryDirectory.Path)),
                new RejectingHomeNavigation(),
                reader,
                new StubLearningPlanReader(),
                new StubLearningSessionReader(),
                new StubReviewProgressReader(), new EmptyKanaProgressReader());
            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
            reader.ReadAsync = async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("不应完成");
            };
            using var cancellation = new CancellationTokenSource();
            var refresh = viewModel.RefreshLearningProgressAsync(cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
            Assert.Equal("学习 0/20", viewModel.LearningProgressLabel);
            Assert.Null(viewModel.ErrorMessage);
        }

        [Fact]
        public async Task InitializeAsync_WhenSettingsExist_RestoresSavedState()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var settingsService = CreateSettingsService(paths);
            await settingsService.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                CancellationToken.None);
            var viewModel = CreateViewModel(paths);

            await viewModel.InitializeAsync(CancellationToken.None);

            Assert.Equal(ThemeMode.Dark, viewModel.SelectedThemeMode);
            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Null(viewModel.ErrorMessage);
        }

        [Fact]
        public async Task SelectingLanguageAndTheme_SavesImmediatelyAndRaisesPropertyChanged()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var viewModel = CreateViewModel(paths);
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
            await viewModel.InitializeAsync(CancellationToken.None);

            await viewModel.SelectLanguageAsync(LearningLanguage.English, CancellationToken.None);
            await viewModel.SelectThemeAsync(ThemeMode.Dark, CancellationToken.None);

            var persisted = CreateSettingsService(paths).Load();
            Assert.Equal(LearningLanguage.English, persisted.LastLanguage);
            Assert.Equal(ThemeMode.Dark, persisted.ThemeMode);
            Assert.Contains(nameof(HomeViewModel.SelectedLanguage), changedProperties);
            Assert.Contains(nameof(HomeViewModel.SelectedThemeMode), changedProperties);
        }

        [Fact]
        public async Task SelectingTheCurrentValue_DoesNotAttemptAnotherSave()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var viewModel = CreateViewModel(paths);
            await viewModel.InitializeAsync(CancellationToken.None);
            Directory.CreateDirectory(paths.SettingsFile);

            await viewModel.SelectThemeAsync(ThemeMode.System, CancellationToken.None);

            Assert.Null(viewModel.ErrorMessage);
            Assert.Equal(ThemeMode.System, viewModel.SelectedThemeMode);
        }

        [Fact]
        public async Task SelectingAnUndefinedLanguageOrTheme_ThrowsClearArgumentException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var viewModel = CreateViewModel(paths);
            await viewModel.InitializeAsync(CancellationToken.None);

            var languageException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                viewModel.SelectLanguageAsync((LearningLanguage)99, CancellationToken.None));
            var themeException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                viewModel.SelectThemeAsync((ThemeMode)99, CancellationToken.None));

            Assert.Contains("语言", languageException.Message);
            Assert.Contains("主题", themeException.Message);
        }

        [Fact]
        public async Task SelectingValue_WhenSaveFails_KeepsPreviousStateAndPublishesError()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var viewModel = CreateViewModel(paths);
            await viewModel.InitializeAsync(CancellationToken.None);
            Directory.CreateDirectory(paths.SettingsFile);

            await viewModel.SelectLanguageAsync(LearningLanguage.English, CancellationToken.None);

            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.NotNull(viewModel.ErrorMessage);
            Assert.Contains("保存", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task InitializeAsync_WhenSettingsContainUnknownValue_KeepsDefaultsAndPublishesError()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            File.WriteAllText(
                paths.SettingsFile,
                "{\"ThemeMode\":\"Neon\",\"LastLanguage\":\"English\"}");
            var viewModel = CreateViewModel(paths);

            await viewModel.InitializeAsync(CancellationToken.None);

            Assert.Equal(ThemeMode.System, viewModel.SelectedThemeMode);
            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.NotNull(viewModel.ErrorMessage);
            Assert.Contains("设置", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task InitializeAsync_WhenSettingsJsonIsMalformed_UsesDefaultsWithoutError()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            File.WriteAllText(paths.SettingsFile, "{ invalid json");
            var viewModel = CreateViewModel(paths, ignoreConfigurationLoadErrors: true);

            await viewModel.InitializeAsync(CancellationToken.None);

            Assert.Equal(ThemeMode.System, viewModel.SelectedThemeMode);
            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.Null(viewModel.ErrorMessage);
        }

        [Fact]
        public async Task OpenLexiconLookupCommand_UsesCurrentHomeLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            using var provider = CreateNavigationProvider(paths);
            var navigation = provider.GetRequiredService<NavigationService>();
            var viewModel = new HomeViewModel(
                CreateSettingsService(paths),
                navigation,
                new StubLearningProgressReader(),
                new StubLearningPlanReader(),
                new StubLearningSessionReader(),
                new StubReviewProgressReader(), new EmptyKanaProgressReader());
            navigation.InitializeHome(viewModel);
            await viewModel.SelectLanguageAsync(LearningLanguage.English, CancellationToken.None);
            var reachedPage = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            navigation.TransitionRequested += (_, args) => reachedPage.TrySetResult(args.CurrentPage);

            viewModel.OpenLexiconLookupCommand.Execute(null);
            var page = await reachedPage.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

            var lookupViewModel = Assert.IsType<LexiconLookupViewModel>(page);
            Assert.Equal(LearningLanguage.English, lookupViewModel.SelectedLanguage);
            navigation.CompleteTransition();
        }

        [Fact]
        public async Task OpenImportInboxCommand_UsesCurrentHomeLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            using var provider = CreateNavigationProvider(paths);
            var navigation = provider.GetRequiredService<NavigationService>();
            var viewModel = new HomeViewModel(
                CreateSettingsService(paths),
                navigation,
                new StubLearningProgressReader(),
                new StubLearningPlanReader(),
                new StubLearningSessionReader(),
                new StubReviewProgressReader(), new EmptyKanaProgressReader());
            navigation.InitializeHome(viewModel);
            var reachedPage = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            navigation.TransitionRequested += (_, args) => reachedPage.TrySetResult(args.CurrentPage);

            viewModel.OpenImportInboxCommand.Execute(null);
            var page = await reachedPage.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

            var inboxViewModel = Assert.IsType<ImportInboxListViewModel>(page);
            Assert.Equal(LearningLanguage.Japanese, inboxViewModel.SelectedLanguage);
            navigation.CompleteTransition();
        }

        private static HomeViewModel CreateViewModel(
            ApplicationPaths paths,
            bool ignoreConfigurationLoadErrors = false)
        {
            return new HomeViewModel(new SettingsService(
                BuildConfiguration(paths, ignoreConfigurationLoadErrors),
                paths),
                new RejectingHomeNavigation(),
                new StubLearningProgressReader(),
                new StubLearningPlanReader(),
                new StubLearningSessionReader(),
                new StubReviewProgressReader(), new EmptyKanaProgressReader());
        }

        private static SettingsService CreateSettingsService(ApplicationPaths paths)
        {
            return new SettingsService(BuildConfiguration(paths), paths);
        }

        private static ServiceProvider CreateNavigationProvider(ApplicationPaths paths)
        {
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths)).MigrateAsync().GetAwaiter().GetResult();
            var services = new ServiceCollection();
            services.AddSingleton<NavigationService>();
            services.AddSingleton<IBackNavigation>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<IImportInboxDetailNavigation>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<OverlayService>();
            services.AddSingleton<IOverlayRequester>(serviceProvider =>
                serviceProvider.GetRequiredService<OverlayService>());
            services.AddScoped<LexiconLookupService>(_ => CreateLexiconService(paths));
            services.AddScoped<ILexiconSearch>(serviceProvider =>
                serviceProvider.GetRequiredService<LexiconLookupService>());
            services.AddScoped<ImportInboxService>(serviceProvider =>
                new ImportInboxService(
                    serviceProvider.GetRequiredService<LexiconLookupService>(),
                    DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths))));
            services.AddScoped<IImportStaging>(serviceProvider =>
                serviceProvider.GetRequiredService<ImportInboxService>());
            services.AddScoped<IImportInboxQuery>(serviceProvider =>
                serviceProvider.GetRequiredService<ImportInboxService>());
            services.AddScoped<IImportInboxActions>(serviceProvider =>
                serviceProvider.GetRequiredService<ImportInboxService>());
            services.AddScoped<LexiconLookupViewModel>();
            services.AddScoped<ImportInboxListViewModel>();
            return services.BuildServiceProvider();
        }

        private static LexiconLookupService CreateLexiconService(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            return new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
        }

        private static IConfigurationRoot BuildConfiguration(
            ApplicationPaths paths,
            bool ignoreConfigurationLoadErrors = false)
        {
            return new ConfigurationBuilder()
                .SetBasePath(paths.Root)
                .AddJsonFile(source =>
                {
                    source.Path = Path.Combine("CONFIG", "settings.json");
                    source.Optional = true;
                    source.ReloadOnChange = false;
                    if (ignoreConfigurationLoadErrors)
                    {
                        source.OnLoadException = context => context.Ignore = true;
                    }
                })
                .Build();
        }

        private sealed class RejectingHomeNavigation : IHomeNavigation
        {
            public Task OpenKanaAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.FromException(new InvalidOperationException("此测试不执行五十音导航。"));
            public Task OpenWrongAnswersAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task OpenReviewSessionAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task OpenReviewPlanAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task OpenReviewLibraryAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task OpenTextImportAsync(LearningLanguage language, string? initialFilePath = null,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该设置测试不执行页面导航。"));
            }

            public Task OpenLexiconLookupAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该设置测试不执行页面导航。"));
            }

            public Task OpenImportInboxAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该设置测试不执行页面导航。"));
            }

            public Task OpenLearningPlanAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该设置测试不执行页面导航。"));
            }

            public Task OpenLearningAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该设置测试不执行学习导航。"));
            }
        }
    }
}
