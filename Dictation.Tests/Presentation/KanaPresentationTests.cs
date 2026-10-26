using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Kana;
using Dictation.Core.Tts;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dictation.Tests.Presentation
{
    /// <summary>通过STA、真实XAML、DI和SQLite验证五十音独立页面，不模拟业务层。</summary>
    public sealed class KanaPresentationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public Task HomePlanSessionFeedbackSummary_ReturnUsesSameWindowAndOwnProgress() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var tts = new LearningPresentationTestEnvironment.ControlledTtsProvider();
            var audio = new LearningPresentationTestEnvironment.ControlledAudioPlayer();
            using var provider = await CreateAsync(directory.Path, tts, audio);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            navigation.InitializeWorkspace(home);
            await home.OpenKanaAsync(Token);
            var plan = Assert.IsType<KanaPlanViewModel>(navigation.CurrentPage);
            plan.Enabled = true;
            plan.Katakana = true;
            plan.Hiragana = false;
            plan.Voiced = true;
            plan.Clear = false;
            plan.DailyTarget = 1;
            plan.ReviewAll = false;
            await plan.SaveAsync(Token);
            Assert.Equal("五十音 0/1", plan.ProgressLabel);
            navigation.CompleteTransition();
            var windows = Application.Current?.Windows.Count ?? 0;
            await plan.StartAsync(Token);
            var session = Assert.IsType<KanaSessionViewModel>(navigation.CurrentPage);
            var page = new KanaSessionPage { DataContext = session, Width = 960, Height = 720 };
            Render(page);
            Assert.Equal(KanaScript.Katakana, session.Question!.Script);
            Assert.Contains("片假名", Assert.IsType<TextBlock>(page.FindName("QuestionPrompt")).Text);
            Assert.Empty(Assert.IsType<TextBlock>(page.FindName("CorrectAnswer")).Text);
            Assert.Equal(session.Question.SpokenText, Assert.Single(tts.Requests).Text);
            Assert.Single(audio.Played);
            session.AnswerText = "错误";
            await session.SubmitAsync(Token);
            Assert.False(session.Feedback!.IsCorrect);
            Render(page);
            Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<FrameworkElement>(page.FindName("FeedbackPanel")).Visibility);
            Assert.Equal(session.Feedback.CorrectAnswer, Assert.IsType<TextBlock>(page.FindName("CorrectAnswer")).Text);
            // 用已经合法揭示的错误答案完成两次纠正，不从题面读取未揭示答案。
            var correction = session.Feedback.CorrectAnswer;
            await session.ContinueAsync(Token);
            session.AnswerText = correction;
            await session.SubmitAsync(Token);
            Assert.False(session.Feedback!.IsCompleted);
            await session.ContinueAsync(Token);
            session.AnswerText = correction;
            await session.SubmitAsync(Token);
            await session.ContinueAsync(Token);
            Assert.True(session.IsCompleted);
            Assert.Single(session.Errors);
            Render(page);
            Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<FrameworkElement>(page.FindName("SummaryPanel")).Visibility);
            Assert.Equal("进度 1/1", session.ProgressLabel);
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories WHERE ReviewCount=1 AND LapseCount=1;"));
            Assert.Equal(3L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM WordGroups;"));
            Assert.Equal(windows, Application.Current?.Windows.Count ?? 0);
            navigation.CompleteTransition();
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            Assert.Same(plan, navigation.CurrentPage);
            Assert.Equal("五十音 1/1", plan.ProgressLabel);
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal("五十音 1/1", home.KanaProgressLabel);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => session.StartAsync(Token));
        });

        [Fact]
        public Task Plan_CheckboxesKeepOnePerDimensionAndUseThemeBrushes() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var vm = provider.GetRequiredService<KanaPlanViewModel>();
            await vm.RefreshAsync(Token);
            var page = new KanaPlanPage { DataContext = vm, Width = 960, Height = 720 };
            Render(page);
            Assert.False(Assert.IsType<CheckBox>(page.FindName("HiraganaChoice")).IsEnabled);
            Assert.False(Assert.IsType<CheckBox>(page.FindName("ClearChoice")).IsEnabled);
            vm.Hiragana = false;
            vm.Clear = false;
            Assert.True(vm.Hiragana && vm.Clear);
            vm.Katakana = true;
            vm.Hiragana = false;
            vm.Contracted = true;
            vm.Clear = false;
            vm.Contracted = false;
            Assert.True(vm.Katakana && vm.Contracted);
            vm.Enabled = true;
            vm.ReviewAll = true;
            await vm.SaveAsync(Token);
            var saved = await provider.GetRequiredService<IKanaPlanReader>().LoadAsync(Token);
            Assert.True(saved.ReviewAll && saved.Katakana && saved.Contracted);
            Assert.Equal("本轮全量 36 项", vm.RangeLabel);
            Render(page);
            Assert.False(Assert.IsType<TextBox>(page.FindName("DailyQuantity")).IsEnabled);
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(page.Resources, theme);
                Render(page);
                Assert.Equal(theme == ResolvedTheme.Light ? Color.FromRgb(31, 41, 55) : Color.FromRgb(248, 250, 252),
                    Assert.IsType<SolidColorBrush>(Assert.IsType<CheckBox>(page.FindName("KatakanaChoice")).Foreground).Color);
            }
        });

        [Fact]
        public Task EnglishHome_HidesAndRejectsKanaNavigation() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeWorkspace(home);
            await home.SelectLanguageAsync(LearningLanguage.English, Token);
            var page = new LanguageWorkspacePage { DataContext = home, Width = 960, Height = 720 };
            Render(page);
            Assert.Equal(Visibility.Collapsed, Assert.IsType<Button>(page.FindName("KanaButton")).Visibility);
            Assert.False(home.OpenKanaCommand.CanExecute(null));
            await Assert.ThrowsAsync<InvalidOperationException>(() => home.OpenKanaAsync(Token));
            Assert.Same(home, nav.CurrentPage);
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task AudioFailure_CannotSubmitUntilSuccessfulRetryAndDoesNotWriteFiles(bool playerFails) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var tts = new LearningPresentationTestEnvironment.ControlledTtsProvider();
            var audio = new LearningPresentationTestEnvironment.ControlledAudioPlayer();
            if (playerFails) audio.Failure = new IOException("播放失败"); else tts.Failure = new IOException("合成失败");
            using var provider = await CreateAsync(directory.Path, tts, audio);
            await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), Token);
            var vm = provider.GetRequiredService<KanaSessionViewModel>();
            await vm.StartAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.False(vm.SubmitCommand.CanExecute(null));
            vm.AnswerText = "错误";
            await vm.SubmitAsync(Token);
            Assert.Null(vm.Feedback);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories;"));
            tts.Failure = null;
            audio.Failure = null;
            await vm.PlayAsync(Token);
            Assert.True(vm.SubmitCommand.CanExecute(null));
            await vm.SubmitAsync(Token);
            Assert.False(vm.Feedback!.IsCorrect);
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Empty(Directory.GetFiles(directory.Path, "*.wav", SearchOption.AllDirectories));
        });

        [Fact]
        public Task ReturnBeforeAudioCompletion_CancelsOldScopeWithoutLateErrorOrObservation() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tts = new LearningPresentationTestEnvironment.ControlledTtsProvider
            {
                BeforeReturnAsync = async _ => { entered.TrySetResult(); await completion.Task; throw new IOException("退场后的音频异常"); }
            };
            using var provider = await CreateAsync(directory.Path, tts);
            await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), Token);
            var home = provider.GetRequiredService<HomeViewModel>();
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeWorkspace(home);
            await home.OpenKanaAsync(Token);
            var plan = Assert.IsType<KanaPlanViewModel>(nav.CurrentPage);
            nav.CompleteTransition();
            var opening = plan.StartAsync(Token);
            await entered.Task.WaitAsync(Token);
            var old = Assert.IsType<KanaSessionViewModel>(nav.CurrentPage);
            nav.CompleteTransition();
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            completion.TrySetResult();
            await opening;
            Assert.Null(old.ErrorMessage);
            Assert.Same(plan, nav.CurrentPage);
            await nav.GoBackAsync(Token);
            nav.CompleteTransition();
            await home.SelectLanguageAsync(LearningLanguage.English, Token);
            Assert.Null(home.KanaProgressErrorMessage);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task HomeLateProgress_AfterLanguageSwitchCannotPublishCountOrError(bool fail) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var delayed = new DelayedProgress();
            using var provider = await CreateAsync(directory.Path, configure: services => services.AddSingleton<IKanaProgressReader>(delayed));
            var home = provider.GetRequiredService<HomeViewModel>();
            var pending = home.RefreshKanaProgressAsync(Token);
            await delayed.Entered.Task.WaitAsync(Token);
            await home.SelectLanguageAsync(LearningLanguage.English, Token);
            if (fail) delayed.Result.SetException(new IOException("旧日语统计失败")); else delayed.Result.SetResult(new(true, 7, 20, 13, false));
            await pending;
            Assert.Equal("", home.KanaProgressLabel);
            Assert.Null(home.KanaProgressErrorMessage);
        });

        [Fact]
        public Task Plan_InvalidQuantityAndBrokenStoreShowsInlineErrors() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var vm = provider.GetRequiredService<KanaPlanViewModel>();
            await vm.RefreshAsync(Token);
            vm.DailyTarget = 0;
            await vm.SaveAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaPlans;"));
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync("DROP TABLE KanaPlans;");
            await vm.RefreshAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.False(vm.IsBusy);
        });

        private static Task<ServiceProvider> CreateAsync(string root, ITtsProvider? tts = null, IAudioPlayer? audio = null, Action<IServiceCollection>? configure = null) =>
            ReviewPlanPresentationTestHost.CreateAsync(root, createLexicons: false, configure: services =>
            {
                services.AddSingleton(tts ?? new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton(audio ?? new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                configure?.Invoke(services);
            });
        private static async Task<long> ScalarAsync(ServiceProvider provider, string sql)
        {
            using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            return await connection.ExecuteScalarAsync<long>(sql);
        }
        private static void Render(FrameworkElement page)
        {
            page.Measure(new Size(960, 720));
            page.Arrange(new Rect(0, 0, 960, 720));
            page.UpdateLayout();
        }
        /// <summary>仅延迟外部统计读取以验证首页版本保护，不模拟五十音作答。</summary>
        private sealed class DelayedProgress : IKanaProgressReader
        {
            internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<KanaProgressDto> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<KanaProgressDto> GetProgressAsync(CancellationToken cancellationToken = default)
            {
                Entered.TrySetResult();
                return Result.Task;
            }
        }
    }
}
