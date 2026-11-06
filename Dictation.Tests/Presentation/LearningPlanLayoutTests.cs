using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Presentation;
using Dictation.Presentation.Controls;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证学习计划在真实窗口中的响应排版、单滚动和分页交互。</summary>
    [Collection("DpiRendering")]
    public sealed class LearningPlanLayoutTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(ResolvedTheme.Light, 1d, 1024d)]
        [InlineData(ResolvedTheme.Dark, 1d, 1024d)]
        [InlineData(ResolvedTheme.Light, 1.5d, 1024d)]
        [InlineData(ResolvedTheme.Dark, 1.5d, 1024d)]
        [InlineData(ResolvedTheme.Light, 2d, 1024d)]
        [InlineData(ResolvedTheme.Dark, 2d, 1024d)]
        [InlineData(ResolvedTheme.Light, 1d, 620d)]
        [InlineData(ResolvedTheme.Dark, 1d, 620d)]
        [InlineData(ResolvedTheme.Light, 1.5d, 620d)]
        [InlineData(ResolvedTheme.Dark, 1.5d, 620d)]
        [InlineData(ResolvedTheme.Light, 2d, 620d)]
        [InlineData(ResolvedTheme.Dark, 2d, 620d)]
        public Task Page_ResponsiveCardsSingleScrollAndEmptyActionsRemainUsable(ResolvedTheme theme, double scale, double width)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                foreach (var language in new[] { LearningLanguage.Japanese, LearningLanguage.English })
                {
                    using var directory = new TemporaryDirectory();
                    using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                    var viewModel = provider.GetRequiredService<LearningPlanViewModel>();
                    viewModel.Initialize(language);
                    await viewModel.RefreshAsync(Token);
                    var page = new LearningPlanPage { DataContext = viewModel };
                    var window = provider.GetRequiredService<HomeWindow>();
                    ApplyResources(window);
                    window.Width = width;
                    window.Height = 760;
                    window.ShowActivated = false;
                    ThemePalette.Apply(window.Resources, theme);
                    ((ContentControl)window.FindName("CurrentPagePresenter")).Content = page;
                    try
                    {
                        window.Show();
                        VisualTreeHelper.SetRootDpi(window, new DpiScale(scale, scale));
                        await SettleAsync();
                        Export(window, $"{language}-{theme}-{scale:0.0}-w{width:0}-top", scale);
                        var rhythm = Descendants<TextBlock>(page).Single(item => item.Text == "今天的节奏");
                        var practice = Descendants<TextBlock>(page).Single(item => item.Text == "练习内容");
                        var first = rhythm.TranslatePoint(new Point(), page);
                        var second = practice.TranslatePoint(new Point(), page);
                        if (width >= 1000)
                        {
                            Assert.InRange(Math.Abs(first.Y - second.Y), 0, 1);
                            Assert.True(second.X > first.X + 200);
                        }
                        else
                        {
                            Assert.True(second.Y > first.Y + 100);
                            Assert.InRange(Math.Abs(first.X - second.X), 0, 1);
                        }
                        var scroll = Assert.Single(Descendants<ScrollViewer>(page), PageScrollHost.GetIsPageScrollViewer);
                        scroll.ScrollToBottom();
                        await SettleAsync();
                        Export(window, $"{language}-{theme}-{scale:0.0}-w{width:0}-bottom", scale);
                        Assert.Contains(Descendants<TextBlock>(page), item => item.IsVisible && item.Text.Contains("学习库暂无词条", StringComparison.Ordinal));
                        Assert.False(Button(page, "开始本轮").IsEnabled);
                        Assert.False(Button(page, "移除所选").IsEnabled);
                        Assert.True(Button(page, "保存计划").IsEnabled);
                        foreach (var content in new[] { "保存计划", "开始本轮", "移除所选" })
                        {
                            var button = Button(page, content);
                            var point = button.TranslatePoint(new Point(), page);
                            Assert.InRange(point.X, 0, page.ActualWidth - button.ActualWidth);
                            Assert.InRange(point.Y, 0, page.ActualHeight - button.ActualHeight);
                            Assert.True(button.Focusable && button.IsTabStop);
                        }
                        var englishHeading = Descendants<TextBlock>(page).Single(item => item.Text == "英语词形提示");
                        Assert.Equal(language == LearningLanguage.English, englishHeading.IsVisible);
                    }
                    finally { window.Close(); }
                }
            });
        }

        [Theory]
        [InlineData(21)]
        [InlineData(1001)]
        public Task Scroll_AppendKeepsSelectionAndRefreshKeepsPosition(int count)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                var importer = provider.GetRequiredService<ImportInboxService>();
                var staged = await importer.StageAsync(new StageImportBatchRequestDto(LearningLanguage.Japanese,
                    ImportSourceKind.Manual, new[] { new ImportSelectionDto("食前", "jmdict:2:1") }), Token);
                await importer.DecideAsync(new DecideImportBatchRequestDto(staged.Batch.BatchId,
                    staged.Batch.Items.Select(item => item.ItemId).ToArray(), ImportDecision.Learning), Token);
                var libraryManager = provider.GetRequiredService<ILearningLibraryManager>();
                var template = (await libraryManager.GetLibraryPageAsync(LearningLanguage.Japanese, 1, 20, Token)).Groups[0];
                var library = new ControlledLearningLibraryManager(template, count);
                var viewModel = new LearningPlanViewModel(provider.GetRequiredService<ILearningPlanEditor>(), library,
                    provider.GetRequiredService<ILearningSessionStartPreparer>(), provider.GetRequiredService<ILearningSessionNavigation>(),
                    provider.GetRequiredService<ILexiconSearch>(), provider.GetRequiredService<IBackNavigation>(), provider.GetRequiredService<IOverlayRequester>());
                viewModel.Initialize(LearningLanguage.Japanese);
                await viewModel.RefreshAsync(Token);
                viewModel.LibraryItems[0].IsSelected = true;
                var page = new LearningPlanPage { DataContext = viewModel };
                var window = provider.GetRequiredService<HomeWindow>();
                ApplyResources(window);
                ((ContentControl)window.FindName("CurrentPagePresenter")).Content = page;
                try
                {
                    window.ShowActivated = false;
                    window.Show();
                    await SettleAsync();
                    var saveButton = Button(page, "保存计划");
                    var savePosition = saveButton.TranslatePoint(new Point(), page);
                    Assert.InRange(savePosition.Y, 0, page.ActualHeight - saveButton.ActualHeight);
                    var scroll = Assert.Single(Descendants<ScrollViewer>(page), PageScrollHost.GetIsPageScrollViewer);
                    scroll.ScrollToBottom();
                    await SettleAsync();
                    Assert.Equal(Math.Min(40, count), viewModel.LibraryItems.Count);
                    Assert.True(viewModel.LibraryItems[0].IsSelected);
                    Assert.False(viewModel.LibraryItems[^1].IsSelected);
                    Assert.Null(viewModel.IsAllSelected);
                    scroll.ScrollToVerticalOffset(300);
                    await SettleAsync();
                    await viewModel.RefreshPreservingStateAsync(Token);
                    await SettleAsync();
                    Assert.InRange(scroll.VerticalOffset, 299, 301);
                    Assert.True(viewModel.LibraryItems[0].IsSelected);
                    var recreated = new LearningPlanPage { DataContext = viewModel };
                    ((ContentControl)window.FindName("CurrentPagePresenter")).Content = recreated;
                    await SettleAsync();
                    var restored = Assert.Single(Descendants<ScrollViewer>(recreated), PageScrollHost.GetIsPageScrollViewer);
                    Assert.InRange(restored.VerticalOffset, 299, 301);
                    var dailyInput = Assert.IsType<TextBox>(recreated.FindName("DailyTargetInput"));
                    dailyInput.Text = string.Empty;
                    await SettleAsync();
                    Assert.False(Button(recreated, "保存计划").IsEnabled);
                    Assert.False(Button(recreated, "开始本轮").IsEnabled);
                    dailyInput.Text = "20";
                    await SettleAsync();
                    Assert.True(Button(recreated, "保存计划").IsEnabled);
                    restored.ScrollToTop();
                    await SettleAsync();
                    System.Windows.Controls.Primitives.ScrollBar.PageDownCommand.Execute(null, restored);
                    await SettleAsync();
                    Assert.True(restored.VerticalOffset > 0);
                    while (viewModel.LibraryItems.Count < count)
                    {
                        var previousCount = viewModel.LibraryItems.Count;
                        restored.ScrollToBottom();
                        await SettleAsync();
                        Assert.InRange(viewModel.LibraryItems.Count - previousCount, 1, 20);
                        Assert.True(viewModel.LibraryItems[0].IsSelected);
                    }
                    Assert.Equal(count, viewModel.LibraryItems.Select(item => item.WordGroupId).Distinct().Count());
                    Assert.False(viewModel.LoadMoreCommand.CanExecute(null));
                    Assert.True(Button(recreated, "保存计划").IsEnabled);
                }
                finally { window.Close(); }
            });
        }

        private static Button Button(DependencyObject root, string content)
        {
            return Descendants<Button>(root).Single(button => Equals(button.Content, content));
        }

        private static void ApplyResources(HomeWindow window)
        {
            foreach (var name in new[] { "Typography", "Layout", "FormControls", "NavigationCards", "Buttons", "ScrollBars" })
            {
                window.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/Dictation.Presentation;component/Theme/{name}.xaml", UriKind.Relative)
                });
            }
        }

        private static async Task SettleAsync()
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match) yield return match;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
            }
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
            using var stream = File.Create(Path.Combine(output, $"learning-plan-{name}.png"));
            encoder.Save(stream);
        }
    }
}
