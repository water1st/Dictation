using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证学习模式直接提交跨日旧题也有当天目标，不能依赖先读取下一题。</summary>
    public sealed class LearningDailyCompletionBoundaryTests
    {
        [Fact]
        public async Task SubmitPendingAnswer_AcrossMidnightRecordsActualDayTargets()
        {
            var token = TestContext.Current.CancellationToken;
            using var host = await StatisticsTestHost.CreateAsync();
            var group = await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await host.ExecuteAsync("UPDATE WordGroups SET Placement='Learning' WHERE WordGroupId=@group;", new { group });
            var operations = host.Services.GetRequiredService<ILearningSessionOperations>();
            var session = await operations.StartOrResumeAsync(new StartLearningSessionRequestDto(LearningLanguage.Japanese), token);
            var question = (await operations.GetCurrentQuestionAsync(session.SessionId, token))!;
            host.Clock.Value = host.Clock.Value.AddDays(1);
            await operations.SubmitAnswerAsync(new SubmitLearningAnswerRequestDto(session.SessionId, question.QuestionId, "错误"), token);
            var snapshot = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", "2026-10-13", token);
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.LearningTarget);
        }
    }
}
