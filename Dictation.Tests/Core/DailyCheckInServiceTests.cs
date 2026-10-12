using Dictation.Core.Configuration;
using Dictation.Core.Statistics;
using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证客观完成事实、真实目标及语言隔离，不以启动或空日冒充打卡。</summary>
    public sealed class DailyCheckInServiceTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;
        [Theory]
        [InlineData(5, 4, false)]
        [InlineData(5, 5, true)]
        [InlineData(5, 6, true)]
        [InlineData(1, 0, false)]
        [InlineData(1, 1, true)]
        [InlineData(0, 0, false)]
        public async Task GetToday_UsesActualTargetAndRequiresCompletedWork(int target, int completed, bool expected)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.Services.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(
                new DailyPlanSnapshotPo("Japanese", StatisticsTestHost.Today, 0, target, 0), token);
            for (var index = 0; index < completed; index++) await host.SeedReviewAsync($"g{index}", $"s{index}");
            var actual = await host.Services.GetRequiredService<IDailyCheckInReader>().GetTodayAsync(LearningLanguage.Japanese, token);
            Assert.Equal(expected, actual.IsCheckedIn);
            Assert.Equal(completed, actual.ReviewCompleted);
            Assert.Equal(target, actual.ReviewTarget);
            Assert.Equal(new DateOnly(2026, 10, 12), actual.LocalDate);
        }

        [Fact]
        public async Task GetToday_LearningAndKanaMustAlsoReachTheirTargets()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.Services.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 1, 1), token);
            await host.SeedReviewAsync("r", "review");
            var reader = host.Services.GetRequiredService<IDailyCheckInReader>();
            Assert.False((await reader.GetTodayAsync(LearningLanguage.Japanese, token)).IsCheckedIn);
            await host.SeedLearningAsync("l");
            Assert.False((await reader.GetTodayAsync(LearningLanguage.Japanese, token)).IsCheckedIn);
            await host.SeedKanaAsync("kana");
            Assert.True((await reader.GetTodayAsync(LearningLanguage.Japanese, token)).IsCheckedIn);
        }

        [Fact]
        public async Task GetToday_DoesNotUseJapaneseWorkForEnglishOrInventHistory()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("jp");
            var value = await host.Services.GetRequiredService<IDailyCheckInReader>().GetTodayAsync(LearningLanguage.English, token);
            Assert.False(value.HasSnapshot);
            Assert.False(value.IsCheckedIn);
            Assert.Equal(0, value.LearningCompleted);
            Assert.Equal(0, value.KanaCompleted);
        }

        [Fact]
        public async Task GetToday_DeleteGroupWithdrawsCheckIn()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.Services.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 0, 0), token);
            await host.SeedLearningAsync("g");
            var reader = host.Services.GetRequiredService<IDailyCheckInReader>();
            Assert.True((await reader.GetTodayAsync(LearningLanguage.Japanese, token)).IsCheckedIn);
            await host.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId='g';");
            Assert.False((await reader.GetTodayAsync(LearningLanguage.Japanese, token)).IsCheckedIn);
        }

        [Fact]
        public async Task GetToday_UsesCompletionDayNotSessionStartAndDoesNotCarryBadge()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.Services.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 0, 1, 0), token);
            await host.SeedReviewAsync("g", "old-session");
            var reader = host.Services.GetRequiredService<IDailyCheckInReader>();
            Assert.True((await reader.GetTodayAsync(LearningLanguage.Japanese, token)).IsCheckedIn);
            host.Clock.Value = host.Clock.Value.AddDays(1);
            var next = await reader.GetTodayAsync(LearningLanguage.Japanese, token);
            Assert.Equal(new DateOnly(2026, 10, 13), next.LocalDate);
            Assert.False(next.IsCheckedIn);
            Assert.Equal(0, next.ReviewCompleted);
        }

        [Fact]
        public async Task GetToday_RejectsInvalidLanguageAndPropagatesCancellation()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var reader = host.Services.GetRequiredService<IDailyCheckInReader>();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetTodayAsync((LearningLanguage)99, token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetTodayAsync(LearningLanguage.Japanese, new CancellationToken(true)));
        }
    }
}
