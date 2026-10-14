using Dapper;
using Dictation.Core.Importing;
using Dictation.DataAccess.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.DataAccess
{
    /// <summary>验证记忆写入复用调用方事务，保留旧状态并拒绝错误所属关系。</summary>
    public sealed class ReviewMemoryTransactionTests
    {
        [Fact]
        public async Task Insert_MissingOnlyPreservesExistingStateAndDeleteCascades()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var member = await environment.MemberAsync(group);
            var seed = Seed(group, member);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var writer = new ReviewMemoryWriter();
                await writer.InsertMissingAsync(connection, transaction, new[] { seed }, TestContext.Current.CancellationToken);
                await writer.InsertMissingAsync(connection, transaction, new[] { seed with { Stability = 80, ReviewCount = 10 } }, TestContext.Current.CancellationToken);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }
            var row = Assert.Single(await environment.MemoriesAsync(group));
            Assert.Equal(seed.Stability, (double)row.Stability);
            Assert.Equal(1L, (long)row.ReviewCount);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId = @Id;", new { Id = group });
            }
            Assert.Empty(await environment.MemoriesAsync(group));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Insert_RejectsForeignMemberOrUnknownForm(bool foreignMember)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            var other = await environment.ImportAsync("食前", "jmdict:2:1");
            var seed = Seed(group, await environment.MemberAsync(foreignMember ? other : group));
            if (!foreignMember) seed = seed with { FormCode = "Unknown" };
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => new ReviewMemoryWriter().InsertMissingAsync(
                connection, transaction, new[] { seed }, TestContext.Current.CancellationToken));
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            Assert.Empty(await environment.MemoriesAsync(group));
        }

        [Fact]
        public async Task Insert_SecondRecordFailureCanRollBackFirstRecordInSameTransaction()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            var seed = Seed(group, await environment.MemberAsync(group));
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync("""
                CREATE TRIGGER RejectTeMemory BEFORE INSERT ON ReviewFormMemories WHEN NEW.FormCode = 'Te'
                BEGIN SELECT RAISE(ABORT, '测试第二条写入失败'); END;
                """);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<SqliteException>(() => new ReviewMemoryWriter().InsertMissingAsync(connection, transaction,
                new[] { seed, seed with { FormCode = "Te" } }, TestContext.Current.CancellationToken));
            Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewFormMemories;", transaction: transaction));
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            Assert.Empty(await environment.MemoriesAsync(group));
        }

        [Fact]
        public async Task Insert_CanceledOperationWritesNothing()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            var seed = Seed(group, await environment.MemberAsync(group));
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ReviewMemoryWriter().InsertMissingAsync(
                connection, transaction, new[] { seed }, cancellation.Token));
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            Assert.Empty(await environment.MemoriesAsync(group));
        }

        private static ReviewFormMemoryPo Seed(string group, long member) => new(group, member, "Dictionary",
            2.3065, 2.118103970459016, MemorySeedTestEnvironment.Now, MemorySeedTestEnvironment.Now.AddDays(2), 1, 0);
    }
}
