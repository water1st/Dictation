using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Dictation.Presentation;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>用真实单窗口、绑定、DI和Core就绪通知验证非模态入口，不访问公网。</summary>
    public sealed class UpdateToolbarTests
    {
        [Fact]
        public Task Loaded_ShowsAvailableButtonWithoutDownloadAndWritesOwnedReadySignal() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var fixture = new ToolbarFixture();
            using var services = fixture.Build();
            var window = services.GetRequiredService<HomeWindow>();
            window.ShowActivated = false;
            try
            {
                var button = Assert.IsType<Button>(window.FindName("UpdateButton"));
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, button.Visibility);
                Assert.False(File.Exists(fixture.Ready));
                var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Loaded += (_, _) => loaded.TrySetResult();
                window.Show();
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.Contains("1.0.3", Assert.IsType<string>(button.Content));
                Assert.Same(window.Update.InstallCommand, button.Command);
                Assert.Equal(0, fixture.Preparation.Calls);
                Assert.True(File.Exists(fixture.Ready));
                Assert.False(File.Exists(Path.Combine(fixture.Root, "DATA", "UserData.db")));
            }
            finally { window.Close(); }
        });

        [Fact]
        public Task Click_DownloadFailureIsInlineAndKeepsOriginalWindowOpen() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var fixture = new ToolbarFixture();
            using var services = fixture.Build();
            var window = services.GetRequiredService<HomeWindow>();
            window.ShowActivated = false;
            try
            {
                var button = Assert.IsType<Button>(window.FindName("UpdateButton"));
                var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Loaded += (_, _) => loaded.TrySetResult();
                window.Show();
                await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Assert.True(button.Command.CanExecute(null));
                button.Command.Execute(null);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(1, fixture.Preparation.Calls);
                Assert.True(window.IsVisible);
                Assert.True(button.IsEnabled);
                Assert.NotEmpty(Assert.IsType<TextBlock>(window.FindName("UpdateStatusText")).Text);
            }
            finally { window.Close(); }
        });

        /// <summary>创建真实发行与事务，网络查询和下载替换为确定的能力边界。</summary>
        private sealed class ToolbarFixture : IDisposable
        {
            private readonly TemporaryDirectory directory = new();
            internal string Root => directory.Path;
            internal string Ready { get; }
            internal RejectingPreparation Preparation { get; } = new();
            internal ToolbarFixture()
            {
                var job = Path.Combine(Root, "CACHE", "Updates", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(job);
                Ready = Path.Combine(job, "startup.ready");
                var files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" };
                File.WriteAllText(Path.Combine(Root, "Dictation.Presentation.exe"), "new-main");
                File.WriteAllText(Path.Combine(Root, "package-manifest.json"), JsonSerializer.Serialize(new { version = "1.0.2", files }));
                File.WriteAllText(Path.Combine(job, "recovery.json"), JsonSerializer.Serialize(new
                {
                    RootDirectory = Root, PreviousVersion = "1.0.1", Version = "1.0.2",
                    PreviousFiles = files.Append("package-manifest.json").ToArray(), NewFiles = files.Append("package-manifest.json").ToArray()
                }));
            }
            internal ServiceProvider Build()
            {
                var collection = new ServiceCollection().AddDictationDataAccess(Root).AddDictationCore().AddDictationPresentation();
                collection.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    { ["DICTATION_UPDATE_READY_FILE"] = Ready }).Build());
                collection.AddSingleton<IUpdateReleaseQuery>(new Query());
                collection.AddSingleton<IUpdatePreparation>(Preparation);
                return collection.BuildServiceProvider();
            }
            public void Dispose() => directory.Dispose();
        }

        /// <summary>提供确定的新版本，不模拟UI、绑定或启动就绪文件。</summary>
        private sealed class Query : IUpdateReleaseQuery
        {
            public Task<AvailableUpdateDto?> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult<AvailableUpdateDto?>(new("1.0.3",
                new("https://github.com/water1st/Dictation/releases/download/v1.0.3/Dictation-win-x64.zip"),
                new("https://github.com/water1st/Dictation/releases/download/v1.0.3/Dictation-win-x64.zip.sha256")));
        }

        /// <summary>模拟下载边界失败，验证真实命令与页内错误绑定不会关闭窗口。</summary>
        private sealed class RejectingPreparation : IUpdatePreparation
        {
            internal int Calls { get; private set; }
            public Task<PreparedUpdateDto> PrepareAsync(AvailableUpdateDto update, CancellationToken cancellationToken = default)
            {
                Calls++;
                return Task.FromException<PreparedUpdateDto>(new InvalidDataException("校验失败"));
            }
        }
    }
}
