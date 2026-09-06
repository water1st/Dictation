using Dictation.DataAccess.Runtime;
using Dictation.Core.Configuration;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Dictation.Tests.Core
{
    public sealed class SettingsServiceTests
    {
        [Fact]
        public void Constructor_WhenConfigurationIsNull_RejectsArgument()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);

            Assert.Throws<ArgumentNullException>(() =>
                new SettingsService(null!, paths));
        }

        [Fact]
        public void Constructor_WhenApplicationPathsIsNull_RejectsArgument()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            Assert.Throws<ArgumentNullException>(() =>
                new SettingsService(configuration, null!));
        }

        [Fact]
        public void Load_WhenSettingsAreMissing_ReturnsSystemThemeAndJapaneseLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = CreateService(paths, out _);

            var result = service.Load();

            Assert.Equal(ThemeMode.System, result.ThemeMode);
            Assert.Equal(LearningLanguage.Japanese, result.LastLanguage);
        }

        [Fact]
        public async Task Load_WhenSettingsFileIsValid_RestoresSavedValues()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = CreateService(paths, out _);

            await service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                CancellationToken.None);
            var rebuiltService = new SettingsService(
                BuildConfiguration(paths),
                paths);

            var result = rebuiltService.Load();

            Assert.Equal(ThemeMode.Dark, result.ThemeMode);
            Assert.Equal(LearningLanguage.English, result.LastLanguage);
        }

        [Fact]
        public void Load_WhenOnlyOneSettingIsPresent_RejectsIncompleteConfiguration()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            File.WriteAllText(paths.SettingsFile, "{\"ThemeMode\":\"Dark\"}");
            var service = CreateService(paths, out _);

            Assert.Throws<SettingsConfigurationException>(() => service.Load());
        }

        [Fact]
        public void Load_WhenSettingContainsUnknownValue_RejectsConfiguration()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            File.WriteAllText(
                paths.SettingsFile,
                "{\"ThemeMode\":\"Neon\",\"LastLanguage\":\"English\"}");
            var service = CreateService(paths, out _);

            Assert.Throws<SettingsConfigurationException>(() => service.Load());
        }

        [Fact]
        public async Task SaveAsync_FirstSave_UpdatesCurrentConfigurationAfterReload()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var service = CreateService(paths, out var configuration);

            await service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                CancellationToken.None);

            Assert.Equal("Dark", configuration.GetValue<string>("ThemeMode"));
            Assert.Equal("English", configuration.GetValue<string>("LastLanguage"));
            Assert.Equal(ThemeMode.Dark, service.Load().ThemeMode);
            Assert.Equal(LearningLanguage.English, service.Load().LastLanguage);
        }

        [Fact]
        public async Task SaveAsync_ReloadsConfigurationExactlyOnce()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var countingConfiguration = new CountingConfigurationRoot(BuildConfiguration(paths));
            var service = new SettingsService(countingConfiguration, paths);

            await service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                CancellationToken.None);

            Assert.Equal(1, countingConfiguration.ReloadCount);
            Assert.Equal("Dark", countingConfiguration.GetValue<string>("ThemeMode"));
            Assert.Equal("English", countingConfiguration.GetValue<string>("LastLanguage"));
            Assert.Equal(ThemeMode.Dark, service.Load().ThemeMode);
            Assert.True(File.Exists(paths.SettingsFile));
        }

        [Fact]
        public async Task SaveAsync_OverwritesExistingFileAndRebuiltConfigurationCanReadIt()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = CreateService(paths, out _);

            await service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Light, LearningLanguage.Japanese),
                CancellationToken.None);
            await service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                CancellationToken.None);

            var rebuiltService = new SettingsService(
                BuildConfiguration(paths),
                paths);
            var result = rebuiltService.Load();

            Assert.Equal(ThemeMode.Dark, result.ThemeMode);
            Assert.Equal(LearningLanguage.English, result.LastLanguage);
        }

        [Fact]
        public async Task SaveAsync_WhenDtoIsNull_DoesNotWriteFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = CreateService(paths, out _);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveAsync(
                null!,
                CancellationToken.None));

            Assert.False(File.Exists(paths.SettingsFile));
        }

        [Fact]
        public async Task SaveAsync_WhenDtoContainsInvalidValue_DoesNotWriteOrUpdateConfiguration()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = CreateService(paths, out var configuration);

            await Assert.ThrowsAsync<SettingsConfigurationException>(() => service.SaveAsync(
                new ApplicationSettingsDto((ThemeMode)99, LearningLanguage.Japanese),
                CancellationToken.None));

            Assert.False(File.Exists(paths.SettingsFile));
            Assert.Null(configuration.GetValue<string>("ThemeMode"));
            Assert.Null(configuration.GetValue<string>("LastLanguage"));
        }

        [Fact]
        public async Task SaveAsync_WhenPreCanceled_DoesNotWriteOrUpdateConfiguration()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var service = CreateService(paths, out var configuration);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                cancellation.Token));

            Assert.False(File.Exists(paths.SettingsFile));
            Assert.Null(configuration.GetValue<string>("ThemeMode"));
            Assert.Null(configuration.GetValue<string>("LastLanguage"));
            Assert.Empty(Directory.GetFiles(paths.Config, ".settings-*.tmp"));
        }

        [Fact]
        public async Task SaveAsync_WhenSettingsPathIsDirectory_CleansTemporaryFileAndDoesNotUpdateConfiguration()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            Directory.CreateDirectory(paths.SettingsFile);
            var service = CreateService(paths, out var configuration);

            await Assert.ThrowsAsync<SettingsPersistenceException>(() => service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                CancellationToken.None));

            Assert.Null(configuration.GetValue<string>("ThemeMode"));
            Assert.Null(configuration.GetValue<string>("LastLanguage"));
            Assert.Empty(Directory.GetFiles(paths.Config, ".settings-*.tmp"));
        }

        [Fact]
        public async Task SaveAsync_WhenConfigurationIsNotRoot_RejectsBeforeWriting()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = new SettingsService(new NonRootConfiguration(), paths);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
                new ApplicationSettingsDto(ThemeMode.Dark, LearningLanguage.English),
                CancellationToken.None));

            Assert.False(Directory.Exists(paths.Config));
            Assert.False(File.Exists(paths.SettingsFile));
        }

        private static SettingsService CreateService(
            ApplicationPaths paths,
            out IConfiguration configuration)
        {
            configuration = BuildConfiguration(paths);
            return new SettingsService(configuration, paths);
        }

        private static IConfigurationRoot BuildConfiguration(ApplicationPaths paths)
        {
            return new ConfigurationBuilder()
                .SetBasePath(paths.Root)
                .AddJsonFile(Path.Combine("CONFIG", "settings.json"), optional: true, reloadOnChange: false)
                .Build();
        }

        private sealed class NonRootConfiguration : IConfiguration
        {
            private readonly IConfigurationRoot inner = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            public string? this[string key]
            {
                get => inner[key];
                set => inner[key] = value;
            }

            public IEnumerable<IConfigurationSection> GetChildren() => inner.GetChildren();

            public IChangeToken GetReloadToken() => inner.GetReloadToken();

            public IConfigurationSection GetSection(string key) => inner.GetSection(key);
        }

        private sealed class CountingConfigurationRoot : IConfigurationRoot
        {
            private readonly IConfigurationRoot inner;

            public CountingConfigurationRoot(IConfigurationRoot inner)
            {
                this.inner = inner;
            }

            public int ReloadCount { get; private set; }

            public IEnumerable<IConfigurationProvider> Providers => inner.Providers;

            public string? this[string key]
            {
                get => inner[key];
                set => inner[key] = value;
            }

            public IEnumerable<IConfigurationSection> GetChildren() => inner.GetChildren();

            public IChangeToken GetReloadToken() => inner.GetReloadToken();

            public IConfigurationSection GetSection(string key) => inner.GetSection(key);

            public void Reload()
            {
                ReloadCount++;
                inner.Reload();
            }
        }
    }
}
