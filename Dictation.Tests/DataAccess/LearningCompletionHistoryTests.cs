using Dapper;
using Dictation.Core.Statistics;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.DataAccess
{
    /// <summary>验证完成日事实与原事务一起保存、补学不抹历史、升级不猜日期及整组删除。</summary>
    public sealed class LearningCompletionHistoryTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task RealAnswerCompletion_StoresOnlyOneFactForWholeGroup()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(token);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LearningCompletionDates WHERE WordGroupId=@group;", new { group }, cancellationToken: token)));
            Assert.Equal("2026-09-30", await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT LocalDate FROM LearningCompletionDates WHERE WordGroupId=@group;", new { group }, cancellationToken: token)));
        }

        [Fact]
        public async Task LaterProgressDate_DoesNotEraseEarlierCompletedDay()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.OpenAsync(token);
                await connection.ExecuteAsync(new CommandDefinition("UPDATE LearningGroupProgress SET CompletedLocalDate='2026-10-12';", cancellationToken: token));
            }
            var calendar = await environment.Provider.GetRequiredService<ICheckInCalendarReader>().LoadMonthAsync(2026, 9, token);
            Assert.Equal(1, calendar.Days[29].JapaneseLearningCompleted);
            Assert.True(calendar.Days[29].JapaneseIsCheckedIn);
        }

        [Fact]
        public async Task Upgrade14_BackfillsKnownDateButNotUncompletedGroup()
        {
            using var host = await StatisticsTestHost.CreateAsync();
            await host.SeedLearningAsync("known");
            await host.ExecuteAsync("""
                DROP TABLE LearningCompletionDates;
                DELETE FROM SchemaMigrations WHERE Version=15;
                INSERT INTO WordGroups VALUES('pending','Japanese','pending','Learning','2026-10-01T00:00:00Z');
                INSERT INTO LearningGroupProgress VALUES('pending','Japanese','2026-10-12',NULL);
                """);
            await host.Services.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(token);
            await using var connection = host.Connections.CreateConnection();
            await connection.OpenAsync(token);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LearningCompletionDates;", cancellationToken: token)));
            Assert.Equal("known", await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT WordGroupId FROM LearningCompletionDates;", cancellationToken: token)));
            Assert.Equal("2026-10-12", await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT LocalDate FROM LearningCompletionDates;", cancellationToken: token)));
        }

        [Fact]
        public async Task DeleteWholeGroup_CascadesCompletionFact()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(token);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LearningCompletionDates;", cancellationToken: token)));
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM WordGroups WHERE WordGroupId=@group;", new { group }, cancellationToken: token));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LearningCompletionDates;", cancellationToken: token)));
        }
    }
}
