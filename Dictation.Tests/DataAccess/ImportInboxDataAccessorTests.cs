using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.DataAccess
{
    /// <summary>
    /// 验证导入暂存、重复识别、中文修改、确认去向及事务回滚。
    /// </summary>
    public sealed class ImportInboxDataAccessorTests
    {
        [Fact]
        public async Task StageAsync_PersistsSnapshotAndListPendingReturnsFullGroup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var group = CreateGroup("Japanese", "jmdict:1:1", "食べる", "to eat");

            var batch = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "Japanese",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("食べます", "jmdict:1:1", group) }),
                TestContext.Current.CancellationToken);

            var item = Assert.Single(batch.Items);
            Assert.Equal("Pending", batch.Status);
            Assert.Equal("Pending", item.Decision);
            Assert.NotNull(item.WordGroupId);
            Assert.Equal("食べる", Assert.Single(item.WordGroup!.Members).Headword);
            Assert.Equal(new[] { "to eat" }, Assert.Single(item.WordGroup.Members).Meanings);

            var pending = await accessor.ListPendingAsync(
                "Japanese",
                TestContext.Current.CancellationToken);
            var listedItem = Assert.Single(Assert.Single(pending).Items);
            Assert.Equal(item.WordGroupId, listedItem.WordGroupId);
            Assert.Equal("to eat", Assert.Single(listedItem.WordGroup!.Members).Meanings[0]);
        }

        [Fact]
        public async Task StageAsync_DuplicateWithinAndAcrossBatchesRecordsWholeItemAsDuplicate()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var group = CreateGroup("English", "go%2:01:00::", "go", "to move");

            var first = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[]
                    {
                        new ImportBatchStageItemPo("went", "go%2:01:00::", group),
                        new ImportBatchStageItemPo("go", "go%2:01:00::", group)
                    }),
                TestContext.Current.CancellationToken);
            var firstItems = first.Items.ToArray();

            Assert.Equal("Pending", firstItems[0].Decision);
            Assert.Equal("Duplicate", firstItems[1].Decision);
            Assert.NotEqual(firstItems[0].WordGroupId, firstItems[1].WordGroupId);

            var second = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("gone", "go%2:01:00::", group) }),
                TestContext.Current.CancellationToken);

            Assert.Equal("Completed", second.Status);
            Assert.Equal("Duplicate", Assert.Single(second.Items).Decision);
            Assert.Null(Assert.Single(second.Items).WordGroupId);
        }

        [Fact]
        public async Task StageAsync_ExistingLearningAndReviewGroupsAreDuplicate()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var learningGroup = CreateGroup("English", "learning", "go", "move");
            var first = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("go", "learning", learningGroup) }),
                TestContext.Current.CancellationToken);

            await accessor.DecideAsync(
                first.BatchId,
                new[] { Assert.Single(first.Items).ItemId },
                "Learning",
                TestContext.Current.CancellationToken);

            var learningDuplicate = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("went", "learning", learningGroup) }),
                TestContext.Current.CancellationToken);
            Assert.Equal("Duplicate", Assert.Single(learningDuplicate.Items).Decision);

            var reviewGroup = CreateGroup("English", "review", "take", "seize");
            var reviewBatch = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("take", "review", reviewGroup) }),
                TestContext.Current.CancellationToken);
            await accessor.DecideAsync(
                reviewBatch.BatchId,
                new[] { Assert.Single(reviewBatch.Items).ItemId },
                "Review",
                TestContext.Current.CancellationToken);

            var reviewDuplicate = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("took", "review", reviewGroup) }),
                TestContext.Current.CancellationToken);
            Assert.Equal("Duplicate", Assert.Single(reviewDuplicate.Items).Decision);
        }

        [Fact]
        public async Task DecideAsync_AllowsPartialDecisionsAndCompletesBatchAfterLastPendingItem()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var firstGroup = CreateGroup("English", "first", "go", "move");
            var secondGroup = CreateGroup("English", "second", "take", "seize");
            var batch = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[]
                    {
                        new ImportBatchStageItemPo("go", "first", firstGroup),
                        new ImportBatchStageItemPo("take", "second", secondGroup)
                    }),
                TestContext.Current.CancellationToken);
            var itemIds = batch.Items.Select(item => item.ItemId).ToArray();

            await accessor.DecideAsync(
                batch.BatchId,
                new[] { itemIds[0] },
                "Learning",
                TestContext.Current.CancellationToken);

            var partial = Assert.Single(await accessor.ListPendingAsync(
                "English",
                TestContext.Current.CancellationToken));
            Assert.Single(partial.Items, item => item.Decision == "Pending");
            Assert.Contains(partial.Items, item => item.Decision == "Learning");
            Assert.Equal("Learning", partial.Items.Single(item => item.ItemId == itemIds[0]).WordGroup!.Placement);

            await accessor.DecideAsync(
                batch.BatchId,
                new[] { itemIds[1] },
                "Review",
                TestContext.Current.CancellationToken);

            Assert.Empty(await accessor.ListPendingAsync(
                "English",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DecideAsync_DiscardDeletesWholeGroupAndRetainsDecisionRecord()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var group = CreateGroup("Japanese", "discard", "食べる", "to eat", includeSecondMember: true);
            var batch = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "Japanese",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("食べる", "discard", group) }),
                TestContext.Current.CancellationToken);
            var item = Assert.Single(batch.Items);

            await accessor.DecideAsync(
                batch.BatchId,
                new[] { item.ItemId },
                "Discard",
                TestContext.Current.CancellationToken);

            Assert.Empty(await accessor.ListPendingAsync(
                "Japanese",
                TestContext.Current.CancellationToken));
            var counts = await ReadCountsAsync(paths);
            Assert.Equal(0, counts.Groups);
            Assert.Equal(0, counts.Members);
            Assert.Equal(0, counts.Meanings);
            Assert.Equal(0, counts.Forms);
            Assert.Equal(1, counts.DiscardedItems);
            Assert.Equal(1, counts.NullWordGroupItems);
        }

        [Fact]
        public async Task DecideAsync_InvalidMixedSelectionRollsBackEveryItem()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var batch = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[]
                    {
                        new ImportBatchStageItemPo("go", "rollback-1", CreateGroup("English", "rollback-1", "go", "move")),
                        new ImportBatchStageItemPo("take", "rollback-2", CreateGroup("English", "rollback-2", "take", "seize"))
                    }),
                TestContext.Current.CancellationToken);
            var itemIds = batch.Items.Select(item => item.ItemId).ToArray();

            var exception = await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => accessor.DecideAsync(
                batch.BatchId,
                new[] { itemIds[0], 999999L },
                "Learning",
                TestContext.Current.CancellationToken));

            Assert.Contains("批次项目", exception.Message, StringComparison.Ordinal);
            var pending = Assert.Single(await accessor.ListPendingAsync(
                "English",
                TestContext.Current.CancellationToken));
            Assert.All(pending.Items, item => Assert.Equal("Pending", item.Decision));
            Assert.Equal("Pending", pending.Status);
        }

        [Fact]
        public async Task DecideAsync_RejectsCrossBatchAndAlreadyDecidedItemsWithoutMutation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var first = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[]
                    {
                        new ImportBatchStageItemPo("go", "cross-first", CreateGroup("English", "cross-first", "go", "move")),
                        new ImportBatchStageItemPo("take", "cross-second", CreateGroup("English", "cross-second", "take", "seize"))
                    }),
                TestContext.Current.CancellationToken);
            var second = await accessor.StageAsync(
                new ImportBatchStagePo(
                    "English",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("run", "cross-other", CreateGroup("English", "cross-other", "run", "move quickly")) }),
                TestContext.Current.CancellationToken);
            var firstIds = first.Items.Select(item => item.ItemId).ToArray();
            var secondId = Assert.Single(second.Items).ItemId;

            await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => accessor.DecideAsync(
                first.BatchId,
                new[] { firstIds[0], secondId },
                "Learning",
                TestContext.Current.CancellationToken));

            var afterCrossBatch = (await accessor.ListPendingAsync(
                "English",
                TestContext.Current.CancellationToken)).ToArray();
            Assert.Equal(2, afterCrossBatch.Length);
            Assert.All(afterCrossBatch.SelectMany(batch => batch.Items), item => Assert.Equal("Pending", item.Decision));

            await accessor.DecideAsync(
                first.BatchId,
                new[] { firstIds[0] },
                "Learning",
                TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => accessor.DecideAsync(
                first.BatchId,
                new[] { firstIds[0] },
                "Review",
                TestContext.Current.CancellationToken));

            var afterAlreadyDecided = (await accessor.ListPendingAsync(
                "English",
                TestContext.Current.CancellationToken)).ToArray();
            var currentFirst = afterAlreadyDecided.Single(batch => batch.BatchId == first.BatchId);
            var decided = currentFirst.Items.Single(item => item.ItemId == firstIds[0]);
            Assert.Equal("Learning", decided.Decision);
            Assert.Equal("Learning", decided.WordGroup!.Placement);
            Assert.Equal("Pending", currentFirst.Items.Single(item => item.ItemId == firstIds[1]).Decision);
        }

        [Fact]
        public async Task StageAsync_PreCanceledTokenPropagatesWithoutCreatingBatch()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => accessor.StageAsync(
                new ImportBatchStagePo(
                    "Japanese",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("食べる", "cancel", CreateGroup("Japanese", "cancel", "食べる", "to eat")) }),
                cancellation.Token));

            Assert.False(File.Exists(paths.UserDataDatabase));
        }

        [Fact]
        public async Task StageAsync_RejectsAnchorNotPresentInGroupWithoutCreatingBatch()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateUserDatabaseAsync(temporaryDirectory);
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));
            var group = new ImportWordGroupSnapshotPo(
                "Japanese",
                "missing-anchor",
                new[] { CreateMember("Japanese", "member-only", "食べる", "to eat") });

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => accessor.StageAsync(
                new ImportBatchStagePo(
                    "Japanese",
                    "Manual",
                    new[] { new ImportBatchStageItemPo("食べる", "missing-anchor", group) }),
                TestContext.Current.CancellationToken));

            Assert.Contains("锚点", exception.Message, StringComparison.Ordinal);
            var counts = await ReadCountsAsync(paths);
            Assert.Equal(0, counts.Groups);
            Assert.Empty(await accessor.ListPendingAsync(
                "Japanese",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ListPendingAsync_MissingDatabaseIsWrappedInChineseException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            var accessor = DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths));

            var exception = await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => accessor.ListPendingAsync(
                "Japanese",
                TestContext.Current.CancellationToken));

            Assert.Contains("导入收件箱", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(exception.InnerException);
        }

        private static async Task<ApplicationPaths> CreateUserDatabaseAsync(TemporaryDirectory temporaryDirectory)
        {
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            await new SchemaMigrationRunner(
                paths,
                new SqliteConnectionFactory(paths)).MigrateAsync(TestContext.Current.CancellationToken);
            return paths;
        }

        private static ImportWordGroupSnapshotPo CreateGroup(
            string language,
            string sourceSenseId,
            string headword,
            string meaning,
            bool includeSecondMember = false)
        {
            var members = new List<ImportWordGroupMemberPo>
            {
                CreateMember(language, sourceSenseId, headword, meaning)
            };
            if (includeSecondMember)
            {
                members.Add(CreateMember(language, sourceSenseId + "-2", "食べます", "to eat politely"));
            }

            return new ImportWordGroupSnapshotPo(language, sourceSenseId, members);
        }

        private static ImportWordGroupMemberPo CreateMember(
            string language,
            string sourceSenseId,
            string headword,
            string meaning)
        {
            return new ImportWordGroupMemberPo(
                language,
                sourceSenseId,
                sourceSenseId + "-lexeme",
                headword,
                language == "Japanese" ? "よみ" : null,
                "动词",
                "属性",
                false,
                "zh-CN",
                "User",
                "UserProvided",
                new[] { meaning },
                new[]
                {
                    new ImportWordGroupFormPo(
                        headword,
                        language == "Japanese" ? "よみ" : null,
                        language == "Japanese" ? "Dictionary" : "Base",
                        language == "Japanese" ? "Kanji" : null,
                        language == "Japanese" ? "Verb" : null,
                        "普通形",
                        "汉字 · 动词",
                        true,
                        true)
                });
        }

        private static async Task<CountResult> ReadCountsAsync(ApplicationPaths paths)
        {
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                $"Data Source={paths.UserDataDatabase};Pooling=False;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM WordGroups),
                    (SELECT COUNT(*) FROM WordGroupMembers),
                    (SELECT COUNT(*) FROM WordGroupMeanings),
                    (SELECT COUNT(*) FROM WordGroupForms),
                    (SELECT COUNT(*) FROM ImportBatchItems WHERE Decision = 'Discard'),
                    (SELECT COUNT(*) FROM ImportBatchItems WHERE WordGroupId IS NULL);
                """;
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            return new CountResult(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5));
        }

        private sealed record CountResult(
            int Groups,
            int Members,
            int Meanings,
            int Forms,
            int DiscardedItems,
            int NullWordGroupItems);
    }
}
