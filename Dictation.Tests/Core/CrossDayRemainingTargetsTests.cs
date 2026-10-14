using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.Core.Statistics;
using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>通过真实作答验证昨日部分完成的轮次只把剩余任务带到今天。</summary>
    public sealed class CrossDayRemainingTargetsTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Resume_CompletedYesterdayDoesNotMakeTodayTargetUnreachable(bool review)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services, "食前", "jmdict:2:1");
            string sessionId;
            if (review)
            {
                var plan = await host.Services.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, Token);
                await host.Services.GetRequiredService<IReviewPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                    plan.RangeMode, plan.WordGroupIds, new[] { "Dictionary" }, new[] { LearningChannel.Dictation },
                    new[] { LearningOutputMode.Kana }, plan.EnglishFormLabelMode, plan.EnglishExampleSourceSenseId), Token);
                var session = await host.Services.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token);
                Assert.NotNull(session);
                sessionId = session.SessionId;
            }
            else
            {
                await host.ExecuteAsync("UPDATE WordGroups SET Placement='Learning';");
                await host.Services.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                    LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Dictionary" },
                    new[] { LearningChannel.Dictation }, new[] { LearningOutputMode.Kana }), Token);
                sessionId = (await host.Services.GetRequiredService<ILearningSessionOperations>().StartOrResumeAsync(new(LearningLanguage.Japanese), Token)).SessionId;
            }
            var question = await ReadAsync(host, review, sessionId);
            for (var index = 0; index < 100 && question is not null && await CompletedAsync(host, review, sessionId) == 0; index++)
                question = await AnswerAsync(host, review, sessionId, question);
            Assert.Equal(1, await CompletedAsync(host, review, sessionId));
            Assert.NotNull(question);

            host.Clock.Value = host.Clock.Value.AddDays(1);
            // 恢复合同仍包含整个轮次，不能为修复打卡而偷偷修改浏览或结算范围。
            if (review)
            {
                var resumed = await host.Services.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token);
                Assert.NotNull(resumed);
                Assert.Equal(sessionId, resumed.SessionId);
                Assert.Equal(2, resumed.WordGroupIds.Count);
            }
            else
            {
                var resumed = await host.Services.GetRequiredService<ILearningSessionOperations>().StartOrResumeAsync(new(LearningLanguage.Japanese), Token);
                Assert.Equal(sessionId, resumed.SessionId);
                Assert.Equal(2, resumed.WordGroupIds.Count);
            }
            var target = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", "2026-10-13", Token);
            Assert.NotNull(target);
            Assert.Equal(1, review ? target.ReviewTarget : target.LearningTarget);
            for (var index = 0; index < 100 && question is not null; index++) question = await AnswerAsync(host, review, sessionId, question);
            Assert.Null(question);
            var today = await host.Services.GetRequiredService<IDailyCheckInReader>().GetTodayAsync(LearningLanguage.Japanese, Token);
            Assert.Equal(1, review ? today.ReviewCompleted : today.LearningCompleted);
            Assert.True(today.IsCheckedIn);
        }

        private static Task<PracticeQuestionDto?> ReadAsync(StatisticsTestHost host, bool review, string id) => review
            ? host.Services.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(id, Token)
            : host.Services.GetRequiredService<ILearningSessionOperations>().GetCurrentQuestionAsync(id, Token);

        private static async Task<PracticeQuestionDto?> AnswerAsync(StatisticsTestHost host, bool review, string id, PracticeQuestionDto question) => review
            ? (await host.Services.GetRequiredService<IReviewSessionAnswerer>().SubmitAnswerAsync(new(id, question.QuestionId, question.SpokenText), Token)).NextQuestion
            : (await host.Services.GetRequiredService<ILearningSessionOperations>().SubmitAnswerAsync(new(id, question.QuestionId, question.SpokenText), Token)).NextQuestion;

        private static async Task<long> CompletedAsync(StatisticsTestHost host, bool review, string id)
        {
            await using var connection = host.Connections.CreateConnection();
            await connection.OpenAsync(Token);
            var sql = review ? "SELECT COUNT(*) FROM ReviewSessionGroups WHERE SessionId=@id AND CompletedLocalDate IS NOT NULL;"
                : "SELECT COUNT(*) FROM LearningSessionGroups S JOIN LearningGroupProgress P ON P.WordGroupId=S.WordGroupId WHERE S.SessionId=@id AND P.CompletedLocalDate IS NOT NULL;";
            return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { id }, cancellationToken: Token));
        }
    }
}
