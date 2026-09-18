using System.Reflection;
using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.DataAccess
{
    /// <summary>
    /// 验证导入及学习链路持久保存稳定词形代码与中文来源元数据。
    /// </summary>
    public sealed class StableImportMetadataTests
    {
        [Fact]
        public async Task MigrateAsync_NewDatabaseAddsStableIdentityColumns()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);

            await using var connection = OpenUserDatabase(paths);
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            Assert.Contains("KanjiUsageCode", await ReadColumnNamesAsync(connection, "WordGroupMembers"));
            var formColumns = await ReadColumnNamesAsync(connection, "WordGroupForms");
            Assert.Contains("FormCode", formColumns);
            Assert.Contains("ScriptCode", formColumns);
            Assert.Contains("RoleCode", formColumns);
            Assert.Equal(6L, await ExecuteScalarAsync<long>(
                connection,
                "SELECT MAX(Version) FROM SchemaMigrations;"));
        }

        [Fact]
        public async Task StageAsync_JapanesePersistsStableCodesFromAuthority()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);

            var result = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("食べます", "jmdict:1:1") }),
                TestContext.Current.CancellationToken);

            var member = Assert.Single(Assert.Single(result.Batch.Items).WordGroup!.Members);
            Assert.Equal("Common", ReadRequiredProperty(member, "KanjiUsageCode"));
            var form = Assert.Single(member.Forms, value => value.Surface == "たべます");
            Assert.Equal("Polite", ReadRequiredProperty(form, "FormCode"));
            Assert.Equal("Kana", ReadRequiredProperty(form, "ScriptCode"));
            Assert.Equal("Verb", ReadRequiredProperty(form, "RoleCode"));

            await using var connection = OpenUserDatabase(paths);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                "Common|Polite|Kana|Verb",
                await ExecuteScalarAsync<string>(
                    connection,
                    """
                    SELECT Members.KanjiUsageCode || '|' || Forms.FormCode || '|' || Forms.ScriptCode || '|' || Forms.RoleCode
                    FROM WordGroupMembers AS Members
                    INNER JOIN WordGroupForms AS Forms ON Forms.MemberId = Members.MemberId
                    WHERE Forms.Surface = 'たべます';
                    """));

            var nounResult = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("食前", "jmdict:2:1") }),
                TestContext.Current.CancellationToken);
            var nounForm = Assert.Single(
                Assert.Single(Assert.Single(nounResult.Batch.Items).WordGroup!.Members).Forms,
                value => value.Surface == "食前");
            Assert.Equal("Dictionary", ReadRequiredProperty(nounForm, "FormCode"));
            Assert.Equal("Kanji", ReadRequiredProperty(nounForm, "ScriptCode"));
            Assert.Equal("Noun", ReadRequiredProperty(nounForm, "RoleCode"));
        }

        [Fact]
        public async Task StageAsync_EnglishPersistsFormCodeAndNullJapaneseMetadata()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);

            var result = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("gone", "go%2:01:00::") }),
                TestContext.Current.CancellationToken);

            var member = Assert.Single(
                Assert.Single(result.Batch.Items).WordGroup!.Members,
                value => value.SourceSenseId == "go%2:01:00::");
            Assert.Null(ReadRequiredProperty(member, "KanjiUsageCode"));
            var form = Assert.Single(member.Forms, value => value.Surface == "gone");
            Assert.Equal("PastParticiple", ReadRequiredProperty(form, "FormCode"));
            Assert.Null(ReadRequiredProperty(form, "ScriptCode"));
            Assert.Null(ReadRequiredProperty(form, "RoleCode"));

            await using var connection = OpenUserDatabase(paths);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Members.KanjiUsageCode, Forms.FormCode, Forms.ScriptCode, Forms.RoleCode
                FROM WordGroupMembers AS Members
                INNER JOIN WordGroupForms AS Forms ON Forms.MemberId = Members.MemberId
                WHERE Members.SourceSenseId = 'go%2:01:00::' AND Forms.Surface = 'gone';
                """;
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.True(reader.IsDBNull(0));
            Assert.Equal("PastParticiple", reader.GetString(1));
            Assert.True(reader.IsDBNull(2));
            Assert.True(reader.IsDBNull(3));
        }

        [Theory]
        [InlineData(typeof(LexiconLookupFormDto))]
        [InlineData(typeof(ImportWordGroupFormDto))]
        [InlineData(typeof(ImportWordGroupFormPo))]
        public void FormContracts_RejectBlankStableFormCode(Type contractType)
        {
            var constructor = Assert.Single(
                contractType.GetConstructors(),
                value => value.GetParameters().Any(parameter => parameter.Name == "formCode"));
            var arguments = constructor.GetParameters()
                .Select(CreateValidArgument)
                .ToArray();
            var formCodeIndex = Array.FindIndex(
                constructor.GetParameters(),
                parameter => parameter.Name == "formCode");
            arguments[formCodeIndex] = " ";

            var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));

            Assert.IsType<ArgumentException>(exception.InnerException);
        }

        [Fact]
        public async Task MigrateAsync_PopulatedV3MapsStableCodesWithoutLosingForms()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateLegacyV3DatabaseAsync(temporaryDirectory, useKnownLabels: true);

            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths)).MigrateAsync(TestContext.Current.CancellationToken);

            await using var connection = OpenUserDatabase(paths);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(6L, await ExecuteScalarAsync<long>(
                connection,
                "SELECT MAX(Version) FROM SchemaMigrations;"));
            Assert.Equal(
                "Common|Polite|Kana|Verb|たべます",
                await ExecuteScalarAsync<string>(
                    connection,
                    """
                    SELECT Members.KanjiUsageCode || '|' || Forms.FormCode || '|' || Forms.ScriptCode || '|' || Forms.RoleCode || '|' || Forms.Surface
                    FROM WordGroupMembers AS Members
                    INNER JOIN WordGroupForms AS Forms ON Forms.MemberId = Members.MemberId
                    WHERE Members.Language = 'Japanese';
                    """));
            Assert.Equal(
                "PastParticiple|gone",
                await ExecuteScalarAsync<string>(
                    connection,
                    """
                    SELECT Forms.FormCode || '|' || Forms.Surface
                    FROM WordGroupMembers AS Members
                    INNER JOIN WordGroupForms AS Forms ON Forms.MemberId = Members.MemberId
                    WHERE Members.Language = 'English';
                    """));
        }

        [Fact]
        public async Task MigrateAsync_UnknownLegacyJapaneseLabelsRollsBackEntireMigration()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateLegacyV3DatabaseAsync(temporaryDirectory, useKnownLabels: false);

            await Assert.ThrowsAsync<SchemaMigrationDataAccessException>(() => new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths)).MigrateAsync(TestContext.Current.CancellationToken));

            await using var connection = OpenUserDatabase(paths);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3L, await ExecuteScalarAsync<long>(
                connection,
                "SELECT MAX(Version) FROM SchemaMigrations;"));
            Assert.DoesNotContain("KanjiUsageCode", await ReadColumnNamesAsync(connection, "WordGroupMembers"));
            Assert.DoesNotContain("FormCode", await ReadColumnNamesAsync(connection, "WordGroupForms"));
            Assert.Equal("未知形态|たべます", await ExecuteScalarAsync<string>(
                connection,
                "SELECT FormLabel || '|' || Surface FROM WordGroupForms WHERE MemberId = 1;"));
        }

        private static object? CreateValidArgument(ParameterInfo parameter)
        {
            if (parameter.ParameterType == typeof(bool))
            {
                return true;
            }

            if (parameter.ParameterType == typeof(string))
            {
                return parameter.Name switch
                {
                    "reading" or "scriptCode" or "roleCode" => null,
                    _ => "有效值"
                };
            }

            throw new InvalidOperationException($"测试没有为参数 {parameter.Name} 定义有效值。");
        }

        private static async Task<ApplicationPaths> CreateLegacyV3DatabaseAsync(
            TemporaryDirectory temporaryDirectory,
            bool useKnownLabels)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            await using var connection = OpenUserDatabase(paths);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE SchemaMigrations (
                    Version INTEGER NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL,
                    AppliedAtUtc TEXT NOT NULL
                );
                INSERT INTO SchemaMigrations VALUES (1, '一', '2026-01-01T00:00:00Z');
                INSERT INTO SchemaMigrations VALUES (2, '二', '2026-01-01T00:00:00Z');
                INSERT INTO SchemaMigrations VALUES (3, '三', '2026-01-01T00:00:00Z');

                CREATE TABLE WordGroups (
                    WordGroupId TEXT NOT NULL PRIMARY KEY,
                    Language TEXT NOT NULL,
                    AnchorSourceSenseId TEXT NOT NULL,
                    Placement TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                CREATE TABLE WordGroupMembers (
                    MemberId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    WordGroupId TEXT NOT NULL,
                    Language TEXT NOT NULL,
                    SourceSenseId TEXT NOT NULL,
                    SourceLexemeId TEXT NOT NULL,
                    Headword TEXT NOT NULL,
                    Reading TEXT NULL,
                    PartOfSpeechLabel TEXT NOT NULL,
                    AttributeLabel TEXT NOT NULL,
                    IsPhrase INTEGER NOT NULL,
                    SortOrder INTEGER NOT NULL,
                    MeaningLanguage TEXT NOT NULL,
                    MeaningSource TEXT NOT NULL,
                    MeaningPrecision TEXT NOT NULL
                );
                CREATE TABLE WordGroupForms (
                    MemberId INTEGER NOT NULL,
                    FormIndex INTEGER NOT NULL,
                    Surface TEXT NOT NULL,
                    Reading TEXT NULL,
                    FormLabel TEXT NOT NULL,
                    DetailLabel TEXT NOT NULL,
                    IsCommon INTEGER NOT NULL,
                    IsPrimary INTEGER NOT NULL,
                    PRIMARY KEY (MemberId, FormIndex)
                );

                INSERT INTO WordGroups VALUES ('ja', 'Japanese', 'ja-sense', 'Inbox', '2026-01-01T00:00:00Z');
                INSERT INTO WordGroups VALUES ('en', 'English', 'en-sense', 'Inbox', '2026-01-01T00:00:00Z');
                INSERT INTO WordGroupMembers VALUES (
                    1, 'ja', 'Japanese', 'ja-sense', 'ja-lexeme', '食べる', 'たべる', '动词', @JapaneseAttribute,
                    0, 0, 'zh-Hans', 'Tomoshi', 'Exact');
                INSERT INTO WordGroupMembers VALUES (
                    2, 'en', 'English', 'en-sense', 'en-lexeme', 'go', NULL, '动词', '动词',
                    0, 0, 'zh-Hans', 'Ecdict', 'Exact');
                INSERT INTO WordGroupForms VALUES (
                    1, 0, 'たべます', 'たべます', @JapaneseForm, @JapaneseDetail, 1, 0);
                INSERT INTO WordGroupForms VALUES (
                    2, 0, 'gone', NULL, '过去分词', '词库来源', 1, 0);
                """;
            command.Parameters.AddWithValue(
                "@JapaneseAttribute",
                useKnownLabels ? "二类动词（一段）；常用汉字" : "无法识别的汉字类型");
            command.Parameters.AddWithValue("@JapaneseForm", useKnownLabels ? "礼貌形" : "未知形态");
            command.Parameters.AddWithValue("@JapaneseDetail", useKnownLabels ? "假名 · 动词" : "未知文字 · 未知角色");
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            return paths;
        }

        private static object? ReadRequiredProperty(object value, string propertyName)
        {
            var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            return property.GetValue(value);
        }

        private static async Task<ApplicationPaths> CreateEnvironmentAsync(TemporaryDirectory temporaryDirectory)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths)).MigrateAsync(TestContext.Current.CancellationToken);
            return paths;
        }

        private static ImportInboxService CreateService(ApplicationPaths paths)
        {
            var lexiconFactory = new LexiconSqliteConnectionFactory(paths);
            return new ImportInboxService(
                new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(lexiconFactory),
                    new EnglishLexiconDataAccessor(lexiconFactory)),
                DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths)));
        }

        private static SqliteConnection OpenUserDatabase(ApplicationPaths paths)
        {
            return new SqliteConnection(
                $"Data Source={paths.UserDataDatabase};Pooling=False;Foreign Keys=True");
        }

        private static async Task<string[]> ReadColumnNamesAsync(
            SqliteConnection connection,
            string tableName)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            var names = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                names.Add(reader.GetString(1));
            }

            return names.ToArray();
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
