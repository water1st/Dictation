using Dapper;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>为统计测试建立真实三层容器、固定本地时钟和隔离用户数据库。</summary>
    internal sealed class StatisticsTestHost : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        internal ServiceProvider Services { get; private set; } = null!;
        internal StatisticsTestClock Clock { get; } = new();
        internal SqliteConnectionFactory Connections => Services.GetRequiredService<SqliteConnectionFactory>();
        internal const string Today = "2026-10-12";

        internal static async Task<StatisticsTestHost> CreateAsync()
        {
            var host = new StatisticsTestHost();
            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
                services.AddDictationDataAccess(host.directory.Path).AddDictationCore();
                services.AddSingleton<TimeProvider>(host.Clock);
                host.Services = services.BuildServiceProvider();
                var paths = host.Services.GetRequiredService<ApplicationPaths>();
                paths.EnsureRuntimeDirectories();
                LexiconTestDatabases.CreateBoth(paths);
                await host.Services.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(TestContext.Current.CancellationToken);
                return host;
            }
            catch { host.Dispose(); throw; }
        }

        internal async Task ExecuteAsync(string sql, object? parameters = null)
        {
            await using var connection = Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken));
        }

        internal Task SeedLearningAsync(string id, string language = "Japanese", string date = Today) => ExecuteAsync("""
            INSERT OR IGNORE INTO WordGroups VALUES(@id,@language,@id,'Review','2026-10-01T00:00:00Z');
            INSERT INTO LearningGroupProgress VALUES(@id,@language,@date,@date);
            """, new { id, language, date });

        internal Task SeedReviewAsync(string id, string session, string language = "Japanese", string date = Today) => ExecuteAsync("""
            INSERT OR IGNORE INTO WordGroups VALUES(@id,@language,@id,'Review','2026-10-01T00:00:00Z');
            INSERT INTO ReviewSessions(SessionId,Language,Status,PlanJson,CreatedAtUtc,StartedLocalDate)
                VALUES(@session,@language,'Completed','{}','2026-10-01T00:00:00Z','2026-10-01');
            INSERT INTO ReviewSessionGroups VALUES(@session,@id,0,@date);
            """, new { id, session, language, date });

        internal Task SeedKanaAsync(string session, string date = Today) => ExecuteAsync("""
            INSERT INTO KanaSessions VALUES(@session,'2026-10-01','Completed',2,NULL);
            INSERT INTO KanaSessionItems(SessionId,KanaId,Text,Reading,RowHint,Script,Range,QueueOrder,IsCompleted,CompletedLocalDate)
                VALUES(@session,'Hiragana:Clear:あ','あ','あ','あ行','Hiragana','Clear',0,1,@date);
            """, new { session, date });

        public void Dispose() { Services?.Dispose(); directory.Dispose(); }
    }
}
