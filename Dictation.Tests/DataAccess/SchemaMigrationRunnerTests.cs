using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.DataAccess
{
    public sealed class SchemaMigrationRunnerTests
    {
        [Fact]
        public void SqliteConnectionFactory_ReturnsNewConnectionsForEachCall()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var factory = new SqliteConnectionFactory(paths);

            using var first = factory.CreateConnection();
            using var second = factory.CreateConnection();

            Assert.NotSame(first, second);
            Assert.Equal(paths.UserDataDatabase, first.DataSource);
            Assert.Equal(paths.UserDataDatabase, second.DataSource);
        }

        [Fact]
        public async Task SqliteConnectionFactory_HandlesSemicolonInRootPath()
        {
            var rootDirectory = Path.Combine(
                Path.GetTempPath(),
                $"Dictation.Tests;{Guid.NewGuid():N}");
            Directory.CreateDirectory(rootDirectory);

            try
            {
                var paths = new ApplicationPaths(rootDirectory);
                paths.EnsureRuntimeDirectories();
                var factory = new SqliteConnectionFactory(paths);

                await using var connection = factory.CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);

                Assert.Equal(paths.UserDataDatabase, connection.DataSource);
                Assert.True(File.Exists(paths.UserDataDatabase));
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public async Task MigrateAsync_NewDatabase_CreatesMetadataAndImportTablesWithoutBackup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var runner = CreateRunner(paths);

            await runner.MigrateAsync(TestContext.Current.CancellationToken);

            Assert.True(File.Exists(paths.UserDataDatabase));
            Assert.Equal(
                new[]
                {
                    "ImportBatchItems",
                    "ImportBatches",
                    "LearningPlanChannels",
                    "LearningPlanForms",
                    "LearningPlanOutputs",
                    "LearningPlans",
                    "SchemaMigrations",
                    "WordGroupForms",
                    "WordGroupMeanings",
                    "WordGroupMembers",
                    "WordGroups"
                },
                await ReadTableNamesAsync(paths));
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, await ReadAppliedVersionsAsync(paths));
            Assert.Empty(Directory.GetFiles(paths.Backups));
        }

        [Fact]
        public async Task MigrateAsync_UpgradesV1Database_AndLatestConstraintsAreActive()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { CreateInitialMigration() }).MigrateAsync(TestContext.Current.CancellationToken);

            await CreateRunner(paths).MigrateAsync(TestContext.Current.CancellationToken);

            await using var connection = new SqliteConnection($"Data Source={paths.UserDataDatabase};Pooling=False;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "PRAGMA foreign_keys;"));
            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
                connection,
                "INSERT INTO ImportBatches (BatchId, Language, SourceKind, Status, CreatedAtUtc) VALUES ('bad', 'Japanese', 'Manual', 'Unknown', 'now');"));
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, await ReadAppliedVersionsAsync(paths));

            await ExecuteAsync(connection, "INSERT INTO WordGroups (WordGroupId, Language, AnchorSourceSenseId, Placement, CreatedAtUtc) VALUES ('legacy-group', 'Japanese', 'legacy', 'Inbox', '2026-01-01T00:00:00Z');");
            await ExecuteAsync(connection, "INSERT INTO WordGroupMembers (WordGroupId, Language, SourceSenseId, SourceLexemeId, Headword, PartOfSpeechLabel, AttributeLabel, IsPhrase, SortOrder) VALUES ('legacy-group', 'Japanese', 'legacy', 'legacy', '旧词', '其他', '其他', 0, 0);");
            Assert.Equal(
                "und|Legacy|Missing",
                await ExecuteScalarAsync<string>(connection, "SELECT MeaningLanguage || '|' || MeaningSource || '|' || MeaningPrecision FROM WordGroupMembers WHERE SourceSenseId = 'legacy';"));
        }

        [Fact]
        public async Task MigrateAsync_ExistingDatabaseWithPendingVersion_BackupsBeforeApplyingPending()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var v1 = CreateInitialMigration();
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1 }).MigrateAsync(TestContext.Current.CancellationToken);
            var v2 = new SchemaMigration(2, "添加第二张表", "CREATE TABLE AddedV2 (Id INTEGER NOT NULL);");
            var runner = new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1, v2 });

            await runner.MigrateAsync(TestContext.Current.CancellationToken);

            var backups = Directory.GetFiles(paths.Backups);
            Assert.Single(backups);
            Assert.Equal(new[] { "SchemaMigrations" }, await ReadTableNamesAsync(backups[0]));
            Assert.Equal(new[] { "AddedV2", "SchemaMigrations" }, await ReadTableNamesAsync(paths));
            Assert.Equal(new[] { 1, 2 }, await ReadAppliedVersionsAsync(paths));
        }

        [Fact]
        public async Task MigrateAsync_WhenNoMigrationIsPending_DoesNotCreateAnotherBackup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var migrations = new[] { CreateInitialMigration() };
            var factory = new SqliteConnectionFactory(paths);
            var runner = new SchemaMigrationRunner(paths, factory, migrations);

            await runner.MigrateAsync(TestContext.Current.CancellationToken);
            var backupCount = Directory.GetFiles(paths.Backups).Length;
            await runner.MigrateAsync(TestContext.Current.CancellationToken);

            Assert.Equal(backupCount, Directory.GetFiles(paths.Backups).Length);
        }

        [Fact]
        public async Task MigrateAsync_WhenLaterMigrationFails_RollsBackTheWholePendingBatch()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var v1 = CreateInitialMigration();
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1 }).MigrateAsync(TestContext.Current.CancellationToken);
            var v2 = new SchemaMigration(2, "添加第二张表", "CREATE TABLE AddedV2 (Id INTEGER NOT NULL);");
            var v3 = new SchemaMigration(3, "故意失败", "CREATE TABLE AddedV3 (Id INTEGER NOT NULL); INSERT INTO MissingTable VALUES (1);");
            var runner = new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1, v2, v3 });

            await Assert.ThrowsAsync<SchemaMigrationDataAccessException>(() => runner.MigrateAsync(
                TestContext.Current.CancellationToken));

            Assert.Equal(new[] { "SchemaMigrations" }, await ReadTableNamesAsync(paths));
            Assert.Equal(new[] { 1 }, await ReadAppliedVersionsAsync(paths));
        }

        [Fact]
        public async Task MigrateAsync_WhenBackupPathIsOccupied_LeavesLiveDatabaseUnchanged()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var v1 = CreateInitialMigration();
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1 }).MigrateAsync(TestContext.Current.CancellationToken);
            Directory.Delete(paths.Backups);
            File.WriteAllText(paths.Backups, "occupied");
            var v2 = new SchemaMigration(2, "添加第二张表", "CREATE TABLE AddedV2 (Id INTEGER NOT NULL);");
            var runner = new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1, v2 });

            var exception = await Assert.ThrowsAsync<SchemaMigrationDataAccessException>(() => runner.MigrateAsync(
                TestContext.Current.CancellationToken));

            Assert.Contains("备份", exception.Message);
            Assert.Equal(new[] { "SchemaMigrations" }, await ReadTableNamesAsync(paths));
            Assert.Equal(new[] { 1 }, await ReadAppliedVersionsAsync(paths));
        }

        [Fact]
        public async Task MigrateAsync_WhenDatabaseIsCorrupted_ThrowsReadableDataAccessException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            File.WriteAllText(paths.UserDataDatabase, "not a sqlite database");
            var runner = CreateRunner(paths);

            var exception = await Assert.ThrowsAsync<SchemaMigrationDataAccessException>(() => runner.MigrateAsync(
                TestContext.Current.CancellationToken));

            Assert.Contains("迁移", exception.Message);
            Assert.NotNull(exception.InnerException);
            Assert.Empty(Directory.GetFiles(paths.Backups));
        }

        [Fact]
        public async Task MigrateAsync_RejectsInvalidMigrationDefinitionsBeforeCreatingDatabase()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var invalidDefinitions = new[]
            {
                new[] { new SchemaMigration(0, "零版本", "SELECT 1;") },
                new[] { new SchemaMigration(1, "", "SELECT 1;") },
                new[] { new SchemaMigration(1, "空 SQL", "") },
                new[]
                {
                    new SchemaMigration(1, "重复一", "SELECT 1;"),
                    new SchemaMigration(1, "重复二", "SELECT 2;")
                },
                new[]
                {
                    new SchemaMigration(2, "二", "SELECT 2;"),
                    new SchemaMigration(1, "一", "SELECT 1;")
                }
            };

            foreach (var migrations in invalidDefinitions)
            {
                var runner = new SchemaMigrationRunner(
                    paths,
                    new SqliteConnectionFactory(paths),
                    migrations);

                await Assert.ThrowsAsync<ArgumentException>(() => runner.MigrateAsync(
                    TestContext.Current.CancellationToken));
                Assert.False(File.Exists(paths.UserDataDatabase));
            }
        }

        [Fact]
        public async Task MigrateAsync_RejectsUnknownAppliedVersionBeforeApplyingPendingMigrations()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var v1 = CreateInitialMigration();
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1 }).MigrateAsync(TestContext.Current.CancellationToken);
            await ExecuteAsync(paths, "INSERT INTO SchemaMigrations (Version, Name, AppliedAtUtc) VALUES (99, '未来', '2026-01-01T00:00:00Z');");
            var v2 = new SchemaMigration(2, "添加第二张表", "CREATE TABLE AddedV2 (Id INTEGER NOT NULL);");
            var runner = new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1, v2 });

            await Assert.ThrowsAsync<SchemaMigrationDataAccessException>(() => runner.MigrateAsync(
                TestContext.Current.CancellationToken));

            Assert.Equal(new[] { 1, 99 }, await ReadAppliedVersionsAsync(paths));
            Assert.Equal(new[] { "SchemaMigrations" }, await ReadTableNamesAsync(paths));
        }

        [Fact]
        public async Task MigrateAsync_RejectsMissingEarlierAppliedVersion()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var v1 = CreateInitialMigration();
            var v2 = new SchemaMigration(2, "二", "CREATE TABLE AddedV2 (Id INTEGER NOT NULL);");
            var v3 = new SchemaMigration(3, "三", "CREATE TABLE AddedV3 (Id INTEGER NOT NULL);");
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1, v2, v3 }).MigrateAsync(TestContext.Current.CancellationToken);
            await ExecuteAsync(paths, "DELETE FROM SchemaMigrations WHERE Version = 2;");
            var runner = new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths),
                new[] { v1, v2, v3 });

            await Assert.ThrowsAsync<SchemaMigrationDataAccessException>(() => runner.MigrateAsync(
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task MigrateAsync_WhenCancellationIsAlreadyRequested_DoesNotCreateDatabase()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = CreateRuntimePaths(temporaryDirectory);
            var runner = CreateRunner(paths);

            await Assert.ThrowsAsync<OperationCanceledException>(() => runner.MigrateAsync(
                new CancellationToken(canceled: true)));

            Assert.False(File.Exists(paths.UserDataDatabase));
        }

        private static ApplicationPaths CreateRuntimePaths(TemporaryDirectory temporaryDirectory)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            return paths;
        }

        private static SchemaMigrationRunner CreateRunner(ApplicationPaths paths)
        {
            return new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths));
        }

        private static SchemaMigration CreateInitialMigration()
        {
            return new SchemaMigration(
                1,
                "创建迁移元数据",
                "CREATE TABLE IF NOT EXISTS SchemaMigrations (Version INTEGER NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAtUtc TEXT NOT NULL);");
        }

        private static async Task<string[]> ReadTableNamesAsync(ApplicationPaths paths)
        {
            return await ReadTableNamesAsync(paths.UserDataDatabase);
        }

        private static async Task<string[]> ReadTableNamesAsync(string databasePath)
        {
            await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
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

        private static async Task ExecuteAsync(ApplicationPaths paths, string sql)
        {
            await using var connection = new SqliteConnection($"Data Source={paths.UserDataDatabase};Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        private static async Task ExecuteAsync(SqliteConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("SQL 未返回值。"));
        }
    }
}
