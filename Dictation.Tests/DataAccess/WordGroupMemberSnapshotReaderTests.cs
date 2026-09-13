using System.Data.Common;
using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.DataAccess
{
    public sealed class WordGroupMemberSnapshotReaderTests
    {
        [Fact]
        public async Task ReadAsync_多成员多义项多词形_保留完整字段和持久化顺序()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            await using var connection = new SqliteConnectionFactory(paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await InsertSnapshotAsync(connection, transaction: null);

            var snapshots = await new WordGroupMemberSnapshotReader().ReadAsync(
                connection,
                transaction: null,
                new[] { "group-1" },
                TestContext.Current.CancellationToken);

            var members = Assert.IsAssignableFrom<IReadOnlyList<ImportWordGroupMemberPo>>(snapshots["group-1"]);
            Assert.Equal(new[] { "食べる", "食事" }, members.Select(member => member.Headword));
            var first = members[0];
            Assert.Equal("Japanese", first.Language);
            Assert.Equal("sense-1", first.SourceSenseId);
            Assert.Equal("lexeme-1", first.SourceLexemeId);
            Assert.Equal("たべる", first.Reading);
            Assert.Equal("动词", first.PartOfSpeechLabel);
            Assert.Equal("常用汉字", first.AttributeLabel);
            Assert.True(first.IsPhrase);
            Assert.Equal("zh-CN", first.MeaningLanguage);
            Assert.Equal("JmDict", first.MeaningSource);
            Assert.Equal("Exact", first.MeaningPrecision);
            Assert.Equal("Common", first.KanjiUsageCode);
            Assert.Equal(new[] { "吃", "吃饭" }, first.Meanings);
            Assert.Equal(new[] { "食べる", "たべます" }, first.Forms.Select(form => form.Surface));
            Assert.Equal("たべる", first.Forms[0].Reading);
            Assert.Equal("Dictionary", first.Forms[0].FormCode);
            Assert.Equal("Kanji", first.Forms[0].ScriptCode);
            Assert.Equal("Verb", first.Forms[0].RoleCode);
            Assert.Equal("原形", first.Forms[0].FormLabel);
            Assert.Equal("汉字 · 动词", first.Forms[0].DetailLabel);
            Assert.True(first.Forms[0].IsCommon);
            Assert.True(first.Forms[0].IsPrimary);
            Assert.Equal("Polite", first.Forms[1].FormCode);
            Assert.Equal("Kana", first.Forms[1].ScriptCode);
            Assert.False(first.Forms[1].IsCommon);
            Assert.False(first.Forms[1].IsPrimary);
            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        }

        [Fact]
        public async Task ReadAsync_空词组或无成员_直接返回空映射()
        {
            await using var unopenedConnection = new SqliteConnection();
            var reader = new WordGroupMemberSnapshotReader();

            var emptyInput = await reader.ReadAsync(
                unopenedConnection,
                transaction: null,
                Array.Empty<string>(),
                TestContext.Current.CancellationToken);

            Assert.Empty(emptyInput);

            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            await using var connection = new SqliteConnectionFactory(paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            var noMembers = await reader.ReadAsync(
                connection,
                transaction: null,
                new[] { "missing-group" },
                TestContext.Current.CancellationToken);

            Assert.Empty(noMembers);
        }

        [Fact]
        public async Task ReadAsync_预取消令牌_传播取消且不访问连接()
        {
            await using var unopenedConnection = new SqliteConnection();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                new WordGroupMemberSnapshotReader().ReadAsync(
                    unopenedConnection,
                    transaction: null,
                    new[] { "group-1" },
                    cancellation.Token));
        }

        [Fact]
        public async Task ReadAsync_数据库缺少表_传播原始数据库异常()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<SqliteException>(() =>
                new WordGroupMemberSnapshotReader().ReadAsync(
                    connection,
                    transaction: null,
                    new[] { "group-1" },
                    TestContext.Current.CancellationToken));

            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        }

        [Fact]
        public async Task ReadAsync_调用方事务内读取_不提交且回滚后快照消失()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            await using var connection = new SqliteConnectionFactory(paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await InsertSnapshotAsync(connection, transaction);

            var insideTransaction = await new WordGroupMemberSnapshotReader().ReadAsync(
                connection,
                transaction,
                new[] { "group-1" },
                TestContext.Current.CancellationToken);

            Assert.Equal(2, insideTransaction["group-1"].Count);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            var afterRollback = await new WordGroupMemberSnapshotReader().ReadAsync(
                connection,
                transaction: null,
                new[] { "group-1" },
                TestContext.Current.CancellationToken);
            Assert.Empty(afterRollback);
        }

        private static async Task<ApplicationPaths> CreateEnvironmentAsync(TemporaryDirectory temporaryDirectory)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            await new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)).MigrateAsync(
                TestContext.Current.CancellationToken);
            return paths;
        }

        private static async Task InsertSnapshotAsync(
            SqliteConnection connection,
            DbTransaction? transaction)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO WordGroups (
                    WordGroupId, Language, AnchorSourceSenseId, Placement, CreatedAtUtc)
                VALUES (
                    'group-1', 'Japanese', 'sense-1', 'Learning', '2026-09-13T00:00:00.0000000+00:00');

                INSERT INTO WordGroupMembers (
                    MemberId, WordGroupId, Language, SourceSenseId, SourceLexemeId, Headword, Reading,
                    PartOfSpeechLabel, AttributeLabel, IsPhrase, SortOrder,
                    MeaningLanguage, MeaningSource, MeaningPrecision, KanjiUsageCode)
                VALUES
                    (22, 'group-1', 'Japanese', 'sense-2', 'lexeme-2', '食事', 'しょくじ',
                     '名词', '普通', 0, 1, 'zh-CN', 'JmDict', 'Broad', 'Rare'),
                    (11, 'group-1', 'Japanese', 'sense-1', 'lexeme-1', '食べる', 'たべる',
                     '动词', '常用汉字', 1, 0, 'zh-CN', 'JmDict', 'Exact', 'Common');

                INSERT INTO WordGroupMeanings (MemberId, MeaningIndex, Text)
                VALUES (11, 1, '吃饭'), (22, 0, '饮食'), (11, 0, '吃');

                INSERT INTO WordGroupForms (
                    MemberId, FormIndex, Surface, Reading, FormCode, ScriptCode, RoleCode,
                    FormLabel, DetailLabel, IsCommon, IsPrimary)
                VALUES
                    (11, 1, 'たべます', 'たべます', 'Polite', 'Kana', 'Verb',
                     '礼貌形', '假名 · 动词', 0, 0),
                    (22, 0, '食事', 'しょくじ', 'Dictionary', 'Kanji', 'Noun',
                     '原形', '汉字 · 名词', 1, 1),
                    (11, 0, '食べる', 'たべる', 'Dictionary', 'Kanji', 'Verb',
                     '原形', '汉字 · 动词', 1, 1);
                """,
                transaction: transaction,
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }
}
