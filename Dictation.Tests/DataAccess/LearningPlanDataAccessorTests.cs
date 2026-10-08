using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
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
    /// 验证计划持久化、学习库分页删除以及共享读取失败的异常包装。
    /// </summary>
    public sealed class LearningPlanDataAccessorTests
    {
        [Fact]
        public async Task SavePlanAsync_RoundTripsEachLanguageIndependently()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));
            var japanese = CreatePlan("Japanese", 20, 15, "Random", new[] { "Dictionary", "Polite" }, new[] { "Kana", "Kanji" });
            var english = CreatePlan("English", 30, 25, "Sequential", new[] { "Base", "Past" }, new[] { "Latin" });

            await accessor.SavePlanAsync(japanese, TestContext.Current.CancellationToken);
            await accessor.SavePlanAsync(english, TestContext.Current.CancellationToken);

            AssertPlanEqual(japanese, await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
            AssertPlanEqual(english, await accessor.GetPlanAsync("English", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task MigrateAsync_AddsLearningPlanSchemaAsVersionFive()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            await using var connection = new SqliteConnectionFactory(paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            Assert.Equal(12L, await connection.ExecuteScalarAsync<long>("SELECT MAX(Version) FROM SchemaMigrations;"));
            var tables = (await connection.QueryAsync<string>(
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'LearningPlan%' ORDER BY name;"))
                .ToArray();
            Assert.Equal(
                new[] { "LearningPlanChannels", "LearningPlanForms", "LearningPlanOutputs", "LearningPlans" },
                tables);
        }

        [Fact]
        public async Task SavePlanAsync_InvalidCountRollsBackWithoutPartialChildren()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));
            var invalid = CreatePlan("Japanese", 0, 20, "Random", new[] { "Dictionary" }, new[] { "Kana" });

            await Assert.ThrowsAsync<LearningPlanDataAccessException>(() => accessor.SavePlanAsync(
                invalid,
                TestContext.Current.CancellationToken));

            Assert.Null(await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetLearningLibraryPageAsync_ReturnsOnlyLanguageLearningGroupsWithStablePagingAndFullSnapshot()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var importService = CreateImportService(paths);
            await StageAndDecideAsync(importService, LearningLanguage.Japanese, "食べる", "jmdict:1:1", ImportDecision.Learning);
            await StageAndDecideAsync(importService, LearningLanguage.Japanese, "食前", "jmdict:2:1", ImportDecision.Learning);
            await StageAndDecideAsync(importService, LearningLanguage.Japanese, "食事", "jmdict:10:1", ImportDecision.Review);
            await StageAndDecideAsync(importService, LearningLanguage.English, "gone", "go%2:01:00::", ImportDecision.Learning);
            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));

            var first = await accessor.GetLearningLibraryPageAsync(
                "Japanese",
                offset: 0,
                limit: 1,
                TestContext.Current.CancellationToken);
            var repeated = await accessor.GetLearningLibraryPageAsync(
                "Japanese",
                offset: 0,
                limit: 1,
                TestContext.Current.CancellationToken);
            var second = await accessor.GetLearningLibraryPageAsync(
                "Japanese",
                offset: 1,
                limit: 1,
                TestContext.Current.CancellationToken);

            Assert.Equal(2, first.TotalCount);
            Assert.Equal(
                first.Groups.Select(group => group.WordGroupId),
                repeated.Groups.Select(group => group.WordGroupId));
            Assert.Single(first.Groups);
            Assert.Single(second.Groups);
            Assert.NotEqual(first.Groups[0].WordGroupId, second.Groups[0].WordGroupId);
            Assert.All(first.Groups.Concat(second.Groups), group =>
            {
                Assert.Equal("Japanese", group.Language);
                Assert.NotEmpty(group.Members);
                Assert.All(group.Members, member =>
                {
                    Assert.NotEmpty(member.Meanings);
                    Assert.NotEmpty(member.Forms);
                    Assert.All(member.Forms, form => Assert.False(string.IsNullOrWhiteSpace(form.FormCode)));
                });
            });
        }

        [Fact]
        public async Task DeleteWordGroupAsync_DeletesOneLearningGroupAndCascadesSnapshot()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var importService = CreateImportService(paths);
            var groupId = await StageAndDecideAsync(
                importService,
                LearningLanguage.Japanese,
                "食べる",
                "jmdict:1:1",
                ImportDecision.Learning);
            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));

            var result = await accessor.DeleteWordGroupAsync(
                "Japanese",
                groupId,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);

            Assert.Equal(DeleteLearningWordGroupResult.Deleted, result);
            Assert.Equal(0L, await CountRowsAsync(paths, "WordGroups", "WordGroupId", groupId));
            Assert.Equal(0L, await CountRowsAsync(paths, "WordGroupMembers", "WordGroupId", groupId));
            Assert.Equal(0L, await CountRowsAsync(paths, "WordGroupMeanings"));
            Assert.Equal(0L, await CountRowsAsync(paths, "WordGroupForms"));
        }

        [Fact]
        public async Task DeleteWordGroupAsync_ReportsMissingWrongLanguageAndWrongPlacementWithoutDeleting()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var importService = CreateImportService(paths);
            var learningGroupId = await StageAndDecideAsync(
                importService,
                LearningLanguage.Japanese,
                "食前",
                "jmdict:2:1",
                ImportDecision.Learning);
            var reviewGroupId = await StageAndDecideAsync(
                importService,
                LearningLanguage.Japanese,
                "食事",
                "jmdict:10:1",
                ImportDecision.Review);
            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));

            Assert.Equal(
                DeleteLearningWordGroupResult.NotFound,
                await accessor.DeleteWordGroupAsync("Japanese", "missing", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
            Assert.Equal(
                DeleteLearningWordGroupResult.WrongLanguage,
                await accessor.DeleteWordGroupAsync("English", learningGroupId, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
            Assert.Equal(
                DeleteLearningWordGroupResult.WrongPlacement,
                await accessor.DeleteWordGroupAsync("Japanese", reviewGroupId, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));
            Assert.Equal(1L, await CountRowsAsync(paths, "WordGroups", "WordGroupId", learningGroupId));
            Assert.Equal(1L, await CountRowsAsync(paths, "WordGroups", "WordGroupId", reviewGroupId));
        }

        [Fact]
        public async Task CountLearningWordGroupsAsync_CountsOnlyDistinctMatchingLearningGroups()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var importService = CreateImportService(paths);
            var learningGroupId = await StageAndDecideAsync(
                importService,
                LearningLanguage.Japanese,
                "食前",
                "jmdict:2:1",
                ImportDecision.Learning);
            var reviewGroupId = await StageAndDecideAsync(
                importService,
                LearningLanguage.Japanese,
                "食事",
                "jmdict:10:1",
                ImportDecision.Review);
            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));

            var count = await accessor.CountLearningWordGroupsAsync(
                "Japanese",
                new[] { learningGroupId, learningGroupId, reviewGroupId, "missing" },
                TestContext.Current.CancellationToken);

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task GetPlanAsync_WhenSchemaIsMissingWrapsSqliteFailure()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            await using (var connection = new SqliteConnectionFactory(paths).CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync("DROP TABLE LearningPlans;");
            }

            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));

            var exception = await Assert.ThrowsAsync<LearningPlanDataAccessException>(() => accessor.GetPlanAsync(
                "Japanese",
                TestContext.Current.CancellationToken));
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public async Task GetLearningLibraryPageAsync_共享快照读取失败_包装中文数据访问异常()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var importService = CreateImportService(paths);
            await StageAndDecideAsync(
                importService,
                LearningLanguage.Japanese,
                "食べる",
                "jmdict:1:1",
                ImportDecision.Learning);
            await using (var connection = new SqliteConnectionFactory(paths).CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(
                    "DROP TABLE WordGroupForms;",
                    cancellationToken: TestContext.Current.CancellationToken));
            }

            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));

            var exception = await Assert.ThrowsAsync<LearningPlanDataAccessException>(() =>
                accessor.GetLearningLibraryPageAsync(
                    "Japanese",
                    offset: 0,
                    limit: 10,
                    TestContext.Current.CancellationToken));
            Assert.Equal("读取学习库失败。", exception.Message);
            Assert.IsType<SqliteException>(exception.InnerException);
        }

        [Fact]
        public async Task SavePlanAsync_PreCanceledTokenDoesNotWrite()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateLearningPlan(new SqliteConnectionFactory(paths));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => accessor.SavePlanAsync(
                CreatePlan("Japanese", 20, 20, "Random", new[] { "Dictionary" }, new[] { "Kana" }),
                cancellation.Token));

            Assert.Null(await accessor.GetPlanAsync("Japanese", TestContext.Current.CancellationToken));
        }

        private static LearningPlanPo CreatePlan(
            string language,
            int dailyTarget,
            int sessionLimit,
            string selectionMode,
            IReadOnlyList<string> forms,
            IReadOnlyList<string> outputs)
        {
            return new LearningPlanPo(
                language,
                dailyTarget,
                sessionLimit,
                selectionMode,
                "GrammarName",
                null,
                forms,
                new[] { "MeaningWriting", "Dictation" },
                outputs,
                new DateTimeOffset(2026, 9, 5, 1, 2, 3, TimeSpan.Zero));
        }

        private static void AssertPlanEqual(LearningPlanPo expected, LearningPlanPo? actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(expected.Language, actual.Language);
            Assert.Equal(expected.DailyNewTarget, actual.DailyNewTarget);
            Assert.Equal(expected.SessionWordLimit, actual.SessionWordLimit);
            Assert.Equal(expected.SelectionMode, actual.SelectionMode);
            Assert.Equal(expected.EnglishFormLabelMode, actual.EnglishFormLabelMode);
            Assert.Equal(expected.EnglishExampleSourceSenseId, actual.EnglishExampleSourceSenseId);
            Assert.Equal(expected.FormCodes, actual.FormCodes);
            Assert.Equal(expected.Channels, actual.Channels);
            Assert.Equal(expected.OutputModes, actual.OutputModes);
            Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        }

        private static async Task<ApplicationPaths> CreateEnvironmentAsync(TemporaryDirectory temporaryDirectory)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)).MigrateAsync(
                TestContext.Current.CancellationToken);
            return paths;
        }

        private static ImportInboxService CreateImportService(ApplicationPaths paths)
        {
            var lexiconFactory = new LexiconSqliteConnectionFactory(paths);
            return new ImportInboxService(
                new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(lexiconFactory),
                    new EnglishLexiconDataAccessor(lexiconFactory)),
                DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths)));
        }

        private static async Task<string> StageAndDecideAsync(
            ImportInboxService service,
            LearningLanguage language,
            string sourceText,
            string sourceSenseId,
            ImportDecision decision)
        {
            var staged = await service.StageAsync(
                new StageImportBatchRequestDto(
                    language,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto(sourceText, sourceSenseId) }),
                TestContext.Current.CancellationToken);
            var item = Assert.Single(staged.Batch.Items);
            await service.DecideAsync(
                new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, decision),
                TestContext.Current.CancellationToken);
            return Assert.IsType<string>(item.WordGroupId);
        }

        private static async Task<long> CountRowsAsync(
            ApplicationPaths paths,
            string table,
            string idColumn,
            string groupId)
        {
            await using var connection = new SqliteConnectionFactory(paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return await connection.ExecuteScalarAsync<long>(
                $"SELECT COUNT(*) FROM {table} WHERE {idColumn} = @GroupId;",
                new { GroupId = groupId });
        }

        private static async Task<long> CountRowsAsync(ApplicationPaths paths, string table)
        {
            await using var connection = new SqliteConnectionFactory(paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table};");
        }
    }
}
