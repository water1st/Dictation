using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Services.Configuration;
using Dictation.Services.Importing;
using Dictation.Services.Lexicon;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Services
{
    public sealed class ImportInboxServiceTests
    {
        [Fact]
        public async Task StageAsync_RehydratesJapaneseAuthorityAndStableDeduplicatesSelections()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);

            var result = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    ImportSourceKind.Manual,
                    new[]
                    {
                        new ImportSelectionDto("食べます", "jmdict:1:1"),
                        new ImportSelectionDto(" 食べる ", "jmdict:1:1")
                    }),
                TestContext.Current.CancellationToken);

            var item = Assert.Single(result.Items);
            Assert.Equal("食べます", item.SourceText);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(0, result.DuplicateCount);
            var member = Assert.Single(item.WordGroup!.Members);
            Assert.Equal("jmdict:1:1", member.SourceSenseId);
            Assert.Equal("食べる", member.Headword);
            Assert.Equal("动词", member.PartOfSpeechLabel);
            Assert.Contains("二类动词（一段）", member.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("常用汉字", member.AttributeLabel, StringComparison.Ordinal);
            Assert.Equal("吃", member.Meanings[0]);
            Assert.Contains(member.Forms, form =>
                form.Surface == "食べます"
                && form.FormLabel == "礼貌形"
                && form.DetailLabel == "汉字 · 动词");
        }

        [Fact]
        public async Task StageAsync_EnglishUsesDirectUndirectedGroupWithoutRecursiveOrPhraseMembers()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);

            var result = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Mcp,
                    new[]
                    {
                        new ImportSelectionDto("WENT", "go%2:01:00::"),
                        new ImportSelectionDto("converted", "convert%2:30:00::")
                    }),
                TestContext.Current.CancellationToken);

            Assert.Equal(1, result.AddedCount);
            Assert.Equal(1, result.DuplicateCount);
            var addedItem = result.Items[0];
            var memberIds = addedItem.WordGroup!.Members
                .Select(member => member.SourceSenseId)
                .ToArray();
            Assert.Equal(
                new[] { "go%2:01:00::", "convert%2:30:00::" },
                memberIds);
            Assert.DoesNotContain("conversion%1:04:00::", memberIds);
            Assert.DoesNotContain("go%2:02:00::", memberIds);
            Assert.DoesNotContain(addedItem.WordGroup.Members, member => member.IsPhrase);
            Assert.Equal(ImportItemDecision.Pending, addedItem.Decision);
            Assert.Equal(ImportItemDecision.Duplicate, result.Items[1].Decision);
            Assert.Null(result.Items[1].WordGroup);
            Assert.All(addedItem.WordGroup.Members, member =>
            {
                Assert.Equal("动词", member.PartOfSpeechLabel);
                Assert.Contains(member.Forms, form => form.FormLabel == "原形");
                Assert.Contains(member.Forms, form => form.FormLabel == "过去式");
            });
        }

        [Fact]
        public async Task GetPendingBatchesAndDecideAsyncMapDestinationAndCompletion()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);
            var staged = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("went", "go%2:01:00::") }),
                TestContext.Current.CancellationToken);
            var pending = Assert.Single(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));

            await service.DecideAsync(
                new DecideImportBatchRequestDto(
                    pending.BatchId,
                    new[] { Assert.Single(pending.Items).ItemId },
                    ImportDecision.Review),
                TestContext.Current.CancellationToken);

            Assert.Empty(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            Assert.Equal(staged.BatchId, pending.BatchId);
        }

        [Fact]
        public async Task StageAsync_RejectsInvalidRequestAndUnknownSourceWithoutPersisting()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);

            await Assert.ThrowsAsync<ArgumentException>(() => service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    ImportSourceKind.Manual,
                    Array.Empty<ImportSelectionDto>()),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.StageAsync(
                new StageImportBatchRequestDto(
                    (LearningLanguage)99,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    (ImportSourceKind)99,
                    new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }),
                TestContext.Current.CancellationToken));

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("unknown", "missing-sense") }),
                TestContext.Current.CancellationToken));
            Assert.Contains("source sense", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task StageAsync_PreCanceledTokenPropagatesBeforeAuthorityOrPersistence()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("went", "go%2:01:00::") }),
                cancellation.Token));
            Assert.Empty(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task StageAsync_PropagatesUserDatabaseDataAccessError()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            var exception = await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }),
                TestContext.Current.CancellationToken));

            Assert.Contains("导入收件箱", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DecideAsync_RejectsInvalidRequestBeforeDataAccess()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.DecideAsync(
                null!,
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(
                new DecideImportBatchRequestDto(" ", new[] { 1L }, ImportDecision.Learning),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(
                new DecideImportBatchRequestDto("batch", Array.Empty<long>(), ImportDecision.Learning),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(
                new DecideImportBatchRequestDto("batch", new[] { 0L }, ImportDecision.Learning),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(
                new DecideImportBatchRequestDto("batch", new[] { 1L, 1L }, ImportDecision.Learning),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DecideAsync(
                new DecideImportBatchRequestDto("batch", new[] { 1L }, (ImportDecision)99),
                TestContext.Current.CancellationToken));
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
            var factory = new LexiconSqliteConnectionFactory(paths);
            return new ImportInboxService(
                new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(factory),
                    new EnglishLexiconDataAccessor(factory)),
                new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
        }
    }
}
