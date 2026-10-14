using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.DataAccess
{
    /// <summary>验证月区间真实SQL的最大日期、去重和可空目标边界。</summary>
    public sealed class CalendarMonthQueryTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task LoadMonth_IncludesLastSupportedDateWithoutOverflow()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("max", date: "9999-12-31");
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            await data.InsertIfAbsentAsync(new("Japanese", "9999-12-31", 1, 0, 0), token);
            var row = Assert.Single(await data.LoadMonthAsync(9999, 12, token));
            Assert.Equal("9999-12-31", row.LocalDate);
            Assert.Equal(1, row.LearningCompleted);
            Assert.Equal(1L, row.LearningTarget);
        }

        [Fact]
        public async Task LoadMonth_UnionsLegacyAndFactsWithoutDoubleCountingSessions()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("g");
            await host.ExecuteAsync("INSERT INTO LearningCompletionDates VALUES('g','2026-10-12');");
            await host.SeedReviewAsync("g", "s1");
            await host.SeedReviewAsync("g", "s2");
            await host.SeedKanaAsync("k1");
            await host.SeedKanaAsync("k2");
            var row = Assert.Single(await host.Services.GetRequiredService<DailyCheckInDataAccessor>().LoadMonthAsync(2026, 10, token));
            Assert.Equal("Japanese", row.Language);
            Assert.Equal(1, row.LearningCompleted);
            Assert.Equal(1, row.ReviewCompleted);
            Assert.Equal(1, row.KanaCompleted);
            Assert.Null(row.LearningTarget);
        }

        [Fact]
        public async Task LoadMonth_PreservesEnglishIsolationAndNullableTargets()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("en", "English");
            await host.SeedKanaAsync("k");
            var rows = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().LoadMonthAsync(2026, 10, token);
            var english = Assert.Single(rows, row => row.Language == "English");
            Assert.Equal(1, english.LearningCompleted);
            Assert.Equal(0, english.KanaCompleted);
            Assert.Null(english.LearningTarget);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Services.GetRequiredService<DailyCheckInDataAccessor>().LoadMonthAsync(2026, 10, new CancellationToken(true)));
        }
    }
}
