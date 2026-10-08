using System.Globalization;
using Dapper;
using Dictation.DataAccess;

namespace Dictation.DataAccess.Runtime
{
    /// <summary>
    /// 校验迁移连续性，备份既有数据库并在事务内执行待应用迁移。
    /// </summary>
    public sealed class SchemaMigrationRunner
    {
        private const string SchemaMigrationsTableName = "SchemaMigrations";
        private readonly ApplicationPaths applicationPaths;
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly IReadOnlyList<SchemaMigration> migrations;

        /// <summary>
        /// 校验迁移连续性，备份既有数据库并在事务内执行待应用迁移。
        /// </summary>
        /// <param name="applicationPaths">资源、运行数据和备份路径。</param>
        /// <param name="connectionFactory">创建本层所需 SQLite 连接的工厂。</param>
        public SchemaMigrationRunner(
            ApplicationPaths applicationPaths,
            SqliteConnectionFactory connectionFactory)
            : this(applicationPaths, connectionFactory, CreateDefaultMigrations())
        {
        }

        /// <summary>
        /// 校验迁移连续性，备份既有数据库并在事务内执行待应用迁移。
        /// </summary>
        /// <param name="applicationPaths">资源、运行数据和备份路径。</param>
        /// <param name="connectionFactory">创建本层所需 SQLite 连接的工厂。</param>
        /// <param name="migrations">按版本严格递增的迁移定义。</param>
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

        /// <summary>
        /// 检查已应用版本，先备份既有数据库，再事务性执行全部待处理迁移。
        /// </summary>
        /// <param name="cancellationToken">取消数据库操作的令牌；取消异常保持原样传播。</param>
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

        /// <summary>
        /// 要求数据库版本恰为迁移定义的连续前缀，避免跨版本缺口继续启动。
        /// </summary>
        /// <param name="appliedVersions">从数据库按版本排序读取的已应用版本。</param>
        /// <returns>尚未应用的有序迁移；重复、未知或缺口版本会被拒绝。</returns>
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

        /// <summary>
        /// 在一个事务中执行迁移及版本记录；失败时尝试回滚并保留原始原因。
        /// </summary>
        /// <param name="pendingMigrations">按版本排序的待执行迁移。</param>
        /// <param name="cancellationToken">数据库操作取消令牌。</param>
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

        /// <summary>
        /// 消费批量 SQL 的全部结果集，确保后续语句不会因只读首个结果而被遗漏。
        /// </summary>
        /// <param name="connection">当前迁移连接。</param>
        /// <param name="transaction">覆盖整批迁移的事务。</param>
        /// <param name="sql">迁移定义中的完整 SQL。</param>
        /// <param name="cancellationToken">数据库操作取消令牌。</param>
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
                    """),
                new SchemaMigration(
                    6,
                    "创建可恢复学习会话与进度",
                    """
                    CREATE TABLE LearningSessions (
                        SessionId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        Status TEXT NOT NULL CHECK (Status IN ('InProgress', 'Completed')),
                        CurrentOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (CurrentOrdinal >= 0),
                        CreatedAtUtc TEXT NOT NULL,
                        CompletedAtUtc TEXT NULL
                    );
                    CREATE UNIQUE INDEX UX_LearningSessions_ActiveLanguage
                        ON LearningSessions (Language) WHERE Status = 'InProgress';
                    CREATE TABLE LearningSessionGroups (
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        PRIMARY KEY (SessionId, WordGroupId),
                        UNIQUE (SessionId, SortOrder),
                        FOREIGN KEY (SessionId) REFERENCES LearningSessions (SessionId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
                    );
                    CREATE TABLE LearningGroupProgress (
                        WordGroupId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        FirstStartedLocalDate TEXT NOT NULL,
                        CompletedLocalDate TEXT NULL,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
                    );
                    CREATE TABLE LearningFormStates (
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        CorrectStreak INTEGER NOT NULL DEFAULT 0 CHECK (CorrectStreak BETWEEN 0 AND 2),
                        IsMastered INTEGER NOT NULL DEFAULT 0 CHECK (IsMastered IN (0, 1)),
                        NextEligibleOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (NextEligibleOrdinal >= 0),
                        MeaningWritingCount INTEGER NOT NULL DEFAULT 0 CHECK (MeaningWritingCount >= 0),
                        DictationCount INTEGER NOT NULL DEFAULT 0 CHECK (DictationCount >= 0),
                        LatinCount INTEGER NOT NULL DEFAULT 0 CHECK (LatinCount >= 0),
                        KanaCount INTEGER NOT NULL DEFAULT 0 CHECK (KanaCount >= 0),
                        KanjiCount INTEGER NOT NULL DEFAULT 0 CHECK (KanjiCount >= 0),
                        CorrectCount INTEGER NOT NULL DEFAULT 0 CHECK (CorrectCount >= 0),
                        ErrorCount INTEGER NOT NULL DEFAULT 0 CHECK (ErrorCount >= 0),
                        LastAnsweredAtUtc TEXT NULL,
                        PRIMARY KEY (WordGroupId, MemberId, FormCode),
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
                    );
                    CREATE TABLE LearningQuestions (
                        QuestionId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')),
                        QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal >= 0),
                        Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Answered')),
                        CreatedAtUtc TEXT NOT NULL,
                        UNIQUE (SessionId, QuestionOrdinal),
                        FOREIGN KEY (SessionId) REFERENCES LearningSessions (SessionId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX UX_LearningQuestions_PendingSession
                        ON LearningQuestions (SessionId) WHERE Status = 'Pending';
                    CREATE TABLE LearningQuestionAttempts (
                        AttemptId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        QuestionId INTEGER NOT NULL UNIQUE,
                        SubmittedText TEXT NOT NULL,
                        WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0, 1)),
                        AnsweredAtUtc TEXT NOT NULL,
                        FOREIGN KEY (QuestionId) REFERENCES LearningQuestions (QuestionId) ON DELETE CASCADE
                    );
                    """),
                new SchemaMigration(
                    7,
                    "创建成员词形的客观记忆状态",
                    """
                    CREATE TABLE ReviewFormMemories (
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Stability REAL NOT NULL CHECK (Stability > 0),
                        Difficulty REAL NOT NULL CHECK (Difficulty BETWEEN 1 AND 10),
                        LastReviewedAtUtc TEXT NOT NULL,
                        DueAtUtc TEXT NOT NULL CHECK (DueAtUtc > LastReviewedAtUtc),
                        ReviewCount INTEGER NOT NULL CHECK (ReviewCount >= 1),
                        LapseCount INTEGER NOT NULL CHECK (LapseCount >= 0 AND LapseCount <= ReviewCount),
                        PRIMARY KEY (MemberId, FormCode),
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_ReviewFormMemories_WordGroupId ON ReviewFormMemories (WordGroupId);
                    """),
                new SchemaMigration(
                    8,
                    "创建独立复习计划与整组范围",
                    """
                    CREATE TABLE ReviewPlans (
                        Language TEXT NOT NULL PRIMARY KEY CHECK (Language IN ('Japanese', 'English')),
                        DailyReviewTarget INTEGER NOT NULL CHECK (DailyReviewTarget BETWEEN 1 AND 500),
                        SessionWordLimit INTEGER NOT NULL CHECK (SessionWordLimit BETWEEN 1 AND 500),
                        RangeMode TEXT NOT NULL CHECK (RangeMode IN ('All', 'Selected')),
                        EnglishFormLabelMode TEXT NOT NULL CHECK (EnglishFormLabelMode IN ('GrammarName', 'ExampleParadigm')),
                        EnglishExampleSourceSenseId TEXT,
                        UpdatedAtUtc TEXT NOT NULL,
                        CHECK (Language = 'English' OR (EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)),
                        CHECK ((EnglishFormLabelMode = 'GrammarName' AND EnglishExampleSourceSenseId IS NULL)
                            OR (EnglishFormLabelMode = 'ExampleParadigm' AND EnglishExampleSourceSenseId IS NOT NULL AND LENGTH(TRIM(EnglishExampleSourceSenseId)) > 0))
                    );
                    CREATE TABLE ReviewPlanForms (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        FormCode TEXT NOT NULL,
                        PRIMARY KEY (Language, FormCode),
                        UNIQUE (Language, SortOrder),
                        CHECK ((Language = 'Japanese' AND FormCode IN ('Dictionary', 'Polite', 'Te', 'Ta'))
                            OR (Language = 'English' AND FormCode IN ('Base', 'PresentFirstPersonSingular', 'PresentNonThirdPerson',
                                'ThirdPersonSingular', 'Past', 'PastParticiple', 'PresentParticiple', 'Plural', 'Comparative', 'Superlative'))),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE
                    );
                    CREATE TABLE ReviewPlanChannels (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        PRIMARY KEY (Language, Channel),
                        UNIQUE (Language, SortOrder),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE
                    );
                    CREATE TABLE ReviewPlanOutputs (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        OutputMode TEXT NOT NULL,
                        PRIMARY KEY (Language, OutputMode),
                        UNIQUE (Language, SortOrder),
                        CHECK ((Language = 'Japanese' AND OutputMode IN ('Kana', 'Kanji')) OR (Language = 'English' AND OutputMode = 'Latin')),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE
                    );
                    CREATE TABLE ReviewPlanGroups (
                        Language TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        WordGroupId TEXT NOT NULL,
                        PRIMARY KEY (Language, WordGroupId),
                        UNIQUE (Language, SortOrder),
                        FOREIGN KEY (Language) REFERENCES ReviewPlans (Language) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_ReviewPlanGroups_WordGroupId ON ReviewPlanGroups (WordGroupId);
                    """),
                new SchemaMigration(
                    9,
                    "创建独立可恢复复习会话",
                    """
                    CREATE TABLE ReviewSessions (
                        SessionId TEXT NOT NULL PRIMARY KEY,
                        Language TEXT NOT NULL CHECK (Language IN ('Japanese', 'English')),
                        Status TEXT NOT NULL CHECK (Status IN ('InProgress', 'Completed')),
                        PlanJson TEXT NOT NULL CHECK (json_valid(PlanJson)),
                        CreatedAtUtc TEXT NOT NULL,
                        StartedLocalDate TEXT NOT NULL,
                        CompletedAtUtc TEXT NULL
                    );
                    CREATE UNIQUE INDEX UX_ReviewSessions_ActiveLanguage
                        ON ReviewSessions (Language) WHERE Status = 'InProgress';
                    CREATE TABLE ReviewSessionGroups (
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
                        CompletedLocalDate TEXT NULL,
                        PRIMARY KEY (SessionId, WordGroupId),
                        UNIQUE (SessionId, SortOrder),
                        FOREIGN KEY (SessionId) REFERENCES ReviewSessions (SessionId) ON DELETE CASCADE,
                        FOREIGN KEY (WordGroupId) REFERENCES WordGroups (WordGroupId) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_ReviewSessionGroups_WordGroupId ON ReviewSessionGroups (WordGroupId);
                    CREATE TABLE ReviewSessionForms (
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL CHECK (length(trim(FormCode)) > 0),
                        PRIMARY KEY (SessionId, MemberId, FormCode),
                        FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (MemberId) REFERENCES WordGroupMembers (MemberId) ON DELETE CASCADE
                    );
                    """),
                new SchemaMigration(
                    10,
                    "保存独立复习题目与本轮词形成绩",
                    """
                    ALTER TABLE ReviewSessions ADD COLUMN CurrentOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (CurrentOrdinal >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN CorrectStreak INTEGER NOT NULL DEFAULT 0 CHECK (CorrectStreak BETWEEN 0 AND 2);
                    ALTER TABLE ReviewSessionForms ADD COLUMN IsMastered INTEGER NOT NULL DEFAULT 0 CHECK (IsMastered IN (0, 1));
                    ALTER TABLE ReviewSessionForms ADD COLUMN NextEligibleOrdinal INTEGER NOT NULL DEFAULT 0 CHECK (NextEligibleOrdinal >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN MeaningWritingCount INTEGER NOT NULL DEFAULT 0 CHECK (MeaningWritingCount >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN DictationCount INTEGER NOT NULL DEFAULT 0 CHECK (DictationCount >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN LatinCount INTEGER NOT NULL DEFAULT 0 CHECK (LatinCount >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN KanaCount INTEGER NOT NULL DEFAULT 0 CHECK (KanaCount >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN KanjiCount INTEGER NOT NULL DEFAULT 0 CHECK (KanjiCount >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN CorrectCount INTEGER NOT NULL DEFAULT 0 CHECK (CorrectCount >= 0);
                    ALTER TABLE ReviewSessionForms ADD COLUMN ErrorCount INTEGER NOT NULL DEFAULT 0 CHECK (ErrorCount >= 0);
                    CREATE TABLE ReviewQuestions (
                        QuestionId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SessionId TEXT NOT NULL,
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting', 'Dictation')),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin', 'Kana', 'Kanji')),
                        QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal >= 0),
                        Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Answered')),
                        CreatedAtUtc TEXT NOT NULL,
                        SubmittedText TEXT NULL,
                        WasCorrect INTEGER NULL CHECK (WasCorrect IN (0, 1)),
                        AnsweredAtUtc TEXT NULL,
                        UNIQUE (SessionId, QuestionOrdinal),
                        FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX UX_ReviewQuestions_Pending ON ReviewQuestions (SessionId) WHERE Status='Pending';
                    """),
                new SchemaMigration(
                    11,
                    "保存复习成员词形的独立作答历史",
                    """
                    CREATE TABLE ReviewAnswerAttempts (
                        SessionId TEXT NOT NULL,
                        QuestionId INTEGER NOT NULL,
                        QuestionOrdinal INTEGER NOT NULL CHECK (QuestionOrdinal >= 0),
                        WordGroupId TEXT NOT NULL,
                        MemberId INTEGER NOT NULL,
                        FormCode TEXT NOT NULL,
                        Channel TEXT NOT NULL CHECK (Channel IN ('MeaningWriting','Dictation')),
                        OutputMode TEXT NOT NULL CHECK (OutputMode IN ('Latin','Kana','Kanji')),
                        SubmittedText TEXT NOT NULL,
                        WasCorrect INTEGER NOT NULL CHECK (WasCorrect IN (0,1)),
                        AnsweredAtUtc TEXT NOT NULL,
                        PRIMARY KEY (SessionId, QuestionOrdinal, MemberId, FormCode),
                        FOREIGN KEY (SessionId, WordGroupId) REFERENCES ReviewSessionGroups (SessionId, WordGroupId) ON DELETE CASCADE,
                        FOREIGN KEY (SessionId, MemberId, FormCode) REFERENCES ReviewSessionForms (SessionId, MemberId, FormCode) ON DELETE CASCADE
                    );
                    """)
            };
        }
    }
}
