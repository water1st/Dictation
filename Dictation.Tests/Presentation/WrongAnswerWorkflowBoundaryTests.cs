using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Practice;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证错题页面的模式适配真实允许主动重练，而非只更换标题的普通到期入口。</summary>
    public sealed class WrongAnswerWorkflowBoundaryTests
    {
        [Fact]
        public Task Start_FutureDueErrorProducesQuestionInsteadOfEmptyOrdinaryReview() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path, configure: services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
            });
            var group = await ReviewPlanPresentationTestHost.ImportAsync(provider);
            await provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                ReviewRangeMode.All, Array.Empty<string>(), new[] { "Dictionary" }, new[] { LearningChannel.Dictation },
                new[] { LearningOutputMode.Kana }, EnglishFormLabelMode.GrammarName, null), token);
            var session = Assert.IsType<ReviewSessionDto>(await provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, token));
            var questions = provider.GetRequiredService<IReviewQuestionReader>();
            var answerer = provider.GetRequiredService<IReviewSessionAnswerer>();
            var first = Assert.IsType<Dictation.Core.Practice.PracticeQuestionDto>(await questions.GetCurrentQuestionAsync(session.SessionId, token));
            var result = await answerer.SubmitAnswerAsync(new(session.SessionId, first.QuestionId, "错误"), token);
            while (result.NextQuestion is { } next)
                result = await answerer.SubmitAnswerAsync(new(session.SessionId, next.QuestionId, "たべる"), token);
            Assert.Null(await provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, token));
            var vm = provider.GetRequiredKeyedService<PracticeSessionViewModel>(PracticeSessionMode.WrongAnswers);
            vm.Initialize(LearningLanguage.Japanese);
            await vm.StartAsync(new[] { group }, token);
            Assert.False(vm.IsEmptyVisible);
            Assert.NotNull(vm.Question);
            Assert.Equal(group, Assert.Single(vm.Session!.WordGroupIds));
            Assert.Equal("Dictionary", vm.Question.FormCode);
        });
    }
}
