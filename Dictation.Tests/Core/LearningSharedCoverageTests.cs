using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证共享听写不会替代各义项独立的看义练习或膨胀真实答题数。</summary>
    public sealed class LearningSharedCoverageTests
    {
        [Fact]
        public async Task MixedChannels_SharedDictationStillRequiresMeaningWritingForEachSense()
        {
            var token = TestContext.Current.CancellationToken;
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync();
            await environment.ImportAsync(sense: "jmdict:1:2");
            await environment.Provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation }, new[] { LearningOutputMode.Kana }), token);
            var session = await environment.Sessions.StartOrResumeAsync(new(LearningLanguage.Japanese), token);
            LearningSessionSummaryDto? summary = null;
            for (var index = 0; index < 20 && summary is null; index++)
            {
                var question = Assert.IsType<PracticeQuestionDto>(await environment.Sessions.GetCurrentQuestionAsync(session.SessionId, token));
                summary = (await environment.Sessions.SubmitAnswerAsync(new(session.SessionId, question.QuestionId, "たべる"), token)).Summary;
            }
            Assert.NotNull(summary);
            Assert.Equal(3, summary.AnsweredCount);
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(token);
            var states = await connection.QueryAsync("SELECT MeaningWritingCount,DictationCount FROM LearningFormStates;");
            Assert.Equal(2, states.Count());
            Assert.All(states, state =>
            {
                Assert.Equal(1L, (long)state.MeaningWritingCount);
                Assert.Equal(1L, (long)state.DictationCount);
            });
        }
    }
}
