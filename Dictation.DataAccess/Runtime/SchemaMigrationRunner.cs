using System.Globalization;
using Dapper;
using Dictation.DataAccess;

namespace Dictation.DataAccess.Runtime
{
    public sealed class SchemaMigrationRunner
    {
        private const string SchemaMigrationsTableName = "SchemaMigrations";
        private readonly ApplicationPaths applicationPaths;
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly IReadOnlyList<SchemaMigration> migrations;

        public SchemaMigrationRunner(
            ApplicationPaths applicationPaths,
            SqliteConnectionFactory connectionFactory)
            : this(applicationPaths, connectionFactory, CreateDefaultMigrations())
        {
        }

        public SchemaMigrationRunner(
            ApplicationPaths applicationPaths,
            SqliteConnectionFactory connectionFactory,
            IEnumerable<SchemaMigration> migrations)
        {
            this.applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            ArgumentNullException.ThrowIfNull(migrations);
            this.migrations = migrations.ToArray();
        }

        public async Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            ValidateMigrations();
            cancellationToken.ThrowIfCancellationRequested();

            var databaseExistedAtStart = File.Exists(applicationPaths.UserDataDatabase);
            var appliedVersions = await ReadAppliedVersionsAsync(cancellationToken);
            var pendingMigrations = GetPendingMigrations(appliedVersions);
            if (pendingMigrations.Count == 0)
            {
                return;
            }

            if (databaseExistedAtStart)
            {
                BackupDatabase(pendingMigrations[^1].Version);
            }

            await ApplyPendingMigrationsAsync(pendingMigrations, cancellationToken);
        }

        private async Task<IReadOnlyList<int>> ReadAppliedVersionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);

                var tableExists = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                    "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @Name LIMIT 1;",
                    new { Name = SchemaMigrationsTableName },
                    cancellationToken: cancellationToken));
                if (tableExists is null)
                {
                    return Array.Empty<int>();
                }

                var versions = await connection.QueryAsync<int>(new CommandDefinition(
                    "SELECT Version FROM SchemaMigrations ORDER BY Version;",
                    cancellationToken: cancellationToken));
                return versions.ToArray();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SchemaMigrationDataAccessException("读取 SQLite 迁移记录失败，已阻止程序启动。", exception);
            }
        }

        private IReadOnlyList<SchemaMigration> GetPendingMigrations(IReadOnlyList<int> appliedVersions)
        {
            var migrationsByVersion = migrations.ToDictionary(migration => migration.Version);
            var distinctAppliedVersions = appliedVersions.Distinct().ToArray();
            if (distinctAppliedVersions.Length != appliedVersions.Count)
            {
                throw CreateInvalidDatabaseException("SchemaMigrations 中存在重复版本记录。");
            }

            foreach (var appliedVersion in appliedVersions)
            {
                if (!migrationsByVersion.ContainsKey(appliedVersion))
                {
                    throw CreateInvalidDatabaseException($"数据库包含当前程序未知的迁移版本 {appliedVersion}。");
                }
            }

            if (appliedVersions.Count > migrations.Count)
            {
                throw CreateInvalidDatabaseException("数据库中的迁移记录多于当前程序支持的定义。");
            }

            for (var index = 0; index < appliedVersions.Count; index++)
            {
                if (appliedVersions[index] != migrations[index].Version)
                {
                    throw CreateInvalidDatabaseException(
                        $"数据库迁移记录缺少版本 {migrations[index].Version}，无法安全继续启动。");
                }
            }

            return migrations.Skip(appliedVersions.Count).ToArray();
        }

        private async Task ApplyPendingMigrationsAsync(
            IReadOnlyList<SchemaMigration> pendingMigrations,
            CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                try
                {
                    foreach (var migration in pendingMigrations)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await ExecuteMigrationSqlAsync(
                            connection,
                            transaction,
                            migration.Sql,
                            cancellationToken);
                        await connection.ExecuteAsync(new CommandDefinition(
                            "INSERT INTO SchemaMigrations (Version, Name, AppliedAtUtc) VALUES (@Version, @Name, @AppliedAtUtc);",
                            new
                            {
                                migration.Version,
                                migration.Name,
                                AppliedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                            },
                            transaction: transaction,
                            cancellationToken: cancellationToken));
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await TryRollbackAsync(transaction);
                    throw;
                }
                catch (Exception exception)
                {
                    await TryRollbackAsync(transaction);
                    throw new SchemaMigrationDataAccessException("应用 SQLite 数据库迁移失败，事务已回滚。", exception);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SchemaMigrationDataAccessException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SchemaMigrationDataAccessException("打开 SQLite 数据库进行迁移失败，已阻止程序启动。", exception);
            }
        }

        private static async Task ExecuteMigrationSqlAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.NextResultAsync(cancellationToken))
            {
            }
        }

        private void BackupDatabase(int targetVersion)
        {
            var backupPath = Path.Combine(
                applicationPaths.Backups,
                $"UserData-v{targetVersion}-{Guid.NewGuid():N}.db.bak");
            try
            {
                File.Copy(applicationPaths.UserDataDatabase, backupPath, overwrite: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new SchemaMigrationDataAccessException("备份 SQLite 数据库失败，未执行任何待处理迁移。", exception);
            }
        }

        private void ValidateMigrations()
        {
            var previousVersion = 0;
            var seenVersions = new HashSet<int>();
            for (var index = 0; index < migrations.Count; index++)
            {
                var migration = migrations[index];
                if (migration is null)
                {
                    throw new ArgumentException($"迁移定义第 {index + 1} 项不能为空。", nameof(migrations));
                }

                if (migration.Version <= 0)
                {
                    throw new ArgumentException("迁移版本必须是正数。", nameof(migrations));
                }

                if (!seenVersions.Add(migration.Version))
                {
                    throw new ArgumentException($"迁移版本 {migration.Version} 重复。", nameof(migrations));
                }

                if (index > 0 && migration.Version <= previousVersion)
                {
                    throw new ArgumentException("迁移版本必须严格递增。", nameof(migrations));
                }

                if (string.IsNullOrWhiteSpace(migration.Name))
                {
                    throw new ArgumentException("迁移名称不能为空。", nameof(migrations));
                }

                if (string.IsNullOrWhiteSpace(migration.Sql))
                {
                    throw new ArgumentException("迁移 SQL 不能为空。", nameof(migrations));
                }

                previousVersion = migration.Version;
            }
        }

        private static async Task TryRollbackAsync(System.Data.Common.DbTransaction transaction)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // 保留原始迁移异常，连接释放时会终止未完成的事务。
            }
        }

        private static SchemaMigrationDataAccessException CreateInvalidDatabaseException(string message)
        {
            return new SchemaMigrationDataAccessException(message, new InvalidOperationException(message));
        }

        private static IReadOnlyList<SchemaMigration> CreateDefaultMigrations()
        {
            return new[]
            {
                new SchemaMigration(
                    1,
                    "创建迁移元数据",
                    "CREATE TABLE IF NOT EXISTS SchemaMigrations (Version INTEGER NOT NULL PRIMARY KEY, Name TEXT NOT NULL, AppliedAtUtc TEXT NOT NULL);"),
                new SchemaMigration(
                    2,
                    "创建导入收件箱和词组快照",
                    """
                    CREATE TABLE ImportBatches (
                        BatchId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        SourceKind TEXT NOT NULL CHECK (SourceKind IN ('Manual', 'Text', 'Mcp')),
                        Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Completed')),
                        CreatedAtUtc TEXT NOT NULL,
                        CompletedAtUtc TEXT NULL
                    );
                    CREATE TABLE WordGroups (
                        WordGroupId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        AnchorSourceSenseId TEXT NOT NULL,
                        Placement TEXT NOT NULL CHECK (Placement IN ('Inbox', 'Learning', 'Review')),
                        CreatedAtUtc TEXT NOT NULL
                    );
                    CREATE TABLE ImportBatchItems (
                        ItemId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        BatchId TEXT NOT NULL,
                        SourceText TEXT NOT NULL,
                        AnchorSourceSenseId TEXT NOT NULL,
                        WordGroupId TEXT NULL,
                        Decision TEXT NOT NULL CHECK (Decision IN ('Pending', 'Duplicate', 'Learning', 'Review', 'Discard')),
                        FOREIGN KEY (BatchId) REFERENCES ImportBatches (BatchId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE SET NULL
                    );
                    CREATE TABLE WordGroupMembers (
                        MemberId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        WordGroupId TEXT NOT NULL,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        SourceSenseId TEXT NOT NULL,
                        SourceLexemeId TEXT NOT NULL,
                        Headword TEXT NOT NULL,
                        Reading TEXT NULL,
                        PartOfSpeechLabel TEXT NOT NULL,
                        AttributeLabel TEXT NOT NULL,
                        IsPhrase INTEGER NOT NULL CHECK (IsPhrase IN (0, 1)),
                        SortOrder INTEGER NOT NULL,
                        UNIQUE (Language, SourceSenseId),
                        UNIQUE (WordGroupId, SortOrder),
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
                    );
                    CREATE TABLE WordGroupMeanings (
                        MemberId INTEGER NOT NULL,
                        MeaningIndex INTEGER NOT NULL,
                        Text TEXT NOT NULL,
                        PRIMARY KEY (MemberId, MeaningIndex),
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
                    );
                    CREATE TABLE WordGroupForms (
                        MemberId INTEGER NOT NULL,
                        FormIndex INTEGER NOT NULL,
                        Surface TEXT NOT NULL,
                        Reading TEXT NULL,
                        FormLabel TEXT NOT NULL,
                        DetailLabel TEXT NOT NULL,
                        IsCommon INTEGER NOT NULL CHECK (IsCommon IN (0, 1)),
                        IsPrimary INTEGER NOT NULL CHECK (IsPrimary IN (0, 1)),
                        PRIMARY KEY (MemberId, FormIndex),
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_ImportBatches_StatusLanguage ON ImportBatches (Status, Language);
                    CREATE INDEX IX_ImportBatchItems_BatchDecision ON ImportBatchItems (BatchId, Decision);
                    CREATE INDEX IX_ImportBatchItems_WordGroupId ON ImportBatchItems (WordGroupId);
                    """),
                new SchemaMigration(
                    3,
                    "标记导入快照的中文释义来源",
                    """
                    ALTER TABLE WordGroupMembers ADD COLUMN MeaningLanguage TEXT NOT NULL DEFAULT 'und';
                    ALTER TABLE WordGroupMembers ADD COLUMN MeaningSource TEXT NOT NULL DEFAULT 'Legacy';
                    ALTER TABLE WordGroupMembers ADD COLUMN MeaningPrecision TEXT NOT NULL DEFAULT 'Missing';
                    """),
                new SchemaMigration(
                    4,
                    "保存稳定词形标识",
                    """
                    ALTER TABLE WordGroupMembers ADD COLUMN KanjiUsageCode TEXT NULL
                        CHECK (
                            (Language = 'English' AND KanjiUsageCode IS NULL)
                            OR (Language = 'Japanese' AND KanjiUsageCode IN ('None', 'Common', 'Rare'))
                        );

                    UPDATE WordGroupMembers
                    SET KanjiUsageCode = CASE
                        WHEN Language = 'English' THEN NULL
                        WHEN AttributeLabel LIKE '%常用汉字%' THEN 'Common'
                        WHEN AttributeLabel LIKE '%罕见汉字%' THEN 'Rare'
                        WHEN AttributeLabel LIKE '%无汉字%' THEN 'None'
                        ELSE 'Unknown'
                    END;

                    CREATE TABLE WordGroupForms_New (
                        MemberId INTEGER NOT NULL,
                        FormIndex INTEGER NOT NULL,
                        Surface TEXT NOT NULL,
                        Reading TEXT NULL,
                        FormCode TEXT NOT NULL CHECK (FormCode IN (
                            'Dictionary', 'Polite', 'Te', 'Ta',
                            'Base', 'PresentFirstPersonSingular', 'PresentNonThirdPerson',
                            'ThirdPersonSingular', 'Past', 'PastParticiple', 'PresentParticiple',
                            'Plural', 'Comparative', 'Superlative')),
                        ScriptCode TEXT NULL CHECK (ScriptCode IS NULL OR ScriptCode IN ('Kanji', 'Kana')),
                        RoleCode TEXT NULL CHECK (RoleCode IS NULL OR RoleCode IN ('Noun', 'Verb', 'Adjective', 'Adverb', 'Other')),
                        FormLabel TEXT NOT NULL,
                        DetailLabel TEXT NOT NULL,
                        IsCommon INTEGER NOT NULL CHECK (IsCommon IN (0, 1)),
                        IsPrimary INTEGER NOT NULL CHECK (IsPrimary IN (0, 1)),
                        PRIMARY KEY (MemberId, FormIndex),
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
                    );

                    INSERT INTO WordGroupForms_New (
                        MemberId, FormIndex, Surface, Reading, FormCode, ScriptCode, RoleCode,
                        FormLabel, DetailLabel, IsCommon, IsPrimary)
                    SELECT
                        Forms.MemberId,
                        Forms.FormIndex,
                        Forms.Surface,
                        Forms.Reading,
                        CASE Forms.FormLabel
                            WHEN '普通形' THEN 'Dictionary'
                            WHEN '礼貌形' THEN 'Polite'
                            WHEN 'て形' THEN 'Te'
                            WHEN 'た形' THEN 'Ta'
                            WHEN '原形' THEN 'Base'
                            WHEN '第一人称单数' THEN 'PresentFirstPersonSingular'
                            WHEN '非第三人称现在式' THEN 'PresentNonThirdPerson'
                            WHEN '第三人称单数' THEN 'ThirdPersonSingular'
                            WHEN '过去式' THEN 'Past'
                            WHEN '过去分词' THEN 'PastParticiple'
                            WHEN '现在分词' THEN 'PresentParticiple'
                            WHEN '复数' THEN 'Plural'
                            WHEN '比较级' THEN 'Comparative'
                            WHEN '最高级' THEN 'Superlative'
                            ELSE 'Unknown'
                        END,
                        CASE
                            WHEN Members.Language = 'English' THEN NULL
                            WHEN Forms.DetailLabel LIKE '汉字 · %' THEN 'Kanji'
                            WHEN Forms.DetailLabel LIKE '假名 · %' THEN 'Kana'
                            ELSE 'Unknown'
                        END,
                        CASE
                            WHEN Members.Language = 'English' THEN NULL
                            WHEN Forms.DetailLabel LIKE '% · 名词' THEN 'Noun'
                            WHEN Forms.DetailLabel LIKE '% · 动词' THEN 'Verb'
                            WHEN Forms.DetailLabel LIKE '% · 形容词' THEN 'Adjective'
                            WHEN Forms.DetailLabel LIKE '% · 副词' THEN 'Adverb'
                            WHEN Forms.DetailLabel LIKE '% · 其他' THEN 'Other'
                            ELSE 'Unknown'
                        END,
                        Forms.FormLabel,
                        Forms.DetailLabel,
                        Forms.IsCommon,
                        Forms.IsPrimary
                    FROM WordGroupForms AS Forms
                    INNER JOIN WordGroupMembers AS Members ON Members.MemberId = Forms.MemberId;

                    DROP TABLE WordGroupForms;
                    ALTER TABLE WordGroupForms_New RENAME TO WordGroupForms;
                    """),
                new SchemaMigration(
                    5,
                    "创建按语言隔离的学习计划",
                    """
                    CREATE TABLE LearningPlans (
                        Language TEXT NOT NULL PRIMARY KEY CHECK (Language IN ('Japanese', 'English')),
                        DailyNewTarget INTEGER NOT NULL CHECK (DailyNewTarget BETWEEN 1 AND 500),
                        SessionWordLimit INTEGER NOT NULL CHECK (SessionWordLimit BETWEEN 1 AND 500),
                        SelectionMode TEXT NOT NULL CHECK (SelectionMode IN ('Random', 'Sequential', 'Manual')),
                        EnglishFormLabelMode TEXT NOT NULL CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')),
                        EnglishExampleSourceSenseId TEXT NULL,
                        UpdatedAtUtc TEXT NOT NULL,
                        CHECK (
                            Language = 'English'
                            OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)
                        )
                    );
                    CREATE TABLE LearningPlanForms (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        FormCode TEXT NOT NULL CHECK (length(trim(FormCode)) > 0),
                        PRIMARY KEY (Language, SortOrder),
                        UNIQUE (Language, FormCode),
                        FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE
                    );
                    CREATE TABLE LearningPlanChannels (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        PRIMARY KEY (Language, SortOrder),
                        UNIQUE (Language, Channel),
                        FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE
                    );
                    CREATE TABLE LearningPlanOutputs (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')),
                        PRIMARY KEY (Language, SortOrder),
                        UNIQUE (Language, OutputMode),
                        FOREIGN KEY (Language) REFERENCES LearningPlans (Language) ON DELETE CASCADE
                    );
                    """)
            };
        }
    }
}
