using System.Text.Json;
using Dictation.DataAccess;
using Dictation.DataAccess.Configuration;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.DataAccess
{
    public sealed class SettingsDataAccessorTests
    {
        [Fact]
        public async Task LoadAsync_WhenSettingsFileIsMissing_ReturnsNull()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var accessor = new SettingsDataAccessor(paths);

            var result = await accessor.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Null(result);
        }

        [Fact]
        public async Task SaveThenLoad_PreservesThemeAndLastLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var accessor = new SettingsDataAccessor(paths);
            var expected = new SettingsPo
            {
                ThemeMode = "Dark",
                LastLanguage = "English"
            };

            await accessor.SaveAsync(expected, TestContext.Current.CancellationToken);
            var actual = await accessor.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal("English", actual?.LastLanguage);
            Assert.Equal("Dark", actual?.ThemeMode);
        }

        [Fact]
        public async Task LoadAsync_WhenJsonIsMalformed_ThrowsDataAccessExceptionWithJsonInnerException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            await File.WriteAllTextAsync(paths.SettingsFile, "{ malformed", TestContext.Current.CancellationToken);
            var accessor = new SettingsDataAccessor(paths);

            var exception = await Assert.ThrowsAsync<SettingsDataAccessException>(() => accessor.LoadAsync(
                TestContext.Current.CancellationToken));

            Assert.IsType<JsonException>(exception.InnerException);
        }

        [Fact]
        public async Task SaveAsync_WhenSettingsPathIsDirectory_CleansUpTemporaryFileAndThrows()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            Directory.CreateDirectory(paths.SettingsFile);
            var accessor = new SettingsDataAccessor(paths);

            await Assert.ThrowsAsync<SettingsDataAccessException>(() => accessor.SaveAsync(
                new SettingsPo
                {
                    ThemeMode = "Dark",
                    LastLanguage = "English"
                },
                TestContext.Current.CancellationToken));

            Assert.Empty(Directory.GetFiles(paths.Config, ".settings-*.tmp"));
        }

        [Fact]
        public async Task SaveAsync_RejectsNullSettings()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var accessor = new SettingsDataAccessor(paths);

            await Assert.ThrowsAsync<ArgumentNullException>(() => accessor.SaveAsync(
                null!,
                TestContext.Current.CancellationToken));
        }
    }
}
