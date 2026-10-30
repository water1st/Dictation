using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Controls;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ThemeMode = Dictation.Core.Configuration.ThemeMode;

namespace Dictation.Tests.Presentation
{
    /// <summary>在双语言真实页面中核验主题继承、嵌套视口及固定窗口布局。</summary>
    public sealed class ScrollBarPageTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public Task Pages_ThemeAndLanguageChanges_KeepEveryScrollContainerThemed(LearningLanguage language)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path, services =>
                {
                    services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                    services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                });
                var token = TestContext.Current.CancellationToken;
                var headword = language == LearningLanguage.Japanese ? "食べる" : "go";
                var sense = language == LearningLanguage.Japanese ? "jmdict:1:1" : "go%2:01:00::";
                await ReviewPlanPresentationTestHost.ImportAsync(provider, headword, sense, language);
                var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(language,
                    ImportSourceKind.Manual, new[] { new ImportSelectionDto(headword, sense) }), token);
                using var scope = provider.CreateScope();
                var services = scope.ServiceProvider;
                var home = services.GetRequiredService<HomeViewModel>();
                await home.SelectLanguageAsync(language, token);
                var learning = services.GetRequiredService<LearningPlanViewModel>();
                learning.Initialize(language);
                await learning.RefreshAsync(token);
                var review = services.GetRequiredService<ReviewPlanViewModel>();
                review.Initialize(language);
                await review.RefreshAsync(token);
                var library = services.GetRequiredService<ReviewLibraryViewModel>();
                library.Initialize(language);
                await library.RefreshAsync(token);
                var lookup = services.GetRequiredService<LexiconLookupViewModel>();
                lookup.Initialize(language);
                lookup.Query = headword;
                await lookup.SearchAsync(token);
                var import = services.GetRequiredService<TextImportViewModel>();
                import.Initialize(language);
                await import.LoadTextAsync(string.Join("\r\n", Enumerable.Repeat(headword, 30)), token);
                var inbox = services.GetRequiredService<ImportInboxListViewModel>();
                inbox.Initialize(language);
                await inbox.RefreshAsync(token);
                var detail = services.GetRequiredService<ImportInboxDetailViewModel>();
                detail.Initialize(staged.Batch, Assert.Single(staged.Batch.Items).ItemId);
                var wrong = services.GetRequiredService<WrongAnswerListViewModel>();
                wrong.Initialize(language);
                await wrong.RefreshAsync(token);
                var practice = services.GetRequiredService<PracticeSessionViewModel>();
                practice.Initialize(language);
                var statistics = services.GetRequiredService<StatisticsViewModel>();
                statistics.Initialize(language);
                await statistics.RefreshAsync(token);
                var kana = services.GetRequiredService<KanaPlanViewModel>();
                await kana.RefreshAsync(token);
                var settings = services.GetRequiredService<SettingsViewModel>();
                await settings.SelectTargetLanguageAsync(language);
                var pages = new UserControl[]
                {
                    new HomePage { DataContext = services.GetRequiredService<LandingViewModel>() },
                    new LanguageWorkspacePage { DataContext = home },
                    new SettingsPage { DataContext = settings },
                    new McpSettingsPage { DataContext = settings.Mcp },
                    new LearningPlanPage { DataContext = learning },
                    new ReviewPlanPage { DataContext = review },
                    new ReviewLibraryPage { DataContext = library },
                    new LexiconLookupPage { DataContext = lookup },
                    new TextImportPage { DataContext = import },
                    new ImportInboxListPage { DataContext = inbox },
                    new ImportInboxDetailPage { DataContext = detail },
                    new WrongAnswerListPage { DataContext = wrong },
                    new PracticeSessionPage { DataContext = practice },
                    new StatisticsPage { DataContext = statistics },
                    new KanaPlanPage { DataContext = kana },
                    new KanaSessionPage { DataContext = services.GetRequiredService<KanaSessionViewModel>() }
                };
                using var system = new WindowsSystemThemeProvider(() => 0, subscribeToSystemEvents: false);
                using var manager = new ThemeManager(system);
                var visibleBars = 0;
                foreach (var page in pages)
                {
                    var shell = new PageScrollHost { Content = page };
                    var host = ScrollBarThemeTests.CreateHost(shell);
                    host.Width = 860;
                    host.Height = page.Height = 556;
                    foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
                    {
                        manager.SetThemeMode(mode);
                        ThemePalette.Apply(host.Resources, manager.CurrentTheme);
                        var shared = host.Resources.MergedDictionaries.Single(dictionary => dictionary.Source.OriginalString.EndsWith("ScrollBars.xaml", StringComparison.Ordinal));
                        host.Resources.MergedDictionaries.Remove(shared);
                        ScrollBarThemeTests.Layout(host);
                        ScrollBarThemeTests.Export(shell, $"{language}-{page.GetType().Name}-{mode}-before");
                        host.Resources.MergedDictionaries.Add(shared);
                        ScrollBarThemeTests.Layout(host);
                        ScrollBarInteractionTests.AssertThemedBars(shell, 0);
                        var mainViewers = FormControlThemeTests.Children<ScrollViewer>(page).Where(PageScrollHost.GetIsPageScrollViewer).ToArray();
                        if (page is not HomePage and not McpSettingsPage and not TextImportPage)
                        {
                            var main = Assert.Single(mainViewers);
                            Assert.Equal(ScrollBarVisibility.Hidden, main.VerticalScrollBarVisibility);
                            var rail = Assert.IsType<ScrollBar>(shell.Template.FindName("PART_PageScrollBar", shell));
                            if (main.ScrollableHeight > 0)
                            {
                                Assert.Equal(Visibility.Visible, rail.Visibility);
                                var edge = rail.TransformToAncestor(shell).TransformBounds(new Rect(rail.RenderSize));
                                Assert.Equal(860, edge.Right, 3);
                                Assert.Equal(0, edge.Top, 3);
                                Assert.Equal(556, edge.Bottom, 3);
                            }
                        }
                        var bars = FormControlThemeTests.Children<ScrollBar>(shell).Where(bar => bar.Visibility == Visibility.Visible).ToArray();
                        visibleBars += bars.Length;
                        foreach (var bar in bars)
                        {
                            var bounds = bar.TransformToAncestor(shell).TransformBounds(new Rect(bar.RenderSize));
                            Assert.True(bounds.Right <= 861 && bounds.Left >= -1, $"{page.GetType().Name} 的滚动条超出页面横向边界。");
                        }
                        ScrollBarThemeTests.Export(shell, $"{language}-{page.GetType().Name}-{mode}-after");
                        foreach (var viewer in FormControlThemeTests.Children<ScrollViewer>(page).ToArray())
                        {
                            viewer.ScrollToBottom();
                        }
                        ScrollBarThemeTests.Layout(host);
                        ScrollBarInteractionTests.AssertThemedBars(shell, 0);
                        ScrollBarThemeTests.Export(shell, $"{language}-{page.GetType().Name}-{mode}-bottom");
                        foreach (var viewer in FormControlThemeTests.Children<ScrollViewer>(page).ToArray())
                        {
                            viewer.ScrollToTop();
                        }
                        if (page is SettingsPage)
                        {
                            foreach (var section in Enum.GetValues<SettingsSection>())
                            {
                                await settings.SelectSectionAsync(section, token);
                                ScrollBarThemeTests.Layout(host);
                                ScrollBarInteractionTests.AssertThemedBars(page, 0);
                            }
                        }
                    }
                    host.Children.Clear();
                }
                Assert.True(visibleBars >= 12, $"双语言页面中的实际可见滚动条数量不足：{visibleBars}。");
            });
        }
    }
}
