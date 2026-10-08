using Dapper;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.DataAccess
{
    /// <summary>验证已有版本11用户历史升级后完整保留，且备份和新观察表真实可用。</summary>
    public sealed class LearningSharedMigrationTests
    {
        [Fact]
        public async Task Upgrade_FromVersionElevenPreservesOriginalAnswersMemoriesAndBackup()
        {
            var token = TestContext.Current.CancellationToken;
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(token);
            var memory = await connection.ExecuteScalarAsync<string>("SELECT DueAtUtc FROM ReviewFormMemories;");
            Assert.Equal(2, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            // 仅在隔离夹具恢复版本11的实际结构；生产升级绝不降级或删除用户表。
            await connection.ExecuteAsync("""
                DROP TABLE KanaAttempts;
                DROP TABLE KanaSessionItems;
                DROP TABLE KanaSessions;
                DROP TABLE KanaMemories;
                DROP TABLE KanaPlans;
                DROP TABLE LearningSharedAnswerAttempts;
                DELETE FROM SchemaMigrations WHERE Version>=12;
                """);
            var paths = environment.Provider.GetRequiredService<ApplicationPaths>();

            await new SchemaMigrationRunner(paths, environment.Connections).MigrateAsync(token);

            Assert.Equal(2, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            Assert.Equal(memory, await connection.ExecuteScalarAsync<string>("SELECT DueAtUtc FROM ReviewFormMemories;"));
            Assert.Equal("Review", await connection.ExecuteScalarAsync<string>("SELECT Placement FROM WordGroups WHERE WordGroupId=@Id;", new { Id = group }));
            Assert.Equal(13, await connection.ExecuteScalarAsync<int>("SELECT MAX(Version) FROM SchemaMigrations;"));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LearningSharedAnswerAttempts;"));
            Assert.Single(Directory.GetFiles(paths.Backups));
            Assert.Equal("ok", await connection.ExecuteScalarAsync<string>("PRAGMA integrity_check;"));
        }
    }
}
