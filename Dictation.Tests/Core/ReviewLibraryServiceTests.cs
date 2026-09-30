using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Review;
using Dictation.DataAccess.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>以真实 DI 和 SQLite 验证只读复习词库的词组计数、风险与语言边界。</summary>
    public sealed class ReviewLibraryServiceTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public async Task Load_EmptyLibraryKeepsRequestedLanguageAndSnapshotTime(LearningLanguage language)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var result = await Query(environment).LoadAsync(language, TestContext.Current.CancellationToken);
            Assert.Equal(language, result.Language);
            Assert.Equal(MemorySeedTestEnvironment.Now, result.AsOfUtc);
            Assert.Empty(result.Groups);
            Assert.Equal(0, result.DueGroupCount);
        }

        [Fact]
        public async Task Load_DirectReviewIsPendingWithoutFabricatedStateOrScriptDuplicate()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var id = await environment.ImportAsync(decision: ImportDecision.Review);
            var result = await Query(environment).LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            var group = Assert.Single(result.Groups);
            Assert.Equal(id, group.WordGroup.WordGroupId);
            Assert.Equal(1, result.TotalGroupCount);
            Assert.Equal(1, result.PendingTestGroupCount);
            Assert.True(group.NeedsInitialTest);
            Assert.True(group.IsDue);
            Assert.Null(group.NextReviewAtUtc);
            Assert.Null(group.EstimatedRetention);
            Assert.Equal(4, group.Forms.Count);
            Assert.Equal(4, group.Forms.Select(form => form.FormCode).Distinct().Count());
            Assert.All(group.Forms, form => Assert.False(form.HasMemory));
            Assert.Contains("吃", Assert.Single(group.WordGroup.Members).Meanings);
            Assert.Empty(await environment.MemoriesAsync(id));
        }

        [Fact]
        public async Task Load_EnglishWholeGroupKeepsBothLexemesAndTenFormsAsOneWord()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await ImportEnglishAsync(environment);
            var result = await Query(environment).LoadAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            var group = Assert.Single(result.Groups);
            Assert.Equal(1, result.TotalGroupCount);
            Assert.Equal(2, group.WordGroup.Members.Count);
            Assert.Equal(10, group.Forms.Count);
            Assert.Contains(group.WordGroup.Members, member => member.Headword == "go");
            Assert.Contains(group.WordGroup.Members, member => member.Headword == "convert");
            Assert.All(group.WordGroup.Members, member => Assert.True(member.HasChineseMeaning));
        }

        [Fact]
        public async Task Load_ExcludesLearningAndOtherLanguage()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync();
            var review = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            await ImportEnglishAsync(environment);
            var result = await Query(environment).LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(review, Assert.Single(result.Groups).WordGroup.WordGroupId);
            Assert.All(result.Groups, group => Assert.Equal(LearningLanguage.Japanese, group.WordGroup.Language));
        }

        [Fact]
        public async Task Load_UntestedFirstThenLowestRetentionAndDueMetadata()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var weak = await environment.ImportAsync(decision: ImportDecision.Review);
            var strong = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            var pending = await environment.ImportAsync("食事", "jmdict:10:1", ImportDecision.Review);
            await SeedAsync(environment, weak, 0.212, 5, -4);
            await SeedAsync(environment, strong, 100, 5, 95);
            var result = await Query(environment).LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(new[] { pending, weak, strong }, result.Groups.Select(group => group.WordGroup.WordGroupId));
            Assert.Equal(2, result.DueGroupCount);
            Assert.Equal(1, result.PendingTestGroupCount);
            Assert.True(result.Groups[1].IsDue);
            Assert.False(result.Groups[1].NeedsInitialTest);
            Assert.False(result.Groups[2].IsDue);
            Assert.True(result.Groups[1].EstimatedRetention < result.Groups[2].EstimatedRetention);
            Assert.Equal(MemorySeedTestEnvironment.Now.AddDays(-4), result.Groups[1].NextReviewAtUtc);
            Assert.Equal(MemorySeedTestEnvironment.Now.AddDays(95), result.Groups[2].NextReviewAtUtc);
        }

        [Fact]
        public async Task Load_LearnedFormKeepsNextDateWithoutTreatingDisabledFormsAsDue()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            var group = Assert.Single((await Query(environment).LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken)).Groups);
            Assert.False(group.NeedsInitialTest);
            Assert.False(group.IsDue);
            Assert.Equal(MemorySeedTestEnvironment.Now.AddDays(2), group.NextReviewAtUtc);
            var dictionary = Assert.Single(group.Forms, form => form.FormCode == "Dictionary");
            Assert.True(dictionary.HasMemory);
            Assert.Equal(1, dictionary.ReviewCount);
            Assert.Equal(1, dictionary.EstimatedRetention);
            Assert.Equal(3, group.Forms.Count(form => !form.HasMemory));
        }

        [Fact]
        public async Task Load_DoesNotChangeMemoryOrCreateAnswerHistory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var id = await environment.ImportAsync(decision: ImportDecision.Review);
            await SeedAsync(environment, id, 100, 5, 95);
            var query = Query(environment);
            Assert.Single((await query.LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken)).Groups);
            Assert.Single((await query.LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken)).Groups);
            var memory = Assert.Single(await environment.MemoriesAsync(id));
            Assert.Equal(100, (double)memory.Stability);
            Assert.Equal(1L, (long)memory.ReviewCount);
            Assert.Equal(MemorySeedTestEnvironment.Now.AddDays(95).ToString("O"), (string)memory.DueAtUtc);
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
        }

        [Fact]
        public async Task Load_CanceledQueryPropagatesWithoutChangingData()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var id = await environment.ImportAsync(decision: ImportDecision.Review);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Query(environment).LoadAsync(LearningLanguage.Japanese, cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => environment.Provider.GetRequiredService<ReviewLibraryDataAccessor>().LoadAsync("Japanese", cancellation.Token));
            Assert.Empty(await environment.MemoriesAsync(id));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(999)]
        public async Task Load_RejectsUnsupportedLanguageAtBothBoundaries(int value)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Query(environment).LoadAsync((LearningLanguage)value, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => environment.Provider.GetRequiredService<ReviewLibraryDataAccessor>().LoadAsync(value.ToString(), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Load_GroupDeletionRemovesQueueAndCascadedMemory()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var id = await environment.ImportAsync(decision: ImportDecision.Review);
            await SeedAsync(environment, id, 100, 5, 95);
            Assert.Single((await Query(environment).LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken)).Groups);
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync("DELETE FROM WordGroups WHERE WordGroupId = @Id;", new { Id = id });
            Assert.Empty((await Query(environment).LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken)).Groups);
            Assert.Empty(await environment.MemoriesAsync(id));
        }

        private static IReviewLibraryQuery Query(MemorySeedTestEnvironment environment) => environment.Provider.GetRequiredService<IReviewLibraryQuery>();

        private static async Task ImportEnglishAsync(MemorySeedTestEnvironment environment)
        {
            var staged = await environment.Provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                LearningLanguage.English, ImportSourceKind.Manual, new[] { new ImportSelectionDto("went", "go%2:01:00::") }), TestContext.Current.CancellationToken);
            await environment.Provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                staged.Batch.BatchId, new[] { Assert.Single(staged.Batch.Items).ItemId }, ImportDecision.Review), TestContext.Current.CancellationToken);
        }

        private static async Task SeedAsync(MemorySeedTestEnvironment environment, string id, double stability, int elapsedDays, int dueDays)
        {
            await using var connection = environment.Connections.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await new ReviewMemoryWriter().InsertMissingAsync(connection, transaction, new[] { new ReviewFormMemoryPo(
                id, await environment.MemberAsync(id), "Dictionary", stability, 5,
                MemorySeedTestEnvironment.Now.AddDays(-elapsedDays), MemorySeedTestEnvironment.Now.AddDays(dueDays), 1, 0) }, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
    }
}
