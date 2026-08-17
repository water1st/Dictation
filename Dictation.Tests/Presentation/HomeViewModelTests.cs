using Dictation.DataAccess.Runtime;
using Dictation.Presentation.ViewModels;
using Dictation.Services.Configuration;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Dictation.Tests.Presentation
{
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

        private static HomeViewModel CreateViewModel(
            ApplicationPaths paths,
            bool ignoreConfigurationLoadErrors = false)
        {
            return new HomeViewModel(new SettingsService(
                BuildConfiguration(paths, ignoreConfigurationLoadErrors),
                paths));
        }

        private static SettingsService CreateSettingsService(ApplicationPaths paths)
        {
            return new SettingsService(BuildConfiguration(paths), paths);
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
    }
}
