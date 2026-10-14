using Dictation.Core.Configuration;
using Dictation.Tests.Infrastructure;
using Tomlyn;
using Tomlyn.Model;

namespace Dictation.Tests.Core
{
    /// <summary>验证合法TOML的不同表达方式仍只修改本应用项，并保留其它服务器。</summary>
    public sealed class CodexMcpSyntaxTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static Uri Endpoint => new("http://127.0.0.1:47821/mcp");

        [Theory]
        [InlineData("mcp_servers = { other = {url='https://example.test/mcp'}, dictation = {url='http://127.0.0.1:47821/mcp'} } # 保留尾注释\n")]
        [InlineData("[mcp_servers]\nother={url='https://example.test/mcp'}\ndictation={url='http://127.0.0.1:47821/mcp'} # 目标注释\n")]
        [InlineData("mcp_servers.other.url='https://example.test/mcp'\nmcp_servers.dictation.url='http://127.0.0.1:47821/mcp'\n")]
        public async Task MatchingInlineOrDottedServer_IsReadAndRemovedWithoutDeletingOtherServer(string original)
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            await File.WriteAllTextAsync(file, original, Token);
            var service = new CodexMcpRegistrationService();
            Assert.Equal(Endpoint, (await service.ReadAsync(file, Token)).Endpoint);
            Assert.False((await service.RegisterAsync(file, Endpoint, Token)).Changed);
            Assert.Equal(original, await File.ReadAllTextAsync(file, Token));
            Assert.True((await service.UnregisterAsync(file, Token)).Changed);
            var root = TomlSerializer.Deserialize<TomlTable>(await File.ReadAllTextAsync(file, Token))!;
            var servers = Assert.IsType<TomlTable>(root["mcp_servers"]);
            Assert.False(servers.ContainsKey("dictation"));
            Assert.Equal("https://example.test/mcp", Assert.IsType<TomlTable>(servers["other"])["url"]);
        }

        [Fact]
        public async Task InlineRoot_WithAnotherServerCanAddOurEntryWithoutReformattingOtherKeys()
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            const string original = "model='sample' # 全局注释\nmcp_servers = { other = {url='https://example.test/mcp'} } # 保留尾注释\n";
            await File.WriteAllTextAsync(file, original, Token);
            var service = new CodexMcpRegistrationService();
            Assert.True((await service.RegisterAsync(file, Endpoint, Token)).Changed);
            var text = await File.ReadAllTextAsync(file, Token);
            Assert.Contains("model='sample' # 全局注释", text, StringComparison.Ordinal);
            Assert.Contains("other = {url='https://example.test/mcp'}", text, StringComparison.Ordinal);
            Assert.Contains("# 保留尾注释", text, StringComparison.Ordinal);
            Assert.Equal(Endpoint, (await service.ReadAsync(file, Token)).Endpoint);
            await service.UnregisterAsync(file, Token);
            Assert.Contains("# 保留尾注释", await File.ReadAllTextAsync(file, Token), StringComparison.Ordinal);
        }

        [Fact]
        public async Task ServerBetweenOtherTables_UnregisterKeepsFollowingTableAndComments()
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "config.toml");
            const string original = "model='sample'\n[mcp_servers.dictation]\nurl='http://127.0.0.1:47821/mcp'\n# 另一个服务的说明\n[mcp_servers.other]\nurl='https://example.test/mcp'\n";
            await File.WriteAllTextAsync(file, original, Token);
            Assert.True((await new CodexMcpRegistrationService().UnregisterAsync(file, Token)).Changed);
            var text = await File.ReadAllTextAsync(file, Token);
            Assert.DoesNotContain("127.0.0.1", text, StringComparison.Ordinal);
            Assert.Contains("# 另一个服务的说明", text, StringComparison.Ordinal);
            Assert.Contains("[mcp_servers.other]\nurl='https://example.test/mcp'", text, StringComparison.Ordinal);
        }
    }
}
