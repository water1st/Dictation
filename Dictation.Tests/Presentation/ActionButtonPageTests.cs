using System.Windows;
using System.Windows.Controls;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ThemeMode = Dictation.Core.Configuration.ThemeMode;

namespace Dictation.Tests.Presentation
{
    /// <summary>以真实页面与 ViewModel 核验两种语言和主题下的操作按钮。</summary>
    public sealed class ActionButtonPageTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese, ThemeMode.Light)]
        [InlineData(LearningLanguage.Japanese, ThemeMode.Dark)]
        [InlineData(LearningLanguage.Japanese, ThemeMode.System)]
        [InlineData(LearningLanguage.English, ThemeMode.Light)]
        [InlineData(LearningLanguage.English, ThemeMode.Dark)]
        [InlineData(LearningLanguage.English, ThemeMode.System)]
        public Task Pages_LanguageAndTheme_KeepRoundedActionsAndSeparateNavigation(LearningLanguage language, ThemeMode mode) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path, services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
            });
            using var scope = provider.CreateScope();
            var services = scope.ServiceProvider;
            var home = services.GetRequiredService<HomeViewModel>();
            await home.SelectLanguageAsync(language, TestContext.Current.CancellationToken);
            var learning = services.GetRequiredService<LearningPlanViewModel>();
            learning.Initialize(language);
            var review = services.GetRequiredService<ReviewPlanViewModel>();
            review.Initialize(language);
            var library = services.GetRequiredService<ReviewLibraryViewModel>();
            library.Initialize(language);
            var lookup = services.GetRequiredService<LexiconLookupViewModel>();
            lookup.Initialize(language);
            var import = services.GetRequiredService<TextImportViewModel>();
            import.Initialize(language);
            var inbox = services.GetRequiredService<ImportInboxListViewModel>();
            inbox.Initialize(language);
            var wrong = services.GetRequiredService<WrongAnswerListViewModel>();
            wrong.Initialize(language);
            var practice = services.GetRequiredService<PracticeSessionViewModel>();
            practice.Initialize(language);
            var statistics = services.GetRequiredService<StatisticsViewModel>();
            statistics.Initialize(language);
            var staged = await services.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(language,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto(language == LearningLanguage.Japanese ? "食べる" : "go",
                    language == LearningLanguage.Japanese ? "jmdict:1:1" : "go%2:01:00::") }), TestContext.Current.CancellationToken);
            var detail = services.GetRequiredService<ImportInboxDetailViewModel>();
            detail.Initialize(staged.Batch, Assert.Single(staged.Batch.Items).ItemId);
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
                new KanaPlanPage { DataContext = services.GetRequiredService<KanaPlanViewModel>() },
                new KanaSessionPage { DataContext = services.GetRequiredService<KanaSessionViewModel>() }
            };
            using var system = new WindowsSystemThemeProvider(() => 0, subscribeToSystemEvents: false);
            using var manager = new ThemeManager(system);
            manager.SetThemeMode(mode);
            foreach (var page in pages)
            {
                var host = FormControlThemeTests.CreateHost(page);
                host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Dictation.Presentation;component/Theme/NavigationCards.xaml", UriKind.Relative) });
                ThemePalette.Apply(host.Resources, manager.CurrentTheme);
                host.Width = 860;
                host.Height = 556;
                page.Width = 860;
                page.Height = 556;
                host.Measure(new Size(860, 556));
                host.Arrange(new Rect(0, 0, 860, 556));
                host.UpdateLayout();
                var buttons = FormControlThemeTests.Children<Button>(page).ToArray();
                Assert.NotEmpty(buttons);
                foreach (var button in buttons)
                {
                    button.ApplyTemplate();
                    var border = FormControlThemeTests.Children<Border>(button).First();
                    var navigation = page is HomePage or LanguageWorkspacePage
                        || page is SettingsPage && ReferenceEquals(button.DataContext, settings);
                    Assert.Equal(new CornerRadius(navigation ? 18 : 14), border.CornerRadius);
                    if (!navigation)
                    {
                        Assert.NotNull(button.Template.FindName("FocusIndicator", button));
                    }
                    else
                    {
                        Assert.Null(button.Template.FindName("FocusIndicator", button));
                    }
                    if (button.IsVisible && button.ActualWidth > 0)
                    {
                        Assert.True(button.ActualWidth <= 860, $"{page.GetType().Name} 的按钮超出固定视口宽度。");
                    }
                }
                ActionButtonThemeTests.Render(page, $"{language}-{mode}-{page.GetType().Name}");
                foreach (var scroll in FormControlThemeTests.Children<ScrollViewer>(page))
                {
                    scroll.ScrollToBottom();
                }
                ActionButtonThemeTests.Render(page, $"{language}-{mode}-{page.GetType().Name}-bottom");
                host.Children.Clear();
            }
        });
    }
}
