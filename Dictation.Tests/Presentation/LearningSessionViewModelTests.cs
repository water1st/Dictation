using System.ComponentModel;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证学习页面启动、提交反馈、浏览播放及释放状态。
    /// </summary>
    public sealed class LearningSessionViewModelTests
    {
        [Fact]
        public async Task SubmitCommand_PersistsAnswerBeforeShowingFeedback()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync();
            using var viewModel = environment.CreateViewModel();
            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            var questionId = viewModel.Question!.QuestionId;
            viewModel.AnswerText = "たべます";

            await ExecuteAndWaitAsync(viewModel, viewModel.SubmitCommand);

            Assert.Equal(1L, await environment.CountAttemptsAsync());
            Assert.True(viewModel.Feedback?.WasCorrect);
            Assert.Equal("たべます", viewModel.Feedback?.CorrectAnswer);
            Assert.Equal(questionId, viewModel.Question.QuestionId);
            Assert.False(viewModel.SubmitCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public async Task StartAsync_ResumesSameQuestionWithFixedLanguage(LearningLanguage language)
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(language);
            using var first = environment.CreateViewModel();
            await first.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            using var resumed = environment.CreateViewModel();

            await resumed.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(first.Session!.SessionId, resumed.Session!.SessionId);
            Assert.Equal(first.Question!.QuestionId, resumed.Question!.QuestionId);
            Assert.Equal(language, resumed.SelectedLanguage);
            Assert.Null(resumed.Feedback);
            Assert.Null(resumed.Summary);
            Assert.Null(resumed.Question.SpokenText);
            Assert.Empty(environment.Tts.Requests);
            Assert.False(resumed.ReplayQuestionCommand.CanExecute(null));
            Assert.Throws<InvalidOperationException>(() => resumed.Initialize(
                language == LearningLanguage.Japanese ? LearningLanguage.English : LearningLanguage.Japanese));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public async Task EmptyAnswer_DoesNotEnableSubmitOrWriteAnAttempt(string answer)
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync();
            using var viewModel = environment.CreateViewModel();
            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            viewModel.AnswerText = answer;

            Assert.False(viewModel.SubmitCommand.CanExecute(null));
            await viewModel.SubmitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0L, await environment.CountAttemptsAsync());
            Assert.Null(viewModel.Feedback);
        }

        [Fact]
        public async Task WrongThenCorrectAnswers_RetryAndFinalSummaryKeepTheOriginalError()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync();
            using var viewModel = environment.CreateViewModel();
            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            viewModel.AnswerText = "忘记了";
            await viewModel.SubmitAsync(TestContext.Current.CancellationToken);

            Assert.False(viewModel.Feedback!.WasCorrect);
            Assert.Equal("たべます", viewModel.Feedback.CorrectAnswer);
            Assert.True(viewModel.Feedback.HadErrorInSession);
            for (var index = 0; index < 2; index++)
            {
                await viewModel.ContinueAsync(TestContext.Current.CancellationToken);
                Assert.Null(viewModel.Feedback);
                Assert.Equal(string.Empty, viewModel.AnswerText);
                viewModel.AnswerText = "たべます";
                await viewModel.SubmitAsync(TestContext.Current.CancellationToken);
            }
            await viewModel.ContinueAsync(TestContext.Current.CancellationToken);

            Assert.True(viewModel.IsCompleted);
            Assert.Null(viewModel.Question);
            Assert.Equal(3, viewModel.Summary!.AnsweredCount);
            Assert.Equal("たべます", Assert.Single(viewModel.Summary.Errors).CorrectAnswer);
            Assert.Equal(3L, await environment.CountAttemptsAsync());
            Assert.Null(await environment.Sessions.GetActiveAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task BrowseAndReplay_DoNotChangeTheQuestionOrProgress()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync();
            using var viewModel = environment.CreateViewModel();
            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            var original = viewModel.Question;
            viewModel.AnswerText = "尚未提交";

            viewModel.OpenBrowseCommand.Execute(null);
            Assert.True(viewModel.IsBrowsing);
            Assert.False(viewModel.SubmitCommand.CanExecute(null));
            var member = Assert.Single(viewModel.BrowseMembers);
            Assert.Equal("食べる", member.Headword);
            Assert.Contains("吃", member.Meanings);
            Assert.NotEmpty(member.PartOfSpeechLabel);
            Assert.NotEmpty(member.AttributeLabel);
            Assert.All(viewModel.BrowseForms, form => Assert.Equal("Polite", form.FormCode));
            await viewModel.ReplayBrowseAsync(TestContext.Current.CancellationToken);
            viewModel.CloseBrowseCommand.Execute(null);

            Assert.False(viewModel.IsBrowsing);
            Assert.Same(original, viewModel.Question);
            Assert.Equal("尚未提交", viewModel.AnswerText);
            Assert.Equal(0L, await environment.CountAttemptsAsync());
            Assert.Equal(new TtsSynthesisRequestDto(TtsLanguage.Japanese, "たべます"),
                Assert.Single(environment.Tts.Requests));
            Assert.Equal(LearningPresentationTestEnvironment.ControlledTtsProvider.Wav,
                Assert.Single(environment.Audio.Played));
        }

        [Theory]
        [InlineData(LearningLanguage.Japanese, TtsLanguage.Japanese, "たべます")]
        [InlineData(LearningLanguage.English, TtsLanguage.English, "take off")]
        public async Task Dictation_PlaysTheActualTargetBeforeAcceptingAnswers(
            LearningLanguage language, TtsLanguage ttsLanguage, string expectedText)
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(language, dictation: true);
            using var viewModel = environment.CreateViewModel();

            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            viewModel.AnswerText = expectedText;

            Assert.Equal(new TtsSynthesisRequestDto(ttsLanguage, expectedText), Assert.Single(environment.Tts.Requests));
            Assert.Single(environment.Audio.Played);
            Assert.True(viewModel.SubmitCommand.CanExecute(null));
            Assert.True(viewModel.ReplayQuestionCommand.CanExecute(null));
            Assert.Equal(0L, await environment.CountAttemptsAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task FailedSpeechOrPlayback_DoesNotJudgeAndCanBeRetried(bool synthesisFailure)
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(dictation: true);
            if (synthesisFailure) environment.Tts.Failure = new InvalidOperationException("合成不可用");
            else environment.Audio.Failure = new InvalidOperationException("音频设备不可用");
            using var viewModel = environment.CreateViewModel();

            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            var originalId = viewModel.Question!.QuestionId;
            viewModel.AnswerText = "不应该被判错";
            await viewModel.SubmitAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(viewModel.ErrorMessage);
            Assert.Null(viewModel.Feedback);
            Assert.False(viewModel.SubmitCommand.CanExecute(null));
            Assert.Equal(0L, await environment.CountAttemptsAsync());
            environment.Tts.Failure = null;
            environment.Audio.Failure = null;
            await viewModel.ReplayQuestionAsync(TestContext.Current.CancellationToken);
            Assert.Null(viewModel.ErrorMessage);
            Assert.True(viewModel.SubmitCommand.CanExecute(null));
            Assert.Equal(originalId, viewModel.Question.QuestionId);
        }

        [Fact]
        public async Task CancelPlayback_ReleasesBusyGateAndLeavesThePendingQuestion()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(dictation: true);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            environment.Tts.BeforeReturnAsync = async token =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            };
            using var viewModel = environment.CreateViewModel();
            var start = viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
            var original = viewModel.Question;
            viewModel.AnswerText = "たべます";
            Assert.True(viewModel.IsBusy);
            Assert.False(viewModel.SubmitCommand.CanExecute(null));
            await viewModel.SubmitAsync(TestContext.Current.CancellationToken);

            viewModel.CancelCommand.Execute(null);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);

            Assert.False(viewModel.IsBusy);
            Assert.Same(original, viewModel.Question);
            Assert.Null(viewModel.ErrorMessage);
            Assert.Equal(0L, await environment.CountAttemptsAsync());
            environment.Tts.BeforeReturnAsync = null;
            await viewModel.ReplayQuestionAsync(TestContext.Current.CancellationToken);
            Assert.True(viewModel.SubmitCommand.CanExecute(null));
        }

        [Fact]
        public async Task Dispose_CancelsPlaybackAndRejectsFurtherOperations()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(dictation: true);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            environment.Tts.BeforeReturnAsync = async token =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            };
            var viewModel = environment.CreateViewModel();
            var start = viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

            viewModel.Dispose();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => viewModel.ReplayQuestionAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(viewModel.SubmitCommand.CanExecute(null));
            Assert.Equal(0L, await environment.CountAttemptsAsync());
        }

        [Fact]
        public async Task NoEligibleWords_ShowsAnEmptyCompletedSession()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(importWord: false);
            using var viewModel = environment.CreateViewModel();

            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(viewModel.IsCompleted);
            Assert.Empty(viewModel.Session!.WordGroupIds);
            Assert.Equal(0, viewModel.Summary!.AnsweredCount);
            Assert.Empty(viewModel.BrowseMembers);
            Assert.False(viewModel.SubmitCommand.CanExecute(null));
            Assert.Empty(environment.Tts.Requests);
        }

        private static async Task ExecuteAndWaitAsync(LearningSessionViewModel viewModel, ICommand command)
        {
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sawBusy = false;
            void OnChanged(object? sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName != nameof(LearningSessionViewModel.IsBusy)) return;
                if (viewModel.IsBusy) sawBusy = true;
                else if (sawBusy) finished.TrySetResult();
            }

            viewModel.PropertyChanged += OnChanged;
            try
            {
                Assert.True(command.CanExecute(null));
                command.Execute(null);
                await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                viewModel.PropertyChanged -= OnChanged;
            }
        }
    }
}
