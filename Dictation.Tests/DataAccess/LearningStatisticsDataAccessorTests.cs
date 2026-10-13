using Dictation.DataAccess.Statistics;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.DataAccess
{
    /// <summary>用隔离 SQLite 验证已学词组与历史完成日计数，而不是导入量。</summary>
    public sealed class LearningStatisticsDataAccessorTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static LearningStatisticsDataAccessor Reader(StatisticsTestHost host) => host.Services.GetRequiredService<LearningStatisticsDataAccessor>();

        [Fact]
        public async Task Load_ImportedGroupWithoutCompletionIsNotLearned()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.ExecuteAsync("INSERT INTO WordGroups VALUES('old','Japanese','old','Review','2026-10-01');");
            var result = await Reader(host).LoadAsync("Japanese", StatisticsTestHost.Today, Token);
            Assert.Equal(0, result.LearnedGroupCount);
            Assert.Equal(0, result.TodayLearnedGroupCount);
        }

        [Fact]
        public async Task Load_MultipleCompletionDatesAndLegacyProgressCountGroupOnce()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("one");
            await host.ExecuteAsync("INSERT INTO LearningCompletionDates VALUES('one','2026-10-11'),('one','2026-10-12');");
            var result = await Reader(host).LoadAsync("Japanese", StatisticsTestHost.Today, Token);
            Assert.Equal(1, result.LearnedGroupCount);
            Assert.Equal(1, result.TodayLearnedGroupCount);
        }

        [Fact]
        public async Task Load_HistoryKeepsOriginalDayAfterSupplementaryLearning()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("one", date: "2026-10-13");
            await host.ExecuteAsync("INSERT INTO LearningCompletionDates VALUES('one','2026-10-12');");
            Assert.Equal(1, (await Reader(host).LoadAsync("Japanese", StatisticsTestHost.Today, Token)).TodayLearnedGroupCount);
        }

        [Fact]
        public async Task Load_DeletionRetractsHistoryAndTotal()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("one");
            await host.ExecuteAsync("INSERT INTO LearningCompletionDates VALUES('one','2026-10-12');");
            Assert.Equal(1, (await Reader(host).LoadAsync("Japanese", StatisticsTestHost.Today, Token)).LearnedGroupCount);
            await host.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId='one';");
            var result = await Reader(host).LoadAsync("Japanese", StatisticsTestHost.Today, Token);
            Assert.Equal(0, result.LearnedGroupCount);
            Assert.Equal(0, result.TodayLearnedGroupCount);
        }

        [Fact]
        public async Task Load_SeparatesLanguagesAndExcludesKana()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("jp");
            await host.SeedLearningAsync("en", "English", "2026-10-11");
            await host.SeedKanaAsync("kana");
            Assert.Equal(1, (await Reader(host).LoadAsync("Japanese", StatisticsTestHost.Today, Token)).LearnedGroupCount);
            var en = await Reader(host).LoadAsync("English", StatisticsTestHost.Today, Token);
            Assert.Equal(1, en.LearnedGroupCount);
            Assert.Equal(0, en.TodayLearnedGroupCount);
        }

        [Theory]
        [InlineData("Chinese", "2026-10-12")]
        [InlineData("Japanese", "2026-02-30")]
        [InlineData("English", "2026-1-1")]
        public async Task Load_RejectsInvalidQuery(string language, string date)
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await Assert.ThrowsAnyAsync<ArgumentException>(() => Reader(host).LoadAsync(language, date, Token));
        }

        [Fact]
        public async Task Load_CanceledQueryDoesNotReturnZeroSuccess()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(host).LoadAsync("Japanese", StatisticsTestHost.Today, canceled.Token));
        }
    }
}
