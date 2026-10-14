using Dictation.Core.Configuration;
using Dictation.Core.Kana;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.Core.Statistics;
using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证实际出题范围与首次目标冻结，不因每轮数量、计划改动或新日期错误计数。</summary>
    public sealed class DailyPlanSnapshotServiceTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task RecordToday_UsesAvailableGroupsRatherThanPlanQuota()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await host.Services.GetRequiredService<IDailyPlanSnapshotRecorder>().RecordTodayAsync(LearningLanguage.Japanese, token);
            var value = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token);
            Assert.NotNull(value);
            Assert.Equal(1, value.ReviewTarget);
            Assert.Equal(0, value.LearningTarget);
            Assert.Equal(0, value.KanaTarget);
        }

        [Fact]
        public async Task RecordToday_DoesNotCapAvailabilityAtSessionLimit()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services, "食前", "jmdict:2:1");
            await SaveReviewAsync(host, 20, 1);
            await host.Services.GetRequiredService<IDailyPlanSnapshotRecorder>().RecordTodayAsync(LearningLanguage.Japanese, token);
            var value = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token);
            Assert.Equal(2, value!.ReviewTarget);
        }

        [Fact]
        public async Task RecordToday_DoesNotIncludeFutureMemoriesOrCreateEmptySnapshot()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await host.ExecuteAsync("""
                INSERT INTO ReviewFormMemories(WordGroupId,MemberId,FormCode,Stability,Difficulty,LastReviewedAtUtc,DueAtUtc,ReviewCount,LapseCount)
                SELECT G.WordGroupId,M.MemberId,F.FormCode,5,5,'2026-10-11T00:00:00Z','2026-10-30T00:00:00Z',1,0
                FROM WordGroups G JOIN WordGroupMembers M ON M.WordGroupId=G.WordGroupId
                JOIN WordGroupForms F ON F.MemberId=M.MemberId GROUP BY G.WordGroupId,M.MemberId,F.FormCode;
                """);
            await host.Services.GetRequiredService<IDailyPlanSnapshotRecorder>().RecordTodayAsync(LearningLanguage.Japanese, token);
            Assert.Null(await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token));
        }

        [Fact]
        public async Task RecordToday_KeepsFirstTargetsDespiteSameDayPlanChange()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services, "食前", "jmdict:2:1");
            await SaveReviewAsync(host, 1, 1);
            var recorder = host.Services.GetRequiredService<IDailyPlanSnapshotRecorder>();
            await recorder.RecordTodayAsync(LearningLanguage.Japanese, token);
            await SaveReviewAsync(host, 20, 20);
            await recorder.RecordTodayAsync(LearningLanguage.Japanese, token);
            Assert.Equal(1, (await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token))!.ReviewTarget);
            host.Clock.Value = host.Clock.Value.AddDays(1);
            await recorder.RecordTodayAsync(LearningLanguage.Japanese, token);
            Assert.Equal(2, (await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", "2026-10-13", token))!.ReviewTarget);
        }

        [Fact]
        public async Task StartOrResume_RecordsTargetsWithoutAnsweringAndHandlesMidnight()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            var sessions = host.Services.GetRequiredService<IReviewSessionStarter>();
            var first = await sessions.StartOrResumeAsync(LearningLanguage.Japanese, token);
            Assert.NotNull(first);
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            Assert.NotNull(await data.GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token));
            Assert.False((await host.Services.GetRequiredService<IDailyCheckInReader>().GetTodayAsync(LearningLanguage.Japanese, token)).IsCheckedIn);
            host.Clock.Value = host.Clock.Value.AddDays(1);
            var resumed = await sessions.StartOrResumeAsync(LearningLanguage.Japanese, token);
            Assert.Equal(first.SessionId, resumed!.SessionId);
            Assert.Equal(1, (await data.GetSnapshotAsync("Japanese", "2026-10-13", token))!.ReviewTarget);
        }

        [Fact]
        public async Task RecordToday_CompletedAndStillDueSameGroupDoesNotCountTwice()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var id = await ReviewPlanPresentationTestHost.ImportAsync(host.Services);
            await host.SeedReviewAsync(id, "completed");
            await host.Services.GetRequiredService<IDailyPlanSnapshotRecorder>().RecordTodayAsync(LearningLanguage.Japanese, token);
            Assert.Equal(1, (await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token))!.ReviewTarget);
        }

        [Fact]
        public async Task RecordToday_KanaOnlyTargetsDoNotAppearInEnglish()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var plan = await host.Services.GetRequiredService<IKanaPlanReader>().LoadAsync(token);
            await host.Services.GetRequiredService<IKanaPlanEditor>().SaveAsync(plan with { Enabled = true, DailyTarget = 3 }, token);
            var recorder = host.Services.GetRequiredService<IDailyPlanSnapshotRecorder>();
            await recorder.RecordTodayAsync(LearningLanguage.Japanese, token);
            await recorder.RecordTodayAsync(LearningLanguage.English, token);
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            Assert.Equal(3, (await data.GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token))!.KanaTarget);
            Assert.Null(await data.GetSnapshotAsync("English", StatisticsTestHost.Today, token));
        }

        [Fact]
        public async Task RecordToday_RejectsInvalidLanguageAndCancellation()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var recorder = host.Services.GetRequiredService<IDailyPlanSnapshotRecorder>();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => recorder.RecordTodayAsync((LearningLanguage)99, token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recorder.RecordTodayAsync(LearningLanguage.Japanese, new CancellationToken(true)));
        }

        private static async Task SaveReviewAsync(StatisticsTestHost host, int target, int limit)
        {
            var plan = await host.Services.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, token);
            await host.Services.GetRequiredService<IReviewPlanEditor>().SaveAsync(new SaveReviewPlanRequestDto(plan.Language, target, limit,
                plan.RangeMode, plan.WordGroupIds, plan.FormCodes, plan.Channels, plan.OutputModes, plan.EnglishFormLabelMode, plan.EnglishExampleSourceSenseId), token);
        }
    }
}
