using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Review;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.DataAccess
{
    /// <summary>用真实 SQLite 验证复习范围的原子更新、删除级联与失效选择保护。</summary>
    public sealed class ReviewPlanDataAccessorTests
    {
        [Fact]
        public async Task Save_RoundTripsOrderedOptionsAndDeselectOnlyRemovesMembership()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var first = await environment.ImportAsync(decision: ImportDecision.Review);
            var second = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            var accessor = environment.Provider.GetRequiredService<ReviewPlanDataAccessor>();
            await accessor.SavePlanAsync(Plan(first, second), TestContext.Current.CancellationToken);
            var loaded = Assert.IsType<ReviewPlanPo>(await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
            Assert.Equal(new[] { first, second }, loaded.WordGroupIds);
            Assert.Equal(new[] { "Te", "Dictionary" }, loaded.FormCodes);
            Assert.Equal(new[] { "Dictation", "MeaningWriting" }, loaded.Channels);
            Assert.Equal(MemorySeedTestEnvironment.Now, loaded.UpdatedAtUtc);
            await accessor.SavePlanAsync(Plan(second), TestContext.Current.CancellationToken);
            Assert.Equal(new[] { second }, (await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken))!.WordGroupIds);
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(2L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM WordGroups;"));
        }

        [Theory]
        [InlineData("Deleted")]
        [InlineData("Learning")]
        [InlineData("English")]
        public async Task Save_GroupInvalidatedAfterSelectionKeepsPreviousPlan(string invalidation)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var original = await environment.ImportAsync(decision: ImportDecision.Review);
            var stale = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            var accessor = environment.Provider.GetRequiredService<ReviewPlanDataAccessor>();
            await accessor.SavePlanAsync(Plan(original), TestContext.Current.CancellationToken);
            await using (var connection = environment.Connections.CreateConnection())
            {
                var sql = invalidation switch
                {
                    "Deleted" => "DELETE FROM WordGroups WHERE WordGroupId = @Id;",
                    "Learning" => "UPDATE WordGroups SET Placement = 'Learning' WHERE WordGroupId = @Id;",
                    _ => "UPDATE WordGroups SET Language = 'English' WHERE WordGroupId = @Id;"
                };
                await connection.ExecuteAsync(sql, new { Id = stale });
            }
            await Assert.ThrowsAnyAsync<ArgumentException>(() => accessor.SavePlanAsync(Plan(original, stale) with { DailyReviewTarget = 40 }, TestContext.Current.CancellationToken));
            var loaded = Assert.IsType<ReviewPlanPo>(await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
            Assert.Equal(20, loaded.DailyReviewTarget);
            Assert.Equal(new[] { original }, loaded.WordGroupIds);
        }

        [Fact]
        public async Task Save_ChildWriteFailureRollsBackParentAndAllOptions()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var accessor = environment.Provider.GetRequiredService<ReviewPlanDataAccessor>();
            await accessor.SavePlanAsync(Plan(group), TestContext.Current.CancellationToken);
            await using (var connection = environment.Connections.CreateConnection())
            {
                await connection.ExecuteAsync("""
                    CREATE TRIGGER RejectReviewKana BEFORE INSERT ON ReviewPlanOutputs
                    BEGIN SELECT RAISE(ABORT, '测试保存中途失败'); END;
                    """);
            }
            await Assert.ThrowsAsync<SqliteException>(() => accessor.SavePlanAsync(Plan(group) with
            {
                DailyReviewTarget = 40, FormCodes = new[] { "Polite" }, Channels = new[] { "MeaningWriting" }
            }, TestContext.Current.CancellationToken));
            var loaded = Assert.IsType<ReviewPlanPo>(await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
            Assert.Equal(20, loaded.DailyReviewTarget);
            Assert.Equal(new[] { "Te", "Dictionary" }, loaded.FormCodes);
            Assert.Equal(new[] { "Dictation", "MeaningWriting" }, loaded.Channels);
            Assert.Equal(new[] { group }, loaded.WordGroupIds);
        }

        [Fact]
        public async Task Delete_WholeGroupCascadesMemoryLearningHistoryAndScopeButPreservesOtherGroup()
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync();
            await environment.SavePlanAsync("Dictionary");
            await environment.FinishAsync();
            var other = await environment.ImportAsync("食前", "jmdict:2:1", ImportDecision.Review);
            var accessor = environment.Provider.GetRequiredService<ReviewPlanDataAccessor>();
            await accessor.SavePlanAsync(Plan(group, other), TestContext.Current.CancellationToken);
            Assert.Single(await environment.MemoriesAsync(group));
            await environment.Provider.GetRequiredService<IReviewLibraryManager>().DeleteWordGroupAsync(LearningLanguage.Japanese, group, TestContext.Current.CancellationToken);
            Assert.Empty(await environment.MemoriesAsync(group));
            var loaded = Assert.IsType<ReviewPlanPo>(await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
            Assert.Equal(new[] { other }, loaded.WordGroupIds);
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM WordGroups;"));
            foreach (var table in new[] { "WordGroupMembers", "LearningGroupProgress", "LearningFormStates", "LearningSessionGroups", "LearningQuestions" })
            {
                Assert.Equal(0L, await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE WordGroupId = @Id;", new { Id = group }));
            }
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
        }

        [Theory]
        [InlineData("Missing")]
        [InlineData("Learning")]
        [InlineData("English")]
        public async Task Delete_OnlyCurrentLanguageReviewGroupCanBeDeleted(string scenario)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: scenario == "Learning" ? ImportDecision.Learning : ImportDecision.Review);
            await using var connection = environment.Connections.CreateConnection();
            if (scenario == "English") await connection.ExecuteAsync("UPDATE WordGroups SET Language = 'English' WHERE WordGroupId = @Id;", new { Id = group });
            var accessor = environment.Provider.GetRequiredService<ReviewPlanDataAccessor>();
            if (scenario == "Missing")
            {
                await Assert.ThrowsAsync<KeyNotFoundException>(() => accessor.DeleteWordGroupAsync("Japanese", "missing", MemorySeedTestEnvironment.Now, TestContext.Current.CancellationToken));
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.DeleteWordGroupAsync("Japanese", group, MemorySeedTestEnvironment.Now, TestContext.Current.CancellationToken));
            }
            Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM WordGroups;"));
        }

        [Theory]
        [InlineData("Read")]
        [InlineData("Save")]
        [InlineData("Delete")]
        public async Task Operations_CancellationDoesNotChangePlanOrGroup(string operation)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var accessor = environment.Provider.GetRequiredService<ReviewPlanDataAccessor>();
            await accessor.SavePlanAsync(Plan(group), TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation switch
            {
                "Read" => accessor.GetPlanAsync("Japanese", cancellation.Token),
                "Save" => accessor.SavePlanAsync(Plan(group) with { DailyReviewTarget = 40 }, cancellation.Token),
                _ => accessor.DeleteWordGroupAsync("Japanese", group, MemorySeedTestEnvironment.Now, cancellation.Token)
            });
            Assert.Equal(20, (await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken))!.DailyReviewTarget);
            await using var connection = environment.Connections.CreateConnection();
            Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM WordGroups;"));
        }

        [Theory]
        [InlineData("UnknownRange")]
        [InlineData("UnknownForm")]
        [InlineData("WrongOutput")]
        [InlineData("EmptySelected")]
        [InlineData("DuplicateGroup")]
        public async Task Save_InvalidPersistenceInputCannotCreatePlan(string scenario)
        {
            using var environment = await MemorySeedTestEnvironment.CreateAsync();
            var group = await environment.ImportAsync(decision: ImportDecision.Review);
            var accessor = environment.Provider.GetRequiredService<ReviewPlanDataAccessor>();
            var plan = scenario switch
            {
                "UnknownRange" => Plan(group) with { RangeMode = "Unknown" },
                "UnknownForm" => Plan(group) with { FormCodes = new[] { "Unknown" } },
                "WrongOutput" => Plan(group) with { OutputModes = new[] { "Latin" } },
                "EmptySelected" => Plan(),
                _ => Plan(group, group)
            };
            await Assert.ThrowsAnyAsync<Exception>(() => accessor.SavePlanAsync(plan, TestContext.Current.CancellationToken));
            Assert.Null(await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
        }

        private static ReviewPlanPo Plan(params string[] groups) => new("Japanese", 20, 20, "Selected", groups,
            new[] { "Te", "Dictionary" }, new[] { "Dictation", "MeaningWriting" }, new[] { "Kana" }, "GrammarName", null, MemorySeedTestEnvironment.Now);
    }
}
