using Dictation.Core.Configuration;
using Dictation.Core.Startup;
using Dictation.DataAccess;
using Dictation.Presentation;
using Dictation.Presentation.Mcp;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证坏配置进入真实设置页仍呈现Host失败状态，随后可以保存恢复。</summary>
    public sealed class McpSettingsRecoveryTests
    {
        [Theory]
        [InlineData("{")]
        [InlineData("{\"Mcp\":{\"Enabled\":\"bad\"}}")]
        [InlineData("{\"Mcp\":{\"Port\":1}}")]
        public Task BrokenSettings_RefreshShowsActualFailedStateAndSaveRepairs(string json) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            Directory.CreateDirectory(Path.Combine(directory.Path, "CONFIG"));
            await File.WriteAllTextAsync(Path.Combine(directory.Path, "CONFIG", "mcp.json"), json, TestContext.Current.CancellationToken);
            using var host = Host.CreateDefaultBuilder().ConfigureAppConfiguration(builder => builder.SetBasePath(directory.Path)
                .AddDictationMcpConfiguration()).ConfigureServices(services => services
                    .AddDictationDataAccess(directory.Path).AddDictationCore().AddDictationMcp()).Build();
            await host.Services.GetRequiredService<StartupInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
            await host.StartAsync(TestContext.Current.CancellationToken);
            using var vm = new McpSettingsViewModel(host.Services.GetRequiredService<McpSettingsService>(),
                host.Services.GetRequiredService<IMcpHostControl>(), host.Services.GetRequiredService<IMcpStatusReader>(),
                host.Services.GetRequiredService<CodexMcpRegistrationService>(), Path.Combine(directory.Path, "client", "config.toml"));
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Contains("失败", vm.RuntimeStatusLabel);
            Assert.NotNull(vm.ErrorMessage);
            vm.Enabled = false;
            vm.PortText = "47821";
            await vm.SaveAsync(TestContext.Current.CancellationToken);
            Assert.Null(vm.ErrorMessage);
            Assert.Contains("关闭", vm.RuntimeStatusLabel);
            Assert.False(File.Exists(vm.ConfigPath));
            await host.StopAsync(TestContext.Current.CancellationToken);
        });
    }
}
