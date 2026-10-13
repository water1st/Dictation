using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Startup;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Mcp;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Dictation.Tests.Presentation
{
    /// <summary>通过真实STA绑定、Host、SDK和SQLite验证AI设置及原收件箱确认链路。</summary>
    public sealed class McpSettingsPageTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1023")]
        [InlineData("65536")]
        [InlineData("999999999999")]
        public Task InvalidPortText_ShowsErrorWithoutSavingOldValue(string input) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            await using var fixture = await Fixture.CreateAsync();
            using var vm = fixture.CreateViewModel();
            await vm.RefreshAsync(Token);
            var page = new McpSettingsPage { DataContext = vm, Width = 800, Height = 600 };
            page.Measure(new Size(800, 600)); page.Arrange(new Rect(0, 0, 800, 600)); page.UpdateLayout();
            var textbox = Assert.IsType<TextBox>(page.FindName("PortInput"));
            textbox.SetCurrentValue(TextBox.TextProperty, input);
            textbox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.Equal(input, vm.PortText);
            await vm.SaveAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.False(File.Exists(Path.Combine(fixture.Paths.Config, "mcp.json")));
            Assert.Equal(McpRunState.Disabled, fixture.Status.GetStatus().State);
        });

        [Fact]
        public Task RefreshAndSave_NeverRegisterUntilExplicitClick() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            await using var fixture = await Fixture.CreateAsync();
            using var vm = fixture.CreateViewModel();
            vm.Enabled = true;
            await vm.RefreshAsync(Token);
            Assert.False(vm.Enabled);
            Assert.Contains("关闭", vm.RuntimeStatusLabel);
            Assert.False(File.Exists(fixture.ConfigPath));
            vm.Enabled = true;
            vm.PortText = fixture.Port.ToString();
            await vm.SaveAsync(Token);
            Assert.Null(vm.ErrorMessage);
            Assert.True(fixture.Settings.Load().Enabled);
            Assert.Equal(McpRunState.Running, fixture.Status.GetStatus().State);
            Assert.Contains("运行", vm.RuntimeStatusLabel);
            Assert.Contains("已保存", vm.StatusMessage);
            Assert.False(File.Exists(fixture.ConfigPath));
            await vm.RegisterAsync(Token);
            Assert.Null(vm.ErrorMessage);
            var original = await File.ReadAllTextAsync(fixture.ConfigPath, Token);
            Assert.Contains($"127.0.0.1:{fixture.Port}/mcp", original);
            vm.PortText = Fixture.FreePort().ToString();
            await vm.SaveAsync(Token);
            Assert.Contains("需要更新", vm.RegistrationMessage);
            Assert.Equal(original, await File.ReadAllTextAsync(fixture.ConfigPath, Token));
            await vm.RegisterAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal(original, await File.ReadAllTextAsync(fixture.ConfigPath, Token));
            await vm.UnregisterAsync(Token);
            await vm.RegisterAsync(Token);
            Assert.Null(vm.ErrorMessage);
            Assert.Contains(vm.EndpointText, await File.ReadAllTextAsync(fixture.ConfigPath, Token));
            vm.Enabled = false;
            await vm.SaveAsync(Token);
            Assert.Equal(McpRunState.Disabled, fixture.Status.GetStatus().State);
        });

        [Fact]
        public Task Conflict_ShowsSavedButFailedAndRetriesSavedPort() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            await using var fixture = await Fixture.CreateAsync();
            using var vm = fixture.CreateViewModel();
            await vm.RefreshAsync(Token);
            using var owner = new TcpListener(IPAddress.Loopback, fixture.Port);
            owner.Start();
            vm.Enabled = true;
            vm.PortText = fixture.Port.ToString();
            await vm.SaveAsync(Token);
            Assert.True(fixture.Settings.Load().Enabled);
            Assert.Equal(McpRunState.Failed, fixture.Status.GetStatus().State);
            Assert.Contains("已保存", vm.StatusMessage);
            Assert.Contains("失败", vm.RuntimeStatusLabel);
            Assert.NotNull(vm.ErrorMessage);
            owner.Stop();
            vm.PortText = "not-saved";
            await vm.RetryAsync(Token);
            Assert.Equal(McpRunState.Running, fixture.Status.GetStatus().State);
            Assert.Null(vm.ErrorMessage);
            Assert.Contains(fixture.Port.ToString(), vm.EndpointText);
        });

        [Fact]
        public Task Page_BindsCommandsAndBothThemesWithoutCreatingWindow() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            await using var fixture = await Fixture.CreateAsync();
            using var vm = fixture.CreateViewModel();
            await vm.RefreshAsync(Token);
            var page = new McpSettingsPage { DataContext = vm, Width = 800, Height = 600 };
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(page.Resources, theme);
                page.Measure(new Size(800, 600)); page.Arrange(new Rect(0, 0, 800, 600)); page.UpdateLayout();
                Assert.Same(vm.SaveCommand, Assert.IsType<Button>(page.FindName("SaveButton")).Command);
                Assert.Same(vm.RetryCommand, Assert.IsType<Button>(page.FindName("RetryButton")).Command);
                Assert.Same(vm.RegisterCommand, Assert.IsType<Button>(page.FindName("RegisterButton")).Command);
                Assert.Same(vm.UnregisterCommand, Assert.IsType<Button>(page.FindName("UnregisterButton")).Command);
                Assert.True(Assert.IsType<TextBox>(page.FindName("EndpointOutput")).IsReadOnly);
                Assert.Same(page.Resources["CardBackgroundBrush"], Assert.IsType<Border>(page.FindName("SettingsCard")).Background);
                Assert.Same(page.Resources["ForegroundBrush"], Assert.IsType<Button>(page.FindName("SaveButton")).Foreground);
                Assert.Equal(vm.RuntimeStatusLabel, Assert.IsType<TextBlock>(page.FindName("RuntimeStatus")).Text);
            }
        });

        [Fact]
        public Task Home_NavigatesAndReturnsInSameHostWithDisposedOldPage() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            await using var fixture = await Fixture.CreateAsync();
            var provider = fixture.Host.Services;
            var navigation = provider.GetRequiredService<NavigationService>();
            Assert.Same(navigation, provider.GetRequiredService<IMcpSettingsNavigation>());
            var home = provider.GetRequiredService<HomeViewModel>();
            navigation.InitializeHome(home);
            var shell = provider.GetRequiredService<ShellViewModel>();
            var window = provider.GetRequiredService<HomeWindow>();
            var homePage = new HomePage { DataContext = home };
            window.Content = homePage;
            homePage.Measure(new Size(1000, 1200)); homePage.Arrange(new Rect(0, 0, 1000, 1200)); homePage.UpdateLayout();
            Assert.Same(shell.OpenMcpSettingsCommand, Assert.IsType<Button>(homePage.FindName("OpenMcpSettingsButton")).Command);
            await navigation.OpenMcpSettingsAsync(Token);
            var vm = Assert.IsType<McpSettingsViewModel>(navigation.CurrentPage);
            Assert.Equal("AI导入设置", navigation.PageTitle);
            Assert.Equal(NavigationDirection.Forward, navigation.LastDirection);
            navigation.CompleteTransition();
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal(NavigationDirection.Backward, navigation.LastDirection);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => vm.RefreshAsync(Token));
            window.Close();
        });

        [Fact]
        public Task LeavingPage_IgnoresLateControlCompletion() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            await using var fixture = await Fixture.CreateAsync();
            var lateControl = new DelayedControl();
            using var vm = fixture.CreateViewModel(lateControl);
            await vm.RefreshAsync(Token);
            vm.Enabled = true;
            vm.PortText = fixture.Port.ToString();
            var saving = vm.SaveAsync(Token);
            await lateControl.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), Token);
            Assert.True(vm.IsBusy);
            vm.CancelOperation();
            var message = vm.StatusMessage;
            vm.Dispose();
            lateControl.Completed.SetResult();
            await saving;
            Assert.Equal(message, vm.StatusMessage);
            Assert.Null(vm.ErrorMessage);
        });

        [Theory]
        [InlineData(LearningLanguage.Japanese, "食べます", "jmdict:1:1", ImportDecision.Learning, "吃")]
        [InlineData(LearningLanguage.English, "went", "go%2:01:00::", ImportDecision.Review, "去")]
        public Task SdkStage_RealInboxListAndDetailConfirmWholeGroup(LearningLanguage language, string query, string sense,
            ImportDecision decision, string meaning) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            await using var fixture = await Fixture.CreateAsync();
            await fixture.Control.ApplyAsync(new(true, fixture.Port), Token);
            await using var transport = new HttpClientTransport(new() { Endpoint = fixture.Status.GetStatus().Endpoint });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: Token);
            var staged = await client.CallToolAsync("dictation_import_stage", new Dictionary<string, object?>
            {
                ["language"] = language.ToString(), ["selections"] = new[] { new ImportSelectionDto(query, sense) }
            }, cancellationToken: Token);
            Assert.NotEqual(true, staged.IsError);
            using var result = JsonDocument.Parse(Assert.Single(staged.Content.OfType<TextContentBlock>()).Text);
            var batchId = result.RootElement.GetProperty("batch").GetProperty("batchId").GetString()!;
            using var scope = fixture.Host.Services.CreateScope();
            var list = scope.ServiceProvider.GetRequiredService<ImportInboxListViewModel>();
            list.Initialize(language);
            await list.RefreshAsync(Token);
            list.SelectedBatch = Assert.Single(list.Batches);
            list.IsAllSelected = true;
            var batch = list.SelectedBatch;
            var item = Assert.Single(batch.Items);
            var detail = scope.ServiceProvider.GetRequiredService<ImportInboxDetailViewModel>();
            detail.Initialize(batch, item.ItemId);
            Assert.All(item.WordGroup!.Members, member => Assert.NotEmpty(member.Meanings));
            Assert.Contains(item.WordGroup.Members.SelectMany(member => member.Meanings), text => text.Contains(meaning));
            Assert.Equal(item.WordGroup.Members.Count, detail.Members.Count);
            var page = new ImportInboxDetailPage { DataContext = detail };
            page.Measure(new Size(900, 700)); page.Arrange(new Rect(0, 0, 900, 700)); page.UpdateLayout();
            await list.DecideAsync(decision, Token);
            Assert.Null(list.ErrorMessage);
            Assert.Empty(list.Batches);
            var complete = await fixture.Host.Services.GetRequiredService<IImportBatchReader>().GetBatchAsync(batchId, Token);
            Assert.Equal(ImportBatchStatus.Completed, complete.Status);
            await using var connection = fixture.Host.Services.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            Assert.Equal(decision.ToString(), await connection.ExecuteScalarAsync<string>("SELECT Placement FROM WordGroups WHERE WordGroupId=@id;", new { id = item.WordGroupId }));
        });

        /// <summary>模拟外部控制器迟到完成，只隔离取消边界，不替换业务服务。</summary>
        private sealed class DelayedControl : IMcpHostControl
        {
            internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public async Task ApplyAsync(McpSettingsDto settings, CancellationToken cancellationToken = default)
            {
                Started.TrySetResult();
                await Completed.Task;
            }
        }

        /// <summary>持有唯一真实测试容器和临时配置，不接触实际Codex配置或模型。</summary>
        private sealed class Fixture(TemporaryDirectory directory, IHost host, int port) : IAsyncDisposable
        {
            internal IHost Host => host;
            internal ApplicationPaths Paths => host.Services.GetRequiredService<ApplicationPaths>();
            internal string ConfigPath => Path.Combine(directory.Path, "client", "config.toml");
            internal int Port => port;
            internal McpSettingsService Settings => host.Services.GetRequiredService<McpSettingsService>();
            internal IMcpHostControl Control => host.Services.GetRequiredService<IMcpHostControl>();
            internal IMcpStatusReader Status => host.Services.GetRequiredService<IMcpStatusReader>();
            internal McpSettingsViewModel CreateViewModel(IMcpHostControl? control = null) => new(Settings, control ?? Control,
                Status, host.Services.GetRequiredService<CodexMcpRegistrationService>(), ConfigPath);
            internal static int FreePort()
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            internal static async Task<Fixture> CreateAsync()
            {
                var directory = new TemporaryDirectory();
                LexiconTestDatabases.CreateBoth(new ApplicationPaths(directory.Path));
                var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().ConfigureAppConfiguration(builder =>
                    builder.SetBasePath(directory.Path).AddDictationMcpConfiguration()).ConfigureServices(services =>
                {
                    services.AddDictationDataAccess(directory.Path).AddDictationCore().AddDictationMcp().AddDictationPresentation();
                    services.AddScoped<McpSettingsViewModel>(provider => new(provider.GetRequiredService<McpSettingsService>(),
                        provider.GetRequiredService<IMcpHostControl>(), provider.GetRequiredService<IMcpStatusReader>(),
                        provider.GetRequiredService<CodexMcpRegistrationService>(), Path.Combine(directory.Path, "client", "config.toml")));
                }).Build();
                await host.Services.GetRequiredService<StartupInitializer>().InitializeAsync(Token);
                await host.StartAsync(Token);
                return new(directory, host, FreePort());
            }
            public async ValueTask DisposeAsync()
            {
                await host.StopAsync(CancellationToken.None);
                if (host is IAsyncDisposable disposable) await disposable.DisposeAsync(); else host.Dispose();
                directory.Dispose();
            }
        }
    }
}
