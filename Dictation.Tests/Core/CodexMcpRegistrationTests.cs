using System.Text;
using Dictation.Core.Configuration;
using Dictation.Tests.Infrastructure;
using Tomlyn;
using Tomlyn.Model;

namespace Dictation.Tests.Core
{
    /// <summary>以真实临时TOML文件验证注册、冲突、取消和原子替换，不触碰真实客户端配置。</summary>
    public sealed class CodexMcpRegistrationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static Uri Endpoint => new("http://127.0.0.1:47821/mcp");

        [Fact]
        public async Task MissingFile_RegisterCreatesSingleHttpEntryAndRepeatedClickDoesNotRewrite()
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "client", "config.toml");
            var service = new CodexMcpRegistrationService();
            Assert.Null((await service.ReadAsync(file, Token)).Endpoint);
            Assert.False(File.Exists(file));
            var result = await service.RegisterAsync(file, Endpoint, Token);
            Assert.True(result.Changed);
            Assert.True(File.Exists(file));
            Assert.Equal(Endpoint, result.Endpoint);
            Assert.Equal("http://127.0.0.1:47821/mcp", Server(await File.ReadAllTextAsync(file, Token))["url"]);
            var bytes = await File.ReadAllBytesAsync(file, Token);
            Assert.False((await service.RegisterAsync(file, Endpoint, Token)).Changed);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(file, Token));
            Assert.Equal(Endpoint, (await service.ReadAsync(file, Token)).Endpoint);
            Assert.True((await service.UnregisterAsync(file, Token)).Changed);
            Assert.Null((await service.ReadAsync(file, Token)).Endpoint);
            Assert.False((await service.UnregisterAsync(file, Token)).Changed);
        }

        [Theory]
        [InlineData("# 顶部说明\r\nmodel = 'sample'\r\n[mcp_servers.other]\r\nurl = 'https://example.test/mcp' # 保留注释\r\n")]
        [InlineData("model = 'sample'\n[mcp_servers]\nother = {url = 'https://example.test/mcp'} # 内联表\n")]
        public async Task OtherServersAndComments_SurviveRegisterAndUnregister(string original)
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            await File.WriteAllTextAsync(file, original, Token);
            var service = new CodexMcpRegistrationService();
            await service.RegisterAsync(file, Endpoint, Token);
            var registered = await File.ReadAllTextAsync(file, Token);
            Assert.StartsWith(original, registered, StringComparison.Ordinal);
            Assert.Equal("http://127.0.0.1:47821/mcp", Server(registered)["url"]);
            await service.UnregisterAsync(file, Token);
            Assert.Equal(original, await File.ReadAllTextAsync(file, Token));
        }

        [Theory]
        [InlineData("[mcp_servers.dictation]\nurl = 'https://example.test/mcp'\n")]
        [InlineData("[mcp_servers.dictation]\ncommand = 'different-server'\n")]
        [InlineData("mcp_servers = {dictation = {url = 'https://example.test/mcp'}}\n")]
        [InlineData("[mcp_servers.dictation]\nurl = 'http://127.0.0.1:47822/mcp'\n")]
        public async Task ConflictingName_RegisterDoesNotOverwrite(string original)
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            await File.WriteAllTextAsync(file, original, Token);
            var service = new CodexMcpRegistrationService();
            await Assert.ThrowsAsync<SettingsConfigurationException>(() => service.RegisterAsync(file, Endpoint, Token));
            Assert.Equal(original, await File.ReadAllTextAsync(file, Token));
        }

        [Theory]
        [InlineData("[mcp_servers.dictation]\nurl = 'https://example.test/mcp'\n")]
        [InlineData("[mcp_servers.dictation]\ncommand = 'different-server'\n")]
        public async Task ForeignServer_UnregisterRefusesToRemoveIt(string original)
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            await File.WriteAllTextAsync(file, original, Token);
            await Assert.ThrowsAsync<SettingsConfigurationException>(() => new CodexMcpRegistrationService().UnregisterAsync(file, Token));
            Assert.Equal(original, await File.ReadAllTextAsync(file, Token));
        }

        [Theory]
        [InlineData("[bad")]
        [InlineData("model='a'\nmodel='b'\n")]
        public async Task MalformedToml_DoesNotOverwriteConfiguration(string original)
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            await File.WriteAllTextAsync(file, original, Token);
            var service = new CodexMcpRegistrationService();
            await Assert.ThrowsAsync<SettingsConfigurationException>(() => service.RegisterAsync(file, Endpoint, Token));
            await Assert.ThrowsAsync<SettingsConfigurationException>(() => service.UnregisterAsync(file, Token));
            Assert.Equal(original, await File.ReadAllTextAsync(file, Token));
        }

        [Theory]
        [InlineData("https://127.0.0.1:47821/mcp")]
        [InlineData("http://localhost:47821/mcp")]
        [InlineData("http://127.0.0.1:80/mcp")]
        [InlineData("http://127.0.0.1:47821/other")]
        [InlineData("http://user@127.0.0.1:47821/mcp")]
        [InlineData("http://127.0.0.1:47821/mcp?token=x")]
        public async Task InvalidEndpoint_IsRejectedWithoutCreatingFile(string endpoint)
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            await Assert.ThrowsAsync<ArgumentException>(() => new CodexMcpRegistrationService().RegisterAsync(file, new Uri(endpoint), Token));
            Assert.False(File.Exists(file));
        }

        [Fact]
        public async Task CanceledRegisterAndUnregister_PreserveOriginalBytes()
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            var original = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("# 保留BOM\nmodel='sample'\n")).ToArray();
            await File.WriteAllBytesAsync(file, original, Token);
            var canceled = new CancellationToken(true);
            var service = new CodexMcpRegistrationService();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RegisterAsync(file, Endpoint, canceled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UnregisterAsync(file, canceled));
            Assert.Equal(original, await File.ReadAllBytesAsync(file, Token));
            await service.RegisterAsync(file, Endpoint, Token);
            Assert.Equal(Encoding.UTF8.GetPreamble(), (await File.ReadAllBytesAsync(file, Token))[..3]);
            await service.UnregisterAsync(file, Token);
            Assert.Equal(original, await File.ReadAllBytesAsync(file, Token));
        }

        [Fact]
        public async Task ExternalEditAndFileCreation_AreNotOverwrittenByStaleSnapshot()
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            var original = Encoding.UTF8.GetBytes("model='old'\n");
            var external = Encoding.UTF8.GetBytes("model='external'\n");
            await File.WriteAllBytesAsync(file, external, Token);
            await Assert.ThrowsAsync<SettingsPersistenceException>(() => CodexConfigFileWriter.WriteAsync(file, original, Encoding.UTF8.GetBytes("model='replacement'\n"), Token));
            Assert.Equal(external, await File.ReadAllBytesAsync(file, Token));
            await Assert.ThrowsAsync<SettingsPersistenceException>(() => CodexConfigFileWriter.WriteAsync(file, null, original, Token));
            Assert.Equal(external, await File.ReadAllBytesAsync(file, Token));
            Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
        }

        [Fact]
        public async Task LockedConfiguration_ReportsPersistenceFailureWithoutChangingBytes()
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            var original = Encoding.UTF8.GetBytes("model='sample'\n");
            await File.WriteAllBytesAsync(file, original, Token);
            using var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            await Assert.ThrowsAsync<SettingsPersistenceException>(() => new CodexMcpRegistrationService().RegisterAsync(file, Endpoint, Token));
            Assert.Equal(original, await File.ReadAllBytesAsync(file, Token));
            Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
        }

        private static TomlTable Server(string text)
        {
            var root = TomlSerializer.Deserialize<TomlTable>(text)!;
            return Assert.IsType<TomlTable>(Assert.IsType<TomlTable>(root["mcp_servers"])["dictation"]);
        }
    }
}
