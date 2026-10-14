using Dictation.Core.Configuration;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Dictation.Tests.Core
{
    /// <summary>验证MCP配置真实读写、默认关闭、非法值和取消，保护原主题配置。</summary>
    public sealed class McpSettingsTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task MissingAndSavedSettings_PreserveThemeAndReloadMcpOnly()
        {
            using var directory = new TemporaryDirectory();
            var paths = new ApplicationPaths(directory.Path);
            using var configuration = Configure(paths);
            var themes = new SettingsService(configuration, paths);
            await themes.SaveAsync(new(ThemeMode.Dark, LearningLanguage.English), Token);
            var originalTheme = await File.ReadAllBytesAsync(paths.SettingsFile, Token);
            var service = new McpSettingsService(configuration, paths);
            Assert.False(service.Load().Enabled);
            Assert.Equal(47821, service.Load().Port);
            await service.SaveAsync(new(true, 47822), Token);
            Assert.True(service.Load().Enabled);
            Assert.Equal(47822, service.Load().Port);
            Assert.Equal(originalTheme, await File.ReadAllBytesAsync(paths.SettingsFile, Token));
            using var rebuilt = Configure(paths);
            Assert.Equal(new McpSettingsDto(true, 47822), new McpSettingsService(rebuilt, paths).Load());
            await themes.SaveAsync(new(ThemeMode.Light, LearningLanguage.Japanese), Token);
            Assert.Equal(new McpSettingsDto(true, 47822), service.Load());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1023)]
        [InlineData(65536)]
        public async Task InvalidPort_DoesNotCreateConfiguration(int port)
        {
            using var directory = new TemporaryDirectory();
            var paths = new ApplicationPaths(directory.Path);
            using var configuration = Configure(paths);
            var service = new McpSettingsService(configuration, paths);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveAsync(new(true, port), Token));
            Assert.False(File.Exists(Path.Combine(paths.Config, "mcp.json")));
        }

        [Theory]
        [InlineData("Mcp:Enabled", "maybe")]
        [InlineData("Mcp:Port", "abc")]
        [InlineData("Mcp:Port", "0")]
        [InlineData("Mcp:Port", "65536")]
        public void InvalidPersistentValue_ReportsConfigurationError(string key, string value)
        {
            using var directory = new TemporaryDirectory();
            using var configuration = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
            var service = new McpSettingsService(configuration, new ApplicationPaths(directory.Path));
            Assert.Throws<SettingsConfigurationException>(() => service.Load());
        }

        [Fact]
        public async Task Cancellation_PreservesSavedFileAndDefaultPortBoundary()
        {
            using var directory = new TemporaryDirectory();
            var paths = new ApplicationPaths(directory.Path);
            using var configuration = Configure(paths);
            var service = new McpSettingsService(configuration, paths);
            foreach (var port in new[] { 1024, 65535 })
            {
                await service.SaveAsync(new(false, port), Token);
                Assert.Equal(port, service.Load().Port);
            }
            var file = Path.Combine(paths.Config, "mcp.json");
            var original = await File.ReadAllBytesAsync(file, Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(new(true, 47821), new CancellationToken(true)));
            Assert.Equal(original, await File.ReadAllBytesAsync(file, Token));
            Assert.Empty(Directory.GetFiles(paths.Config, "*.tmp"));
        }

        private static ConfigurationRoot Configure(ApplicationPaths paths) => (ConfigurationRoot)new ConfigurationBuilder().SetBasePath(paths.Root)
            .AddJsonFile("CONFIG/settings.json", optional: true).AddJsonFile("CONFIG/mcp.json", optional: true).Build();
    }
}
