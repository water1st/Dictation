using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    /// <summary>
    /// 验证每日学习进度按本地日期和当前计划组合，不把词形或答题次数当作词组数。
    /// </summary>
    public sealed class LearningProgressServiceTests
    {
        [Fact]
        public async Task GetTodayAsync_UsesLocalDateCurrentTargetAndDistinctGroups()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync();
            var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 4, 16, 30, 0, TimeSpan.Zero));
            var service = new LearningProgressService(
                DataAccessorTestFactory.CreateLearningSession(new SqliteConnectionFactory(environment.Paths)),
                environment.PlanReader,
                clock);
            var empty = await service.GetTodayAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(0, empty.StartedGroupCount);
            Assert.Equal(0, empty.CompletedGroupCount);
            Assert.Equal(20, empty.DailyTarget);

            await environment.Sessions.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                "UPDATE LearningGroupProgress SET FirstStartedLocalDate = @Date, CompletedLocalDate = @Date;",
                new { Date = "2026-09-05" });

            var completed = await service.GetTodayAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(1, completed.StartedGroupCount);
            Assert.Equal(1, completed.CompletedGroupCount);
            Assert.Equal(20, completed.DailyTarget);
            Assert.Equal(0, (await service.GetTodayAsync(
                LearningLanguage.English, TestContext.Current.CancellationToken)).CompletedGroupCount);

            clock.Value = clock.Value.AddDays(1);
            var tomorrow = await service.GetTodayAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(0, tomorrow.StartedGroupCount);
            Assert.Equal(0, tomorrow.CompletedGroupCount);
        }

        [Fact]
        public async Task GetTodayAsync_RejectsInvalidLanguageAndPreCancellation()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync();
            var service = new LearningProgressService(
                DataAccessorTestFactory.CreateLearningSession(new SqliteConnectionFactory(environment.Paths)),
                environment.PlanReader,
                TimeProvider.System);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetTodayAsync(
                (LearningLanguage)99, TestContext.Current.CancellationToken));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetTodayAsync(
                LearningLanguage.Japanese, cancellation.Token));
        }

        private sealed class MutableTimeProvider : TimeProvider
        {
            public MutableTimeProvider(DateTimeOffset value) => Value = value;

            public DateTimeOffset Value { get; set; }

            public override DateTimeOffset GetUtcNow() => Value;

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone(
                "UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
        }
    }
}
