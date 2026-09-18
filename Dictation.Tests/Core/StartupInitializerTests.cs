using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Startup;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>
    /// 验证启动目录创建、数据库迁移和失败时停止初始化。
    /// </summary>
    public sealed class StartupInitializerTests
    {
        [Fact]
        public async Task InitializeAsync_CreatesRuntimeDirectoriesAndInitialDatabase()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var initializer = CreateInitializer(paths);

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(paths.Config));
            Assert.True(Directory.Exists(paths.Data));
            Assert.True(Directory.Exists(paths.Logs));
            Assert.True(Directory.Exists(paths.Backups));
            Assert.False(Directory.Exists(paths.Cache));
            Assert.True(File.Exists(paths.UserDataDatabase));
            Assert.Equal(
                new[]
                {
                    "ImportBatchItems",
                    "ImportBatches",
                    "LearningFormStates",
                    "LearningGroupProgress",
                    "LearningPlanChannels",
                    "LearningPlanForms",
                    "LearningPlanOutputs",
                    "LearningPlans",
                    "LearningQuestionAttempts",
                    "LearningQuestions",
                    "LearningSessionGroups",
                    "LearningSessions",
                    "SchemaMigrations",
                    "WordGroupForms",
                    "WordGroupMeanings",
                    "WordGroupMembers",
                    "WordGroups"
                },
                await ReadTableNamesAsync(paths));
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, await ReadAppliedVersionsAsync(paths));
        }

        [Fact]
        public async Task InitializeAsync_WhenCalledTwice_IsIdempotentWithoutBackup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var initializer = CreateInitializer(paths);

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);
            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.Empty(Directory.GetFiles(paths.Backups));
            Assert.Equal(
                new[]
                {
                    "ImportBatchItems",
                    "ImportBatches",
                    "LearningFormStates",
                    "LearningGroupProgress",
                    "LearningPlanChannels",
                    "LearningPlanForms",
                    "LearningPlanOutputs",
                    "LearningPlans",
                    "LearningQuestionAttempts",
                    "LearningQuestions",
                    "LearningSessionGroups",
                    "LearningSessions",
                    "SchemaMigrations",
                    "WordGroupForms",
                    "WordGroupMeanings",
                    "WordGroupMembers",
                    "WordGroups"
                },
                await ReadTableNamesAsync(paths));
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, await ReadAppliedVersionsAsync(paths));
        }

        [Fact]
        public async Task InitializeAsync_WhenResolvedFromProductionDi_UsesDefaultMigration()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var services = new ServiceCollection();
            services
                .AddDictationDataAccess(temporaryDirectory.Path)
                .AddDictationCore();
            using var provider = services.BuildServiceProvider();
            var paths = provider.GetRequiredService<ApplicationPaths>();
            var initializer = provider.GetRequiredService<StartupInitializer>();

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            Assert.Equal(
                new[]
                {
                    "ImportBatchItems",
                    "ImportBatches",
                    "LearningFormStates",
                    "LearningGroupProgress",
                    "LearningPlanChannels",
                    "LearningPlanForms",
                    "LearningPlanOutputs",
                    "LearningPlans",
                    "LearningQuestionAttempts",
                    "LearningQuestions",
                    "LearningSessionGroups",
                    "LearningSessions",
                    "SchemaMigrations",
                    "WordGroupForms",
                    "WordGroupMeanings",
                    "WordGroupMembers",
                    "WordGroups"
                },
                await ReadTableNamesAsync(paths));
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, await ReadAppliedVersionsAsync(paths));
        }

        [Fact]
        public async Task InitializeAsync_WhenDatabaseIsCorrupted_PropagatesMigrationException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            Directory.CreateDirectory(paths.Data);
            File.WriteAllText(paths.UserDataDatabase, "not a sqlite database");
            var initializer = CreateInitializer(paths);

            var exception = await Assert.ThrowsAsync<SchemaMigrationDataAccessException>(() => initializer.InitializeAsync(
                TestContext.Current.CancellationToken));

            Assert.Contains("迁移", exception.Message);
            Assert.NotNull(exception.InnerException);
            Assert.Empty(Directory.GetFiles(paths.Backups));
        }

        [Fact]
        public async Task InitializeAsync_WhenCancellationIsAlreadyRequested_DoesNotCreateRuntimeDirectoriesOrDatabase()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var initializer = CreateInitializer(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => initializer.InitializeAsync(cancellation.Token));

            Assert.False(Directory.Exists(paths.Config));
            Assert.False(Directory.Exists(paths.Data));
            Assert.False(Directory.Exists(paths.Logs));
            Assert.False(Directory.Exists(paths.Backups));
            Assert.False(File.Exists(paths.UserDataDatabase));
        }

        [Fact]
        public void Constructor_RejectsNullDependencies()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var runner = new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths));

            Assert.Throws<ArgumentNullException>(() => new StartupInitializer(null!, runner));
            Assert.Throws<ArgumentNullException>(() => new StartupInitializer(paths, null!));
        }

        private static StartupInitializer CreateInitializer(ApplicationPaths paths)
        {
            return new StartupInitializer(
                paths,
                new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)));
        }

        private static async Task<string[]> ReadTableNamesAsync(ApplicationPaths paths)
        {
            await using var connection = new SqliteConnection($"Data Source={paths.UserDataDatabase};Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            var names = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                names.Add(reader.GetString(0));
            }

            return names.ToArray();
        }

        private static async Task<int[]> ReadAppliedVersionsAsync(ApplicationPaths paths)
        {
            await using var connection = new SqliteConnection($"Data Source={paths.UserDataDatabase};Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Version FROM SchemaMigrations ORDER BY Version;";
            var versions = new List<int>();
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                versions.Add(reader.GetInt32(0));
            }

            return versions.ToArray();
        }
    }
}
