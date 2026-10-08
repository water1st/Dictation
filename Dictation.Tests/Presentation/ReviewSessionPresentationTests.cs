using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.Core.Tts;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Practice;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证复习入口在既有单窗口中复用练习界面，真实成绩与学习隔离。</summary>
    public sealed class ReviewSessionPresentationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public Task HomeEntry_ResumesOriginalQuestionAndLocksLanguageUntilHome(LearningLanguage language) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await ImportAndPlanAsync(provider, language);
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            await home.SelectLanguageAsync(language, Token);
            navigation.InitializeHome(home);
            await home.OpenReviewSessionAsync(Token);
            var first = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            var question = first.Question!;
            Assert.Equal(language, question.Language);
            Assert.Equal(language == LearningLanguage.Japanese ? "日语复习" : "英语复习", navigation.PageTitle);
            Assert.Null(question.SpokenText);
            Assert.Null(first.Feedback);
            Assert.False(first.ReplayQuestionCommand.CanExecute(null));
            Assert.Throws<InvalidOperationException>(() => first.Initialize(language == LearningLanguage.Japanese ? LearningLanguage.English : LearningLanguage.Japanese));
            navigation.CompleteTransition();
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            Assert.Same(home, navigation.CurrentPage);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => first.SubmitAsync(Token));
            await home.OpenReviewSessionAsync(Token);
            var resumed = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            Assert.NotSame(first, resumed);
            Assert.Equal(question.SessionId, resumed.Session!.SessionId);
            Assert.Equal(question.QuestionId, resumed.Question!.QuestionId);
        });

        [Fact]
        public Task WrongThenTwoCorrect_ShowsImmediateAnswerAndSummaryAndCountsOneGroup() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await ImportAndPlanAsync(provider, LearningLanguage.Japanese);
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeHome(home);
            await home.OpenReviewSessionAsync(Token);
            var vm = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            vm.AnswerText = "错误";
            await vm.SubmitAsync(Token);
            Assert.False(vm.Feedback!.WasCorrect);
            Assert.Equal("たべます", vm.Feedback.CorrectAnswer);
            Assert.True(vm.IsFeedbackVisible);
            Assert.Equal("再记一次，稍后重练", vm.FeedbackLabel);
            Assert.False(vm.SubmitCommand.CanExecute(null));
            for (var index = 0; index < 2; index++)
            {
                await vm.ContinueAsync(Token);
                Assert.Null(vm.Feedback);
                vm.AnswerText = "たべます";
                await vm.SubmitAsync(Token);
                Assert.True(vm.Feedback!.WasCorrect);
                Assert.True(vm.Feedback.HadErrorInSession);
            }
            await vm.ContinueAsync(Token);
            Assert.True(vm.IsCompleted);
            var summary = Assert.IsType<PracticeSessionSummary>(vm.Summary);
            Assert.Equal(1, summary.CompletedWordGroupCount);
            Assert.Equal(3, summary.AnsweredCount);
            Assert.Equal("たべます", Assert.Single(summary.Errors).CorrectAnswer);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT LapseCount FROM ReviewFormMemories;"));
            navigation.CompleteTransition();
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            Assert.Equal("复习 1/20", home.ReviewProgressLabel);
            Assert.Equal("学习 0/20", home.LearningProgressLabel);
        });

        [Fact]
        public Task FailedAudio_CanReplayAndBrowseWithoutAdvancingMemory() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var tts = new LearningPresentationTestEnvironment.ControlledTtsProvider { Failure = new IOException("语音暂不可用") };
            var audio = new LearningPresentationTestEnvironment.ControlledAudioPlayer();
            using var provider = await CreateAsync(directory.Path, tts, audio);
            await ImportAndPlanAsync(provider, LearningLanguage.Japanese, true);
            var vm = await OpenAsync(provider);
            Assert.Contains("语音暂不可用", vm.ErrorMessage);
            vm.AnswerText = "たべます";
            Assert.False(vm.SubmitCommand.CanExecute(null));
            await vm.SubmitAsync(Token);
            Assert.Null(vm.Feedback);
            Assert.Equal(0, await AttemptCountAsync(provider));
            tts.Failure = null;
            await vm.ReplayQuestionAsync(Token);
            Assert.Equal("たべます", tts.Requests[^1].Text);
            Assert.Single(audio.Played);
            Assert.True(vm.SubmitCommand.CanExecute(null));
            var questionId = vm.Question!.QuestionId;
            vm.OpenBrowseCommand.Execute(null);
            Assert.True(vm.IsBrowsing);
            Assert.Equal("食べる", vm.SelectedBrowseMember!.Headword);
            Assert.Contains("吃", vm.SelectedBrowseMember.Meanings);
            Assert.All(vm.BrowseForms, form => Assert.Equal("Polite", form.FormCode));
            await vm.ReplayBrowseAsync(Token);
            Assert.Equal("たべます", tts.Requests[^1].Text);
            Assert.Equal(0, await AttemptCountAsync(provider));
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ReviewFormMemories;"));
            vm.CloseBrowseCommand.Execute(null);
            Assert.Equal(questionId, vm.Question.QuestionId);
            Assert.True(vm.SubmitCommand.CanExecute(null));
        });

        [Fact]
        public Task EmptyReview_ShowsNoDueMessageWithoutFakeCompletionOrQuestion() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var vm = await OpenAsync(provider);
            Assert.True(vm.IsEmptyVisible);
            Assert.Equal("当前没有待测或到期词组，或今天的复习目标已完成。", vm.EmptyMessage);
            Assert.False(vm.IsCompleted);
            Assert.Null(vm.Session);
            Assert.Null(vm.Question);
            Assert.Null(vm.Summary);
            Assert.False(vm.SubmitCommand.CanExecute(null));
            Assert.False(vm.OpenBrowseCommand.CanExecute(null));
            var page = new PracticeSessionPage { DataContext = vm };
            Layout(page);
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(page.FindName("EmptyPanel")).Visibility);
            Assert.Equal(Visibility.Collapsed, Assert.IsType<Border>(page.FindName("SummaryPanel")).Visibility);
        });

        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task SharedPage_BindsReviewPromptFeedbackAndSummaryWithNoSpellingHints(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await ImportAndPlanAsync(provider, LearningLanguage.Japanese);
            var vm = await OpenAsync(provider);
            var page = new PracticeSessionPage { DataContext = vm };
            ThemePalette.Apply(page.Resources, theme);
            Layout(page);
            var answer = Assert.IsType<TextBox>(page.FindName("AnswerTextBox"));
            Assert.False(SpellCheck.GetIsEnabled(answer));
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(page.FindName("QuestionPanel")).Visibility);
            Assert.Equal(Visibility.Collapsed, Assert.IsType<Button>(page.FindName("ReplayQuestionButton")).Visibility);
            answer.SetCurrentValue(TextBox.TextProperty, "错误");
            Assert.Equal("错误", vm.AnswerText);
            await vm.SubmitAsync(Token);
            Layout(page);
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(page.FindName("FeedbackPanel")).Visibility);
            Assert.Equal(Visibility.Visible, Assert.IsType<Button>(page.FindName("ReplayQuestionButton")).Visibility);
            var home = provider.GetRequiredService<HomeViewModel>();
            var homePage = new HomePage { DataContext = home };
            Layout(homePage);
            var start = Assert.IsType<Button>(homePage.FindName("StartReviewButton"));
            Assert.Same(home.OpenReviewSessionCommand, start.Command);
            Assert.True(start.IsAncestorOf(Assert.IsType<TextBlock>(homePage.FindName("ReviewDailyProgress"))));
        });

        [Fact]
        public Task ReturnDuringSubmit_CancelsAndDiscardsLateFeedback() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var late = new LateAnswerer();
            using var provider = await CreateAsync(directory.Path, configure: services => services.AddSingleton<IReviewSessionAnswerer>(late));
            await ImportAndPlanAsync(provider, LearningLanguage.Japanese);
            var vm = await OpenAsync(provider);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.CompleteTransition();
            vm.AnswerText = "错误";
            var submit = vm.SubmitAsync(Token);
            await late.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            await navigation.GoBackAsync(Token);
            late.Release.SetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => submit);
            Assert.Null(vm.Feedback);
            Assert.Null(vm.Summary);
            Assert.Equal(0, await AttemptCountAsync(provider));
            navigation.CompleteTransition();
            Assert.IsType<HomeViewModel>(navigation.CurrentPage);
        });

        private static Task<ServiceProvider> CreateAsync(string root,
            LearningPresentationTestEnvironment.ControlledTtsProvider? tts = null,
            LearningPresentationTestEnvironment.ControlledAudioPlayer? audio = null,
            Action<IServiceCollection>? configure = null) => ReviewPlanPresentationTestHost.CreateAsync(root, configure: services =>
            {
                services.AddSingleton<ITtsProvider>(tts ?? new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(audio ?? new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                configure?.Invoke(services);
            });

        private static async Task ImportAndPlanAsync(ServiceProvider provider, LearningLanguage language, bool dictation = false)
        {
            var japanese = language == LearningLanguage.Japanese;
            await ReviewPlanPresentationTestHost.ImportAsync(provider, japanese ? "食べる" : "take off", japanese ? "jmdict:1:1" : "take_off%2:38:00::", language);
            await provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new SaveReviewPlanRequestDto(language, 20, 20,
                ReviewRangeMode.All, Array.Empty<string>(), new[] { japanese ? "Polite" : "Base" },
                new[] { dictation ? LearningChannel.Dictation : LearningChannel.MeaningWriting },
                new[] { japanese ? LearningOutputMode.Kana : LearningOutputMode.Latin }, EnglishFormLabelMode.GrammarName, null), Token);
        }

        private static async Task<PracticeSessionViewModel> OpenAsync(ServiceProvider provider)
        {
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeHome(home);
            await home.OpenReviewSessionAsync(Token);
            return Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
        }

        private static async Task<int> AttemptCountAsync(ServiceProvider provider)
        {
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ReviewAnswerAttempts;");
        }

        private static void Layout(FrameworkElement page)
        {
            page.Measure(new Size(1100, 1000)); page.Arrange(new Rect(0, 0, 1100, 1000)); page.UpdateLayout();
        }

        /// <summary>模拟不遵守取消的外部晚返回，只用于验证旧页不能发布反馈。</summary>
        private sealed class LateAnswerer : IReviewSessionAnswerer
        {
            internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public async Task<ReviewAnswerResultDto> SubmitAnswerAsync(SubmitReviewAnswerRequestDto request, CancellationToken cancellationToken = default)
            {
                Started.SetResult();
                await Release.Task;
                return new ReviewAnswerResultDto(false, "たべます", "たべます", true, null, null);
            }
        }
    }
}
