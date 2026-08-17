using Dictation.DataAccess.Configuration;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.ViewModels;
using Dictation.Services.Configuration;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Presentation
{
    public sealed class HomeViewModelTests
    {
        [Fact]
        public async Task InitializeAsync_WhenSettingsAreMissing_UsesSystemAndJapaneseDefaults()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var viewModel = CreateViewModel(paths);

            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ThemeMode.System, viewModel.SelectedThemeMode);
            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.Null(viewModel.ErrorMessage);
        }

        [Fact]
        public async Task InitializeAsync_WhenSettingsExist_RestoresSavedState()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var settingsService = new SettingsService(new SettingsDataAccessor(paths));
            await settingsService.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                TestContext.Current.CancellationToken);
            var viewModel = new HomeViewModel(settingsService);

            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ThemeMode.Dark, viewModel.SelectedThemeMode);
            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
        }

        [Fact]
        public async Task SelectingLanguageAndTheme_SavesImmediatelyAndRaisesPropertyChanged()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var viewModel = CreateViewModel(paths);
            var changedProperties = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

            await viewModel.SelectLanguageAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);
            await viewModel.SelectThemeAsync(
                ThemeMode.Dark,
                TestContext.Current.CancellationToken);

            var persisted = await new SettingsService(new SettingsDataAccessor(paths))
                .LoadAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(persisted);
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
            paths.EnsureRuntimeDirectories();
            var viewModel = CreateViewModel(paths);
            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
            File.Delete(paths.SettingsFile);
            Directory.CreateDirectory(paths.SettingsFile);

            await viewModel.SelectThemeAsync(
                ThemeMode.System,
                TestContext.Current.CancellationToken);

            Assert.Null(viewModel.ErrorMessage);
            Assert.Equal(ThemeMode.System, viewModel.SelectedThemeMode);
        }

        [Fact]
        public async Task SelectingAnUndefinedLanguageOrTheme_ThrowsClearArgumentException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var viewModel = CreateViewModel(paths);
            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

            var languageException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                viewModel.SelectLanguageAsync(
                    (LearningLanguage)99,
                    TestContext.Current.CancellationToken));
            var themeException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                viewModel.SelectThemeAsync(
                    (ThemeMode)99,
                    TestContext.Current.CancellationToken));

            Assert.Contains("语言", languageException.Message);
            Assert.Contains("主题", themeException.Message);
        }

        [Fact]
        public async Task SelectingValue_WhenSaveFails_KeepsPreviousStateAndPublishesError()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var viewModel = CreateViewModel(paths);
            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
            File.Delete(paths.SettingsFile);
            Directory.CreateDirectory(paths.SettingsFile);

            await viewModel.SelectLanguageAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.NotNull(viewModel.ErrorMessage);
            Assert.Contains("保存", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task InitializeAsync_WhenLoadFails_KeepsDefaultsAndPublishesError()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            await File.WriteAllTextAsync(paths.SettingsFile, "{ invalid json");
            var viewModel = CreateViewModel(paths);

            await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ThemeMode.System, viewModel.SelectedThemeMode);
            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.NotNull(viewModel.ErrorMessage);
            Assert.Contains("设置", viewModel.ErrorMessage);
        }

        private static HomeViewModel CreateViewModel(ApplicationPaths paths)
        {
            return new HomeViewModel(new SettingsService(new SettingsDataAccessor(paths)));
        }
    }
}
