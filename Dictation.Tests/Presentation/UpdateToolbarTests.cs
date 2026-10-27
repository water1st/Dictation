using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>用真实单窗口、绑定、DI和Core就绪通知验证非模态入口，不访问公网。</summary>
    public sealed class UpdateToolbarTests
    {
        [Fact]
        public Task ErrorLogEntry_WriteFails_RemainsFocusableWithFullErrorAndNoClickAction() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var fixture = new ToolbarFixture();
            File.WriteAllText(Path.Combine(fixture.Root, "LOGS"), "阻止日志目录创建");
            using var services = fixture.Build();
            var window = services.GetRequiredService<HomeWindow>();
            window.ShowActivated = false;
            try
            {
                window.Show();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                await window.Update.InstallAsync();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var button = Assert.IsType<Button>(window.FindName("UpdateErrorButton"));
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.True(button.IsEnabled);
                Assert.True(button.Focusable);
                Assert.True(button.IsTabStop);
                Assert.Null(window.Update.CurrentErrorLogPath);
                Assert.Contains("无法保存错误日志", window.Update.StatusMessage);
                Assert.Equal(window.Update.StatusMessage, AutomationProperties.GetHelpText(button));
                Assert.Equal(window.Update.StatusMessage, button.ToolTip);
                var originalMessage = window.Update.StatusMessage;
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(originalMessage, window.Update.StatusMessage);
            }
            finally { window.Close(); }
        });

        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task ErrorLogEntry_NoErrorOrMissingLog_DoesNotLaunchAndRetainsFeedback(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var fixture = new ToolbarFixture();
            using var services = fixture.Build();
            var window = services.GetRequiredService<HomeWindow>();
            window.ShowActivated = false;
            ThemePalette.Apply(window.Resources, theme);
            try
            {
                window.Show();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var button = Assert.IsType<Button>(window.FindName("UpdateErrorButton"));
                Assert.Equal(Visibility.Collapsed, button.Visibility);
                Assert.False(button.IsEnabled);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Empty(window.Update.StatusMessage);

                await window.Update.InstallAsync();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(button.IsEnabled);
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.True(button.Focusable);
                Assert.True(button.IsTabStop);
                Assert.Contains("日志", AutomationProperties.GetName(button));
                var path = Assert.IsType<string>(window.Update.CurrentErrorLogPath);
                Assert.True(File.Exists(path));
                File.Delete(path);
                var peer = new ButtonAutomationPeer(button);
                Assert.IsAssignableFrom<IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke)).Invoke();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(window.IsVisible);
                Assert.Contains("校验失败", window.Update.StatusMessage);
                Assert.Contains("无法打开错误日志", window.Update.StatusMessage);
                Assert.Equal(path, window.Update.CurrentErrorLogPath);

                await window.Update.CheckAsync();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, button.Visibility);
                Assert.False(button.IsEnabled);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void ErrorLogEntry_SpacesAndChinesePath_PassesSingleAbsolutePathToNotepad()
        {
            var path = Path.Combine(Path.GetTempPath(), "更新 诊断 & 参数", "错误 A.log");
            var info = HomeWindow.CreateErrorLogStartInfo(path);
            Assert.Equal("notepad.exe", Path.GetFileName(info.FileName));
            Assert.True(Path.IsPathFullyQualified(info.FileName));
            Assert.False(info.UseShellExecute);
            Assert.Empty(info.Arguments);
            Assert.Equal(new[] { path }, info.ArgumentList);
        }

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

        [Theory]
        [InlineData(ResolvedTheme.Light, "更新失败，网络连接不可用。")]
        [InlineData(ResolvedTheme.Dark, "更新失败，网络连接不可用。")]
        [InlineData(ResolvedTheme.Light, "HttpRequestException: TLS handshake failed at UpdatePreparation.PrepareAsync() ")]
        [InlineData(ResolvedTheme.Dark, "HttpRequestException: TLS handshake failed at UpdatePreparation.PrepareAsync() ")]
        [InlineData(ResolvedTheme.Light, "连接失败\r\n详细原因\n重试失败\r服务器不可用")]
        [InlineData(ResolvedTheme.Dark, "连接失败\r\n详细原因\n重试失败\r服务器不可用")]
        public Task UpdateError_FixedWindowLongText_OccupiesOnlyLeftOfButtonWithoutMovingControls(ResolvedTheme theme, string message) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var fixture = new ToolbarFixture();
            fixture.Preparation.Message = string.Concat(Enumerable.Repeat(message, 30));
            using var services = fixture.Build();
            services.GetRequiredService<NavigationService>().InitializeHome(services.GetRequiredService<LandingViewModel>());
            var window = services.GetRequiredService<HomeWindow>();
            window.ShowActivated = false;
            ThemePalette.Apply(window.Resources, theme);
            Assert.IsType<ContentControl>(window.FindName("CurrentPagePresenter")).Content =
                new HomePage { DataContext = services.GetRequiredService<LandingViewModel>() };
            try
            {
                window.Show();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var update = Assert.IsType<Button>(window.FindName("UpdateButton"));
                var error = Assert.IsType<TextBlock>(window.FindName("UpdateStatusText"));
                var toolbar = Assert.IsType<Grid>(Assert.IsType<Button>(window.FindName("SettingsButton")).Parent);
                var buttons = toolbar.Children.OfType<Button>().Where(button => Grid.GetColumn(button) >= 3).Prepend(update).ToArray();
                var before = buttons.Select(button => Bounds(button, window)).ToArray();

                await window.Update.InstallAsync();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                Export(window, theme, message.StartsWith("Http") ? "technical" : "chinese");
                Assert.Equal(Visibility.Visible, error.Visibility);
                Assert.True(Bounds(error, window).Right <= Bounds(update, window).Left);
                Assert.InRange(error.ActualWidth, 1, 130);
                Assert.InRange(error.ActualHeight, 1, 24);
                Assert.Equal(TextWrapping.NoWrap, error.TextWrapping);
                Assert.Equal(TextTrimming.CharacterEllipsis, error.TextTrimming);
                Assert.Equal(window.Update.StatusMessage, error.ToolTip);
                Assert.True(error.Focusable);
                for (var index = 0; index < buttons.Length; index++)
                {
                    var after = Bounds(buttons[index], window);
                    Assert.Equal(before[index].X, after.X, 6);
                    Assert.Equal(before[index].Y, after.Y, 6);
                    Assert.Equal(before[index].Width, after.Width, 6);
                    Assert.Equal(before[index].Height, after.Height, 6);
                    var center = buttons[index].TranslatePoint(new Point(buttons[index].ActualWidth / 2, buttons[index].ActualHeight / 2), window);
                    var hit = Assert.IsAssignableFrom<DependencyObject>(window.InputHitTest(center));
                    Assert.True(ReferenceEquals(hit, buttons[index]) || buttons[index].IsAncestorOf(hit));
                }

                var pending = new TaskCompletionSource<AvailableUpdateDto?>();
                fixture.Query.Pending = pending.Task;
                var checking = window.Update.CheckAsync();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, error.Visibility);
                Assert.Equal(new Size(0, 0), error.DesiredSize);
                pending.SetResult(null);
                await checking;
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(Visibility.Collapsed, error.Visibility);
                Assert.Equal(Visibility.Collapsed, update.Visibility);
            }
            finally { window.Close(); }
        });

        private static Rect Bounds(FrameworkElement element, Window window)
        {
            return new Rect(element.TranslatePoint(new Point(), window), element.RenderSize);
        }

        private static void Export(HomeWindow window, ResolvedTheme theme, string name)
        {
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output)) return;
            Directory.CreateDirectory(output);
            var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
            var bitmap = new RenderTargetBitmap(860, 620, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen())
                drawing.DrawRectangle((Brush)window.FindResource("WindowBackgroundBrush"), null, new Rect(0, 0, 860, 620));
            bitmap.Render(background);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, $"toolbar-{name}-{theme}.png"));
            encoder.Save(stream);
        }

        /// <summary>创建真实发行与事务，网络查询和下载替换为确定的能力边界。</summary>
        private sealed class ToolbarFixture : IDisposable
        {
            private readonly TemporaryDirectory directory = new();
            internal string Root => directory.Path;
            internal string Ready { get; }
            internal RejectingPreparation Preparation { get; } = new();
            internal Query Query { get; } = new();
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
                collection.AddSingleton<IUpdateReleaseQuery>(Query);
                collection.AddSingleton<IUpdatePreparation>(Preparation);
                return collection.BuildServiceProvider();
            }
            public void Dispose() => directory.Dispose();
        }

        /// <summary>提供确定的新版本，不模拟UI、绑定或启动就绪文件。</summary>
        private sealed class Query : IUpdateReleaseQuery
        {
            internal Task<AvailableUpdateDto?>? Pending { get; set; }
            public Task<AvailableUpdateDto?> CheckAsync(CancellationToken cancellationToken = default) => Pending ?? Task.FromResult<AvailableUpdateDto?>(new("1.0.3",
                new("https://github.com/water1st/Dictation/releases/download/v1.0.3/Dictation-win-x64.zip"),
                new("https://github.com/water1st/Dictation/releases/download/v1.0.3/Dictation-win-x64.zip.sha256")));
        }

        /// <summary>模拟下载边界失败，验证真实命令与页内错误绑定不会关闭窗口。</summary>
        private sealed class RejectingPreparation : IUpdatePreparation
        {
            internal int Calls { get; private set; }
            internal string Message { get; set; } = "校验失败";
            public Task<PreparedUpdateDto> PrepareAsync(AvailableUpdateDto update, CancellationToken cancellationToken = default)
            {
                Calls++;
                return Task.FromException<PreparedUpdateDto>(new InvalidDataException(Message));
            }
        }
    }
}
