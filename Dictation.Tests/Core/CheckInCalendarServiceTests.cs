using Dictation.Core.Statistics;
using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证月历完整日期、双语言独立资格与已有历史事实，不使用当前计划补造打卡。</summary>
    public sealed class CheckInCalendarServiceTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(2028, 2, 29)]
        [InlineData(2026, 4, 30)]
        [InlineData(9999, 12, 31)]
        [InlineData(1, 1, 31)]
        public async Task LoadMonth_ReturnsEveryDayAndNoEmptyDayBadge(int year, int month, int count)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var result = await host.Services.GetRequiredService<ICheckInCalendarReader>().LoadMonthAsync(year, month, token);
            Assert.Equal(count, result.Days.Count);
            Assert.Equal(new DateOnly(year, month, 1), result.Days[0].Date);
            Assert.Equal(new DateOnly(year, month, count), result.Days[^1].Date);
            Assert.All(result.Days, day => { Assert.False(day.JapaneseIsCheckedIn); Assert.False(day.EnglishIsCheckedIn); });
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(10000, 1)]
        [InlineData(2026, 0)]
        [InlineData(2026, 13)]
        public async Task LoadMonth_RejectsInvalidYearOrMonth(int year, int month)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => host.Services.GetRequiredService<ICheckInCalendarReader>().LoadMonthAsync(year, month, token));
        }

        [Fact]
        public async Task LoadMonth_SharedDateHasIndependentJapaneseAndEnglishBadge()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            await data.InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 0, 0), token);
            await data.InsertIfAbsentAsync(new("English", StatisticsTestHost.Today, 0, 2, 0), token);
            await host.SeedLearningAsync("jp");
            await host.SeedReviewAsync("en1", "s1", "English");
            var reader = host.Services.GetRequiredService<ICheckInCalendarReader>();
            var day = (await reader.LoadMonthAsync(2026, 10, token)).Days[11];
            Assert.True(day.JapaneseIsCheckedIn);
            Assert.False(day.EnglishIsCheckedIn);
            Assert.Equal(1, day.JapaneseLearningCompleted);
            Assert.Equal(1, day.EnglishReviewCompleted);
            await host.SeedReviewAsync("en2", "s2", "English");
            day = (await reader.LoadMonthAsync(2026, 10, token)).Days[11];
            Assert.True(day.JapaneseIsCheckedIn);
            Assert.True(day.EnglishIsCheckedIn);
        }

        [Fact]
        public async Task LoadMonth_DeleteRemovesHistoryAndReopeningKeepsOriginalTargets()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            await data.InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 0, 0), token);
            await host.SeedLearningAsync("g");
            var reader = host.Services.GetRequiredService<ICheckInCalendarReader>();
            Assert.True((await reader.LoadMonthAsync(2026, 10, token)).Days[11].JapaneseIsCheckedIn);
            await data.InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 100, 100, 100), token);
            Assert.True((await reader.LoadMonthAsync(2026, 10, token)).Days[11].JapaneseIsCheckedIn);
            await host.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId='g';");
            var day = (await reader.LoadMonthAsync(2026, 10, token)).Days[11];
            Assert.False(day.JapaneseIsCheckedIn);
            Assert.Equal(0, day.JapaneseLearningCompleted);
        }

        [Fact]
        public async Task LoadMonth_OldCompletionWithoutSnapshotDoesNotInventBadge()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("g");
            var day = (await host.Services.GetRequiredService<ICheckInCalendarReader>().LoadMonthAsync(2026, 10, token)).Days[11];
            Assert.Equal(1, day.JapaneseLearningCompleted);
            Assert.False(day.JapaneseIsCheckedIn);
        }

        [Fact]
        public async Task LoadMonth_FiltersNeighboringMonths()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.Services.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", "2026-09-30", 1, 0, 0), token);
            await host.SeedLearningAsync("g", date: "2026-09-30");
            var reader = host.Services.GetRequiredService<ICheckInCalendarReader>();
            Assert.True((await reader.LoadMonthAsync(2026, 9, token)).Days[29].JapaneseIsCheckedIn);
            Assert.All((await reader.LoadMonthAsync(2026, 10, token)).Days, day => Assert.Equal(0, day.JapaneseLearningCompleted));
        }

        [Fact]
        public async Task LoadMonth_KanaCompletesJapaneseOnly()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.Services.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 0, 0, 1), token);
            await host.SeedKanaAsync("k");
            var day = (await host.Services.GetRequiredService<ICheckInCalendarReader>().LoadMonthAsync(2026, 10, token)).Days[11];
            Assert.True(day.JapaneseIsCheckedIn);
            Assert.False(day.EnglishIsCheckedIn);
            Assert.Equal(1, day.JapaneseKanaCompleted);
        }

        [Fact]
        public async Task LoadMonth_PropagatesCancellation()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ICheckInCalendarReader>()
                .LoadMonthAsync(2026, 10, new CancellationToken(true)));
        }
    }
}
