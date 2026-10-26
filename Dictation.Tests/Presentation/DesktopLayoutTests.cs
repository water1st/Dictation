using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Dictation.Core.Configuration;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>在实际 WPF 视口内验证首页、工作页和设置宿主。</summary>
    public sealed class DesktopLayoutTests
    {
        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task 首页_固定视口_仅显示两个语言入口且无滚动(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
            var root = provider.GetRequiredService<LandingViewModel>();
            var page = new HomePage { DataContext = root };
            Arrange(page, theme);
            var buttons = Children<Button>(page).ToArray();
            Assert.Equal(2, buttons.Length);
            Assert.Contains(buttons, b => ReferenceEquals(b.Command, root.SelectJapaneseCommand));
            Assert.Contains(buttons, b => ReferenceEquals(b.Command, root.SelectEnglishCommand));
            Assert.Empty(Children<ScrollViewer>(page));
            Assert.Equal(2, Children<LanguageBadge>(page).Count());
        });

        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public Task 工作页_保留业务入口和进度_设置入口迁出(LearningLanguage language) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.SelectLanguageAsync(language, TestContext.Current.CancellationToken);
            var page = new LanguageWorkspacePage { DataContext = home };
            Arrange(page, ResolvedTheme.Dark);
            var buttons = Children<Button>(page).ToArray();
            Assert.Contains(buttons, b => ReferenceEquals(b.Command, home.OpenLearningCommand));
            Assert.Contains(buttons, b => ReferenceEquals(b.Command, home.OpenReviewSessionCommand));
            Assert.Contains(buttons, b => ReferenceEquals(b.Command, home.OpenReviewLibraryCommand));
            Assert.Contains(buttons, b => ReferenceEquals(b.Command, home.OpenWrongAnswersCommand));
            Assert.Contains(buttons, b => ReferenceEquals(b.Command, home.OpenLexiconLookupCommand));
            Assert.DoesNotContain(buttons, b => ReferenceEquals(b.Command, home.OpenTextImportCommand) || ReferenceEquals(b.Command, home.OpenLearningPlanCommand));
            Assert.Equal(home.LearningProgressLabel, Assert.IsType<TextBlock>(page.FindName("LearningProgressText")).Text);
            Assert.Equal(home.ReviewProgressLabel, Assert.IsType<TextBlock>(page.FindName("ReviewDailyProgress")).Text);
            var kana = Assert.Single(buttons, b => ReferenceEquals(b.Command, home.OpenKanaCommand));
            Assert.Equal(language == LearningLanguage.Japanese ? Visibility.Visible : Visibility.Collapsed, kana.Visibility);
        });

        [Fact]
        public Task 设置_分类切换_单一右侧滚动并显示明确目标语言() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
            using var scope = provider.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<SettingsViewModel>();
            Assert.Same(scope.ServiceProvider.GetRequiredService<McpSettingsViewModel>(), settings.Mcp);
            var page = new SettingsPage { DataContext = settings };
            Arrange(page, ResolvedTheme.Light);
            await settings.SelectSectionAsync(SettingsSection.Mcp, TestContext.Current.CancellationToken);
            Arrange(page, ResolvedTheme.Light);
            Assert.Single(Children<ScrollViewer>(page), s => s.TemplatedParent is not TextBox);
            Assert.Same(settings.Mcp, Assert.Single(Children<McpSettingsPage>(page)).DataContext);
            await settings.SelectSectionAsync(SettingsSection.Import, TestContext.Current.CancellationToken);
            await settings.SelectTargetLanguageAsync(LearningLanguage.English);
            Arrange(page, ResolvedTheme.Light);
            var import = Assert.Single(Children<Button>(page), b => ReferenceEquals(b.Command, settings.OpenTextImportCommand));
            Assert.Equal(Visibility.Visible, Assert.IsType<StackPanel>(import.Parent).Visibility);
            Assert.Contains(Children<TextBlock>(page), t => t.Text.Contains("目标语言"));
        });

        [Fact]
        public async Task 壳导航_转场禁用返回时_可见性仍由页面层次决定()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            var shell = provider.GetRequiredService<ShellViewModel>();
            var changes = new List<string?>();
            shell.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            nav.InitializeHome(root);
            Assert.False(shell.IsBackButtonVisible);
            Assert.True(shell.IsSettingsButtonVisible);
            await root.SelectLanguageAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.True(shell.IsBackButtonVisible);
            Assert.False(shell.CanGoBack);
            Assert.True(shell.IsSettingsButtonVisible);
            nav.CompleteTransition();
            await nav.OpenSettingsAsync(TestContext.Current.CancellationToken);
            Assert.True(shell.IsBackButtonVisible);
            Assert.False(shell.IsSettingsButtonVisible);
            Assert.Contains(nameof(ShellViewModel.IsBackButtonVisible), changes);
            Assert.Contains(nameof(ShellViewModel.IsSettingsButtonVisible), changes);
        }

        [Fact]
        public Task 窗口转场_旧页和入场页_阻止动画期间再次点击() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            var window = provider.GetRequiredService<HomeWindow>();
            try
            {
                await root.SelectLanguageAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
                Assert.False(Assert.IsType<ContentControl>(window.FindName("PreviousPagePresenter")).IsHitTestVisible);
                Assert.False(Assert.IsType<ContentControl>(window.FindName("CurrentPagePresenter")).IsHitTestVisible);
            }
            finally { window.Close(); }
        });

        private static void Arrange(FrameworkElement page, ResolvedTheme theme)
        {
            ThemePalette.Apply(page.Resources, theme);
            page.Measure(new Size(860, 560));
            page.Arrange(new Rect(0, 0, 860, 560));
            page.UpdateLayout();
            Export(page, theme);
        }

        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task 窗口布局_固定尺寸和主题_标题栏与根内容完整渲染(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
            var nav = provider.GetRequiredService<NavigationService>();
            var root = provider.GetRequiredService<LandingViewModel>();
            nav.InitializeHome(root);
            var window = provider.GetRequiredService<HomeWindow>();
            try
            {
                Assert.Equal(860, window.Width);
                Assert.Equal(620, window.Height);
                Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                Assert.Equal(new Thickness(0), WindowChrome.GetWindowChrome(window).ResizeBorderThickness);
                Assert.IsType<ContentControl>(window.FindName("CurrentPagePresenter")).Content = new HomePage { DataContext = root };
                var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                ThemePalette.Apply(window.Resources, theme);
                content.Measure(new Size(860, 620)); content.Arrange(new Rect(0, 0, 860, 620)); content.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Button>(window.FindName("BackButton")).Visibility);
                Assert.Equal(Visibility.Visible, Assert.IsType<Button>(window.FindName("SettingsButton")).Visibility);
                Assert.Contains(Children<Button>(content), b => AutomationProperties.GetName(b) == "最小化");
                Export(content, theme, "window");
            }
            finally { window.Close(); }
        });

        private static void Export(FrameworkElement page, ResolvedTheme theme, string? name = null)
        {
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output)) return;
            Directory.CreateDirectory(output);
            name ??= page.GetType().Name + (page.DataContext switch
            {
                HomeViewModel home => "-" + home.SelectedLanguage,
                SettingsViewModel settings => "-" + settings.SelectedSection,
                _ => string.Empty
            });
            var bitmap = new RenderTargetBitmap((int)page.ActualWidth, (int)page.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen())
                drawing.DrawRectangle((Brush)page.FindResource("WindowBackgroundBrush"), null, new Rect(0, 0, page.ActualWidth, page.ActualHeight));
            bitmap.Render(background);
            bitmap.Render(page);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, $"after-{name}-{theme}.png"));
            encoder.Save(stream);
        }

        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item) yield return item;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
    }
}
