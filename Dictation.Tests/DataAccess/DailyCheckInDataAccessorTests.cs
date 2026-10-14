using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.DataAccess
{
    /// <summary>验证每日快照唯一性和真实表查询的去重、级联及非法输入边界。</summary>
    public sealed class DailyCheckInDataAccessorTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;
        [Fact]
        public async Task InsertIfAbsent_KeepsFirstTargetsAcrossConcurrencyAndReopen()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            await data.InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 2, 5, 1), token);
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => data.InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 99, 99, 99), token)));
            var reopened = new DailyCheckInDataAccessor(host.Connections);
            var snapshot = await reopened.GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token);
            Assert.NotNull(snapshot);
            Assert.Equal(2, snapshot.LearningTarget);
            Assert.Equal(5, snapshot.ReviewTarget);
            Assert.Equal(1, snapshot.KanaTarget);
        }

        [Fact]
        public async Task GetCounts_DistinctGroupsAndKanaAcrossMultipleSessions()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("g");
            await host.SeedReviewAsync("g", "s1");
            await host.SeedReviewAsync("g", "s2");
            await host.SeedKanaAsync("k1");
            await host.SeedKanaAsync("k2");
            var counts = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetCompletionCountsAsync("Japanese", StatisticsTestHost.Today, token);
            Assert.Equal(1, counts.LearningCompleted);
            Assert.Equal(1, counts.ReviewCompleted);
            Assert.Equal(1, counts.KanaCompleted);
        }

        [Fact]
        public async Task GetCounts_FiltersLanguageAndDateAndKeepsKanaJapaneseOnly()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("jp");
            await host.SeedLearningAsync("en", "English");
            await host.SeedReviewAsync("old", "s", "English", "2026-10-11");
            await host.SeedKanaAsync("k");
            var counts = await host.Services.GetRequiredService<DailyCheckInDataAccessor>().GetCompletionCountsAsync("English", StatisticsTestHost.Today, token);
            Assert.Equal(1, counts.LearningCompleted);
            Assert.Equal(0, counts.ReviewCompleted);
            Assert.Equal(0, counts.KanaCompleted);
        }

        [Theory]
        [InlineData("Chinese", "2026-10-12")]
        [InlineData("Japanese", "2026-02-30")]
        [InlineData("Japanese", "2026-1-1")]
        public async Task Reads_RejectInvalidLanguageOrDate(string language, string date)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => data.GetSnapshotAsync(language, date, token));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => data.GetCompletionCountsAsync(language, date, token));
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, -1, 0)]
        [InlineData(0, 0, -1)]
        public async Task Insert_RejectsNegativeTargetsWithoutWriting(long learning, long review, long kana)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => data.InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, learning, review, kana), token));
            Assert.Null(await data.GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token));
        }

        [Fact]
        public async Task Insert_CancellationDoesNotWriteSnapshot()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            var data = host.Services.GetRequiredService<DailyCheckInDataAccessor>();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => data.InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 1, 0), new CancellationToken(true)));
            Assert.Null(await data.GetSnapshotAsync("Japanese", StatisticsTestHost.Today, token));
        }
    }
}
