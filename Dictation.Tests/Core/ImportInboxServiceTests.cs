using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;
using System.Security.Cryptography;

namespace Dictation.Tests.Core
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

            var item = Assert.Single(result.Batch.Items);
            Assert.False(string.IsNullOrWhiteSpace(result.Batch.BatchId));
            Assert.Equal(LearningLanguage.Japanese, result.Batch.Language);
            Assert.Equal(ImportSourceKind.Manual, result.Batch.SourceKind);
            Assert.Equal(ImportBatchStatus.Pending, result.Batch.Status);
            Assert.Null(result.Batch.CompletedAtUtc);
            Assert.Equal("食べます", item.SourceText);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(0, result.DuplicateCount);
            var member = Assert.Single(item.WordGroup!.Members);
            Assert.Equal("jmdict:1:1", member.SourceSenseId);
            Assert.Equal("jmdict:1:1", member.SourceLexemeId);
            Assert.Equal("Japanese", member.Language);
            Assert.Equal("食べる", member.Headword);
            Assert.Equal("たべる", member.Reading);
            Assert.Equal("动词", member.PartOfSpeechLabel);
            Assert.Contains("二类动词（一段）", member.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("常用汉字", member.AttributeLabel, StringComparison.Ordinal);
            Assert.False(member.IsPhrase);
            Assert.Equal("Common", member.KanjiUsageCode);
            Assert.Equal(new[] { "吃", "吃饭" }, member.Meanings);
            Assert.Equal(LexiconMeaningSource.Tomoshi, member.MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.Exact, member.MeaningPrecision);
            var politeForm = Assert.Single(member.Forms, form => form.Surface == "食べます");
            Assert.Equal("たべます", politeForm.Reading);
            Assert.Equal("Polite", politeForm.FormCode);
            Assert.Equal("Kanji", politeForm.ScriptCode);
            Assert.Equal("Verb", politeForm.RoleCode);
            Assert.Equal("礼貌形", politeForm.FormLabel);
            Assert.Equal("汉字 · 动词", politeForm.DetailLabel);
            Assert.True(politeForm.IsCommon);
            Assert.True(politeForm.IsPrimary);
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
            var addedItem = result.Batch.Items[0];
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
            Assert.Equal(ImportItemDecision.Duplicate, result.Batch.Items[1].Decision);
            Assert.Null(result.Batch.Items[1].WordGroup);
            Assert.All(addedItem.WordGroup.Members, member =>
            {
                Assert.Equal("动词", member.PartOfSpeechLabel);
                Assert.Contains(member.Forms, form => form.FormLabel == "原形");
                Assert.Contains(member.Forms, form => form.FormLabel == "过去式");
            });
        }

        [Fact]
        public async Task StageAsync_UserOverrideNormalizesOnlySelectedAnchor()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);

            var result = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("went", "go%2:01:00::", new[] { " カ\u3099 ", "ガ" }) }),
                TestContext.Current.CancellationToken);

            var members = Assert.Single(result.Batch.Items).WordGroup!.Members;
            var anchor = Assert.Single(members, member => member.SourceSenseId == "go%2:01:00::");
            Assert.Equal(new[] { "ガ" }, anchor.Meanings);
            Assert.Equal(LexiconMeaningSource.User, anchor.MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.UserProvided, anchor.MeaningPrecision);
            var related = Assert.Single(members, member => member.SourceSenseId == "convert%2:30:00::");
            Assert.Equal(new[] { "转换" }, related.Meanings);
            Assert.Equal(LexiconMeaningSource.ChineseOpenWordNet, related.MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.Exact, related.MeaningPrecision);
        }

        [Theory]
        [InlineData(ImportDecision.Learning)]
        [InlineData(ImportDecision.Review)]
        public async Task MissingMeaning_StaysPendingUntilEditedThenAllowsDestinationWithoutChangingLexicons(
            ImportDecision decision)
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);
            var japaneseHash = HashFile(paths.JapaneseLexiconDatabase);
            var englishHash = HashFile(paths.EnglishLexiconDatabase);
            var staged = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("%", "missing:symbols:v") }),
                TestContext.Current.CancellationToken);
            var item = Assert.Single(staged.Batch.Items);
            var member = Assert.Single(item.WordGroup!.Members);

            Assert.False(member.HasChineseMeaning);
            Assert.Equal(LexiconMeaningSource.Missing, member.MeaningSource);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DecideAsync(
                new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, ImportDecision.Learning),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateMeaningsAsync(
                new UpdateImportMeaningsRequestDto(item.WordGroup.WordGroupId, member.SourceSenseId, new[] { " " }),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => service.UpdateMeaningsAsync(
                new UpdateImportMeaningsRequestDto(item.WordGroup.WordGroupId, "wrong-member", new[] { "释义" }),
                TestContext.Current.CancellationToken));

            await service.UpdateMeaningsAsync(
                new UpdateImportMeaningsRequestDto(item.WordGroup.WordGroupId, member.SourceSenseId, new[] { " 符号 ", "符号" }),
                TestContext.Current.CancellationToken);
            var updated = Assert.Single(Assert.Single(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken)).Items).WordGroup!;
            var updatedMember = Assert.Single(updated.Members);
            Assert.Equal(new[] { "符号" }, updatedMember.Meanings);
            Assert.Equal(LexiconMeaningSource.User, updatedMember.MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.UserProvided, updatedMember.MeaningPrecision);

            await service.DecideAsync(
                new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, decision),
                TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => service.UpdateMeaningsAsync(
                new UpdateImportMeaningsRequestDto(item.WordGroup.WordGroupId, member.SourceSenseId, new[] { "新释义" }),
                TestContext.Current.CancellationToken));
            Assert.Equal(japaneseHash, HashFile(paths.JapaneseLexiconDatabase));
            Assert.Equal(englishHash, HashFile(paths.EnglishLexiconDatabase));
        }

        [Fact]
        public async Task MissingMeaning_DiscardRemainsAllowed()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = await CreateEnvironmentAsync(temporaryDirectory);
            var service = CreateService(paths);
            var staged = await service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("%", "missing:symbols:v") }),
                TestContext.Current.CancellationToken);

            await service.DecideAsync(
                new DecideImportBatchRequestDto(
                    staged.Batch.BatchId,
                    new[] { Assert.Single(staged.Batch.Items).ItemId },
                    ImportDecision.Discard),
                TestContext.Current.CancellationToken);

            Assert.Empty(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
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
            Assert.Equal(staged.Batch.BatchId, pending.BatchId);
        }

        [Fact]
        public void StageImportBatchResultDto_RejectsNullBatch()
        {
            Assert.Throws<ArgumentNullException>(() => new StageImportBatchResultDto(null!));
        }

        [Fact]
        public async Task DecideAsync_InvalidItemKeepsExistingPendingItemUnchanged()
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
            var validItem = Assert.Single(staged.Batch.Items);

            await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(
                new DecideImportBatchRequestDto(
                    staged.Batch.BatchId,
                    new[] { validItem.ItemId + 1_000_000 },
                    ImportDecision.Learning),
                TestContext.Current.CancellationToken));

            var pending = Assert.Single(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            var unchangedItem = Assert.Single(pending.Items);
            Assert.Equal(validItem.ItemId, unchangedItem.ItemId);
            Assert.Equal(ImportItemDecision.Pending, unchangedItem.Decision);
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
                DataAccessorTestFactory.CreateImportInbox(new SqliteConnectionFactory(paths)));
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
