using Dictation.DataAccess.Configuration;
using Dictation.DataAccess.Runtime;
using Dictation.Services.Configuration;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Services
{
    public sealed class SettingsServiceTests
    {
        [Fact]
        public async Task LoadAsync_WhenSettingsAreMissing_ReturnsSystemThemeAndJapaneseLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var service = new SettingsService(new SettingsDataAccessor(paths));

            var result = await service.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ThemeMode.System, result.ThemeMode);
            Assert.Equal(LearningLanguage.Japanese, result.LastLanguage);
        }

        [Fact]
        public async Task SaveThenRecreateService_LoadsEnglishAndDarkFromFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var firstService = new SettingsService(new SettingsDataAccessor(paths));

            await firstService.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                TestContext.Current.CancellationToken);

            var secondService = new SettingsService(new SettingsDataAccessor(paths));
            var result = await secondService.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ThemeMode.Dark, result.ThemeMode);
            Assert.Equal(LearningLanguage.English, result.LastLanguage);
        }

        [Fact]
        public async Task SaveAsync_WhenDtoContainsInvalidThemeValue_RejectsWithoutWritingFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var service = new SettingsService(new SettingsDataAccessor(paths));

            await Assert.ThrowsAsync<SettingsConfigurationException>(() => service.SaveAsync(
                new ApplicationSettingsDto((ThemeMode)99, LearningLanguage.Japanese),
                TestContext.Current.CancellationToken));

            Assert.False(File.Exists(paths.SettingsFile));
        }

        [Fact]
        public async Task SaveAsync_WhenDtoContainsInvalidLanguageValue_RejectsWithoutWritingFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var service = new SettingsService(new SettingsDataAccessor(paths));

            await Assert.ThrowsAsync<SettingsConfigurationException>(() => service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.System, (LearningLanguage)99),
                TestContext.Current.CancellationToken));

            Assert.False(File.Exists(paths.SettingsFile));
        }
    }
}
