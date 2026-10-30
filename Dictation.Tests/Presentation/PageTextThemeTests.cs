using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
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
    /// <summary>在真实业务数据与页面模板上审计文字，并对同一页面反复切换主题。</summary>
    [Collection("DpiRendering")]
    public sealed class PageTextThemeTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese, 1d)]
        [InlineData(LearningLanguage.Japanese, 1.5d)]
        [InlineData(LearningLanguage.Japanese, 2d)]
        [InlineData(LearningLanguage.English, 1d)]
        [InlineData(LearningLanguage.English, 1.5d)]
        [InlineData(LearningLanguage.English, 2d)]
        public Task Pages_ThemeChanges_KeepTextAndInlinesReadable(LearningLanguage language, double scale) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path, services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
            });
            await ReviewPlanPresentationTestHost.ImportAsync(provider,
                language == LearningLanguage.Japanese ? "食べる" : "go",
                language == LearningLanguage.Japanese ? "jmdict:1:1" : "go%2:01:00::", language);
            var learningBatch = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(language,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto(language == LearningLanguage.Japanese ? "食前" : "take off",
                    language == LearningLanguage.Japanese ? "jmdict:2:1" : "take_off%2:38:00::") }), TestContext.Current.CancellationToken);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                learningBatch.Batch.BatchId, new[] { Assert.Single(learningBatch.Batch.Items).ItemId }, ImportDecision.Learning), TestContext.Current.CancellationToken);
            await provider.GetRequiredService<LearningPlanService>().SaveAsync(new SaveLearningPlanRequestDto(language, 20, 20,
                LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null,
                new[] { language == LearningLanguage.Japanese ? "Dictionary" : "Base" },
                new[] { LearningChannel.MeaningWriting },
                new[] { language == LearningLanguage.Japanese ? LearningOutputMode.Kana : LearningOutputMode.Latin }), TestContext.Current.CancellationToken);
            using var scope = provider.CreateScope();
            var services = scope.ServiceProvider;
            var token = TestContext.Current.CancellationToken;
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
            lookup.Query = language == LearningLanguage.Japanese ? "食べる" : "go";
            await lookup.SearchAsync(token);
            var import = services.GetRequiredService<TextImportViewModel>();
            import.Initialize(language);
            await import.LoadTextAsync(language == LearningLanguage.Japanese ? "食べる" : "go", token);
            import.ReportInputError("导入错误示例：请检查输入词条。");
            var staged = await services.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(language,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto(language == LearningLanguage.Japanese ? "食べる" : "go",
                    language == LearningLanguage.Japanese ? "jmdict:1:1" : "go%2:01:00::") }), token);
            var inbox = services.GetRequiredService<ImportInboxListViewModel>();
            inbox.Initialize(language);
            await inbox.RefreshAsync(token);
            var detail = services.GetRequiredService<ImportInboxDetailViewModel>();
            detail.Initialize(staged.Batch, Assert.Single(staged.Batch.Items).ItemId);
            var wrong = services.GetRequiredService<WrongAnswerListViewModel>();
            wrong.Initialize(language);
            var practice = services.GetRequiredService<PracticeSessionViewModel>();
            practice.Initialize(language);
            await practice.StartAsync(cancellationToken: token);
            Assert.NotNull(practice.Question);
            practice.AnswerText = "错误答案";
            await practice.SubmitAsync(token);
            Assert.False(practice.Feedback!.WasCorrect);
            await wrong.RefreshAsync(token);
            Assert.NotEmpty(wrong.Items);
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
            foreach (var page in pages)
            {
                var host = FormControlThemeTests.CreateHost(page);
                host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Dictation.Presentation;component/Theme/NavigationCards.xaml", UriKind.Relative) });
                host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Dictation.Presentation;component/Theme/ScrollBars.xaml", UriKind.Relative) });
                host.Width = page.Width = 860;
                page.Height = 556;
                using var source = new HwndSource(new HwndSourceParameters("页面主题验收") { Width = 860, Height = 556 });
                source.RootVisual = host;
                VisualTreeHelper.SetRootDpi(host, new DpiScale(scale, scale));
                foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System, ThemeMode.Dark, ThemeMode.Light })
                {
                    manager.SetThemeMode(mode);
                    ThemePalette.Apply(host.Resources, manager.CurrentTheme);
                    host.Measure(new Size(860, 556));
                    host.Arrange(new Rect(0, 0, 860, 556));
                    host.UpdateLayout();
                    AuditText(page, host);
                    SaveImage(page, $"{language}-{page.GetType().Name}-{mode}-top");
                    foreach (var list in FormControlThemeTests.Children<ListBox>(page))
                    {
                        if (list.Items.Count > 0) list.SelectedIndex = 0;
                    }
                    foreach (var scroll in FormControlThemeTests.Children<ScrollViewer>(page)) scroll.ScrollToBottom();
                    host.UpdateLayout();
                    AuditText(page, host);
                    SaveImage(page, $"{language}-{page.GetType().Name}-{mode}-bottom");
                    if (page is SettingsPage)
                    {
                        foreach (var section in Enum.GetValues<SettingsSection>())
                        {
                            await settings.SelectSectionAsync(section, token);
                            host.UpdateLayout();
                            AuditText(page, host);
                            SaveImage(page, $"{language}-SettingsPage-{mode}-{section}");
                        }
                    }
                    foreach (var scroll in FormControlThemeTests.Children<ScrollViewer>(page)) scroll.ScrollToTop();
                }
                if (page is KanaSessionPage)
                {
                    kana.Enabled = true;
                    kana.DailyTarget = 1;
                    await kana.SaveAsync(token);
                    var session = (KanaSessionViewModel)page.DataContext;
                    await session.StartAsync(token);
                    Assert.True(session.IsAnswering);
                    var correctAnswer = string.Empty;
                    for (var phase = 0; phase < 4; phase++)
                    {
                        foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
                        {
                            ThemePalette.Apply(host.Resources, theme);
                            host.UpdateLayout();
                            AuditText(page, host);
                            SaveImage(page, $"{language}-KanaSessionPage-{theme}-phase{phase}");
                        }
                        if (phase == 0)
                        {
                            session.AnswerText = "错误答案";
                            await session.SubmitAsync(token);
                            Assert.True(session.IsFeedbackVisible);
                            correctAnswer = session.Feedback!.CorrectAnswer;
                        }
                        else if (phase < 3)
                        {
                            await session.ContinueAsync(token);
                            session.AnswerText = correctAnswer;
                            await session.SubmitAsync(token);
                            Assert.True(session.Feedback!.IsCorrect);
                            await session.ContinueAsync(token);
                            if (phase == 1)
                            {
                                Assert.True(session.IsAnswering);
                            }
                        }
                    }
                    Assert.True(session.IsCompleted);
                }
                host.Children.Clear();
            }
        });

        private static void AuditText(UserControl page, FrameworkElement host)
        {
            var allowed = new[] { "ForegroundBrush", "SecondaryForegroundBrush", "AccentBrush", "ErrorBrush" }
                .Select(key => ((SolidColorBrush)host.Resources[key]).Color).ToArray();
            var texts = FormControlThemeTests.Children<TextBlock>(page)
                .Where(text => text.IsVisible && text.ActualWidth > 0 && !string.IsNullOrWhiteSpace(text.Text)).ToArray();
            Assert.True(texts.Length > 0, $"{page.GetType().Name} 必须实际生成可见文字。");
            foreach (var text in texts)
            {
                var color = Assert.IsType<SolidColorBrush>(text.Foreground).Color;
                var accentColors = new[] { "AccentBrush", "PrimaryActionBrush", "PrimaryActionHoverBrush", "PrimaryActionPressedBrush" }
                    .Select(key => ((SolidColorBrush)host.Resources[key]).Color).ToArray();
                var accentButton = FindAccentButton(text, accentColors);
                var expected = accentButton is null ? allowed : new[] { ((SolidColorBrush)host.Resources["AccentForegroundBrush"]).Color };
                Assert.True(expected.Contains(color), $"{page.GetType().Name} 的文字 {text.Text} 使用不适用于所在背景的颜色 {color}。");
                foreach (var run in text.Inlines.OfType<Run>().Where(run => !string.IsNullOrEmpty(run.Text)))
                {
                    Assert.Contains(Assert.IsType<SolidColorBrush>(run.Foreground).Color, expected);
                }
            }
        }

        private static Button? FindAccentButton(DependencyObject element, Color[] accents)
        {
            for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is Button button)
                {
                    return button.Background is SolidColorBrush brush && accents.Contains(brush.Color) ? button : null;
                }
            }
            return null;
        }

        private static void SaveImage(FrameworkElement element, string name)
        {
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output)) return;
            Directory.CreateDirectory(output);
            var scale = VisualTreeHelper.GetDpi(element).DpiScaleX;
            var bitmap = new RenderTargetBitmap((int)(860 * scale), (int)(556 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle((Brush)element.FindResource("WindowBackgroundBrush"), null, new Rect(0, 0, 860, 556));
                drawing.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, 860, 556));
            }
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, $"{name}-{scale * 100:0}pct.png"));
            encoder.Save(stream);
        }
    }
}
