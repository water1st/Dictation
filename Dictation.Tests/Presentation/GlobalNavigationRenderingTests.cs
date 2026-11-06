using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    /// <summary>验证真实页面只保留窗口返回入口，标题栏在主题与 DPI 变化后仍可操作。</summary>
    [Collection("DpiRendering")]
    public sealed class GlobalNavigationRenderingTests
    {
        [Theory]
        [InlineData(ResolvedTheme.Light, 1d, 860d)]
        [InlineData(ResolvedTheme.Dark, 1d, 860d)]
        [InlineData(ResolvedTheme.Light, 1.5d, 860d)]
        [InlineData(ResolvedTheme.Dark, 1.5d, 860d)]
        [InlineData(ResolvedTheme.Light, 2d, 860d)]
        [InlineData(ResolvedTheme.Dark, 2d, 860d)]
        [InlineData(ResolvedTheme.Light, 1d, 620d)]
        [InlineData(ResolvedTheme.Dark, 1d, 620d)]
        [InlineData(ResolvedTheme.Light, 1.5d, 620d)]
        [InlineData(ResolvedTheme.Dark, 1.5d, 620d)]
        [InlineData(ResolvedTheme.Light, 2d, 620d)]
        [InlineData(ResolvedTheme.Dark, 2d, 620d)]
        public Task BusinessPages_GlobalNavigation_RemainsVisibleWithoutDuplicateBack(ResolvedTheme theme, double scale, double width)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                var duplicates = new List<string>();
                foreach (var language in new[] { LearningLanguage.Japanese, LearningLanguage.English })
                {
                    foreach (var pageName in new[] { "Lookup", "ReviewPlan", "ReviewLibrary" })
                    {
                        using var directory = new TemporaryDirectory();
                        using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                        var nav = provider.GetRequiredService<NavigationService>();
                        var root = provider.GetRequiredService<LandingViewModel>();
                        nav.InitializeHome(root);
                        await root.SelectLanguageAsync(language, TestContext.Current.CancellationToken);
                        nav.CompleteTransition();
                        UserControl page;
                        switch (pageName)
                        {
                            case "Lookup":
                                await nav.OpenLexiconLookupAsync(language, TestContext.Current.CancellationToken);
                                page = new LexiconLookupPage();
                                break;
                            case "ReviewPlan":
                                await nav.OpenReviewPlanAsync(language, TestContext.Current.CancellationToken);
                                page = new ReviewPlanPage();
                                break;
                            default:
                                await nav.OpenReviewLibraryAsync(language, TestContext.Current.CancellationToken);
                                page = new ReviewLibraryPage();
                                break;
                        }
                        nav.CompleteTransition();
                        page.DataContext = nav.CurrentPage;
                        var window = provider.GetRequiredService<HomeWindow>();
                        window.Width = width;
                        window.ShowActivated = false;
                        ThemePalette.Apply(window.Resources, theme);
                        ((ContentControl)window.FindName("CurrentPagePresenter")).Content = page;
                        try
                        {
                            window.Show();
                            VisualTreeHelper.SetRootDpi(window, new DpiScale(scale, scale));
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                            window.UpdateLayout();
                            Export(window, $"{pageName}-{language}-{theme}-{scale:0.0}-w{width:0}", scale);
                            foreach (var button in LogicalChildren(page).OfType<Button>())
                            {
                                if (button.GetBindingExpression(Button.CommandProperty)?.ParentBinding.Path?.Path == "BackCommand")
                                    duplicates.Add($"{pageName}/{language}");
                            }
                            var toolbar = (Grid)((Button)window.FindName("SettingsButton")).Parent;
                            var buttons = toolbar.Children.OfType<Button>().Where(button => button.IsVisible).ToArray();
                            Assert.Equal(4, buttons.Length);
                            var previousRight = 0d;
                            foreach (var button in buttons)
                            {
                                var point = button.TranslatePoint(new Point(), window);
                                Assert.True(point.X >= previousRight);
                                Assert.True(point.X + button.ActualWidth <= window.ActualWidth);
                                Assert.True(button.ActualWidth >= 38 && button.ActualHeight >= 36);
                                Assert.True(button.IsEnabled);
                                Assert.True(button.Focusable && button.IsTabStop);
                                var hit = window.InputHitTest(button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), window));
                                Assert.True(ReferenceEquals(hit, button) || hit is DependencyObject child && button.IsAncestorOf(child));
                                previousRight = point.X + button.ActualWidth;
                            }
                            Assert.Equal(Visibility.Visible, ((Button)window.FindName("SettingsButton")).Visibility);
                            Assert.Equal(Visibility.Visible, ((Button)window.FindName("BackButton")).Visibility);
                            Assert.Contains(buttons, button => AutomationProperties.GetName(button) == "最小化");
                        }
                        finally { window.Close(); }
                    }
                }
                Assert.Empty(duplicates);
            });
        }

        private static void Export(HomeWindow window, string name, double scale)
        {
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output)) return;
            Directory.CreateDirectory(output);
            var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)(content.ActualWidth * scale), (int)(content.ActualHeight * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, $"navigation-{name}.png"));
            encoder.Save(stream);
        }

        private static IEnumerable<DependencyObject> LogicalChildren(DependencyObject root)
        {
            yield return root;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                foreach (var descendant in LogicalChildren(child)) yield return descendant;
            }
        }
    }
}
