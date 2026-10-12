using Dictation.Core.Configuration;
using Dictation.Core.Kana;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证不重新进入首页的跨午夜连续作答，避免实际完成日缺少目标。</summary>
    public sealed class DailyPlanBoundaryTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task Review_PendingAnswerAcrossMidnightRecordsNewDayEvenWithoutRestart()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            var session = (await host.Services.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, token))!;
            var question = (await host.Services.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(session.SessionId, token))!;
            host.Clock.Value = host.Clock.Value.AddDays(1);
            await host.Services.GetRequiredService<IReviewSessionAnswerer>().SubmitAnswerAsync(new SubmitReviewAnswerRequestDto(session.SessionId, question.QuestionId, "错误"), token);
            var snapshot = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", "2026-10-13", token);
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.ReviewTarget);
        }

        [Fact]
        public async Task Kana_PendingAnswerAcrossMidnightRecordsNewDay()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var plan = await host.Services.GetRequiredService<IKanaPlanReader>().LoadAsync(token);
            await host.Services.GetRequiredService<IKanaPlanEditor>().SaveAsync(plan with { Enabled = true, DailyTarget = 3 }, token);
            var operations = host.Services.GetRequiredService<IKanaSessionOperations>();
            var session = (await operations.StartOrResumeAsync(token))!;
            var question = (await host.Services.GetRequiredService<IKanaSessionReader>().GetCurrentQuestionAsync(session.SessionId, token))!;
            host.Clock.Value = host.Clock.Value.AddDays(1);
            await operations.SubmitAsync(session.SessionId, question.QuestionOrdinal, "错误", token);
            var snapshot = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", "2026-10-13", token);
            Assert.NotNull(snapshot);
            Assert.Equal(3, snapshot.KanaTarget);
        }

        [Fact]
        public async Task Learning_ReadPendingQuestionAcrossMidnightRecordsNewDay()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var group = await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await host.ExecuteAsync("UPDATE WordGroups SET Placement='Learning' WHERE WordGroupId=@group;", new { group });
            var operations = host.Services.GetRequiredService<ILearningSessionOperations>();
            var reader = host.Services.GetRequiredService<ILearningSessionOperations>();
            var session = await operations.StartOrResumeAsync(new StartLearningSessionRequestDto(LearningLanguage.Japanese), token);
            await reader.GetCurrentQuestionAsync(session.SessionId, token);
            host.Clock.Value = host.Clock.Value.AddDays(1);
            Assert.NotNull(await reader.GetCurrentQuestionAsync(session.SessionId, token));
            Assert.Equal(1, (await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", "2026-10-13", token))!.LearningTarget);
        }

        [Fact]
        public async Task EmptyLearning_DoesNotRecordDailyGoals()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var session = await host.Services.GetRequiredService<ILearningSessionOperations>().StartOrResumeAsync(new StartLearningSessionRequestDto(LearningLanguage.Japanese), token);
            Assert.Equal(LearningSessionStatus.Completed, session.Status);
            Assert.Null(await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token));
        }
    }
}
