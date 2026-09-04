using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.DataAccess
{
    public sealed class EnglishLexiconDataAccessorTests
    {
        [Fact]
        public async Task SearchAsync_AnyVerbForm_ReturnsIndependentSensesSharingLexemeAndForms()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);

            var results = await accessor.SearchAsync("went", 20, TestContext.Current.CancellationToken);

            Assert.Equal(2, results.Count);
            Assert.NotEqual(results[0].LearningSenseId, results[1].LearningSenseId);
            Assert.All(results, result =>
            {
                Assert.Equal("go", result.Headword);
                Assert.Equal("v", result.PartOfSpeech);
                Assert.False(result.IsPhrase);
                Assert.NotEmpty(result.Meanings);
                Assert.DoesNotContain(result.Meanings, meaning => meaning.Contains("to ", StringComparison.Ordinal));
                Assert.Equal(new[] { "go", "goes", "went", "gone", "going" }, result.Forms.Select(form => form.Surface));
            });
            Assert.Equal("go%v", results[0].SourceLexemeId);
            Assert.Equal("ChineseOpenWordNet", results[0].MeaningSource);
            Assert.Equal("Exact", results[0].MeaningPrecision);
            Assert.Equal(new[] { "去", "行走" }, results[0].Meanings);
            Assert.Equal("oewn:go:v", results[1].SourceLexemeId);
            Assert.Equal("Ecdict", results[1].MeaningSource);
            Assert.Equal("Broad", results[1].MeaningPrecision);
            Assert.Equal(new[] { "运行", "运转" }, results[1].Meanings);
            Assert.Single(results, result => result.LearningSenseId == "ecdict:go:v");
        }

        [Fact]
        public async Task SearchAsync_PrefixMatch_ReturnsStableSenseOrderAndHonorsLimit()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);

            var first = await accessor.SearchAsync("go", 20, TestContext.Current.CancellationToken);
            var limited = await accessor.SearchAsync("go", 1, TestContext.Current.CancellationToken);
            var repeated = await accessor.SearchAsync("go", 20, TestContext.Current.CancellationToken);

            Assert.Equal(new[] { "go%2:01:00::", "ecdict:go:v" }, first.Select(result => result.LearningSenseId));
            Assert.Single(limited);
            Assert.Equal("go%2:01:00::", limited[0].LearningSenseId);
            Assert.Equal(first.Select(result => result.LearningSenseId), repeated.Select(result => result.LearningSenseId));
        }

        [Fact]
        public async Task SearchAsync_OnlyMatchesFormsAndEscapesLikeWildcards()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);

            var percent = await accessor.SearchAsync("%", 20, TestContext.Current.CancellationToken);
            var underscore = await accessor.SearchAsync("_", 20, TestContext.Current.CancellationToken);
            var backslash = await accessor.SearchAsync("\\", 20, TestContext.Current.CancellationToken);
            var definition = await accessor.SearchAsync("to move from one place to another", 20, TestContext.Current.CancellationToken);

            Assert.Single(percent);
            Assert.Equal("missing:symbols:v", percent[0].LearningSenseId);
            Assert.Single(underscore);
            Assert.Equal("missing:symbols:v", underscore[0].LearningSenseId);
            Assert.Single(backslash);
            Assert.Equal("missing:symbols:v", backslash[0].LearningSenseId);
            Assert.Empty(percent[0].Meanings);
            Assert.Equal("Missing", percent[0].MeaningSource);
            Assert.Equal("Missing", percent[0].MeaningPrecision);
            Assert.Empty(definition);
        }

        [Fact]
        public async Task SearchAsync_Phrase_ReturnsOnlyDatabaseBaseForm()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);

            var results = await accessor.SearchAsync("take off", 20, TestContext.Current.CancellationToken);

            var result = Assert.Single(results);
            Assert.True(result.IsPhrase);
            var form = Assert.Single(result.Forms);
            Assert.Equal("take off", form.Surface);
            Assert.Equal("Base", form.FormKind);
        }

        [Fact]
        public async Task SearchAsync_PreCanceledToken_PropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => accessor.SearchAsync(
                "went", 20, cancellation.Token));
        }

        [Fact]
        public async Task SearchAsync_MissingDatabase_ThrowsChineseLexiconDataAccessException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var accessor = CreateAccessor(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => accessor.SearchAsync(
                "went", 20, TestContext.Current.CancellationToken));

            Assert.Contains("英语词库", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public async Task SearchAsync_WrongSchemaVersion_ThrowsChineseLexiconDataAccessException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths, "1");
            var accessor = CreateAccessor(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => accessor.SearchAsync(
                "went", 20, TestContext.Current.CancellationToken));

            Assert.Contains("schema_version", exception.Message, StringComparison.Ordinal);
            Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_CorruptDatabase_ThrowsChineseLexiconDataAccessException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            Directory.CreateDirectory(paths.Lexicons);
            File.WriteAllText(paths.EnglishLexiconDatabase, "not a sqlite database");
            var accessor = CreateAccessor(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => accessor.SearchAsync(
                "went", 20, TestContext.Current.CancellationToken));

            Assert.Contains("英语词库", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public async Task GetSenseGroupAsync_UsesLearningSenseAnchorOneLayerUndirectedDerivationsAndExcludesPhrases()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);

            var result = await accessor.GetSenseGroupAsync(
                "go%2:01:00::",
                TestContext.Current.CancellationToken);

            Assert.Equal(
                new[] { "go%2:01:00::", "convert%2:30:00::" },
                result.Select(item => item.LearningSenseId));
            Assert.DoesNotContain(result, item => item.LearningSenseId == "missing:conversion:n");
            Assert.DoesNotContain(result, item => item.LearningSenseId == "ecdict:go:v");
            Assert.DoesNotContain(result, item => item.IsPhrase);
            Assert.Contains(result, item => item.Headword == "convert");
            Assert.Empty(await accessor.GetSenseGroupAsync(
                "missing-sense",
                TestContext.Current.CancellationToken));
            Assert.Empty(await accessor.GetSenseGroupAsync(
                "to move from one place to another",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetSenseGroupAsync_PhraseAnchorReturnsOnlyAnchor()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);

            var result = await accessor.GetSenseGroupAsync(
                "take_off%2:38:00::",
                TestContext.Current.CancellationToken);

            var item = Assert.Single(result);
            Assert.Equal("take off", item.Headword);
            Assert.True(item.IsPhrase);
        }

        [Fact]
        public async Task GetSenseGroupAsync_PreCanceledToken_PropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateEnglish(paths);
            var accessor = CreateAccessor(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => accessor.GetSenseGroupAsync(
                "go%2:01:00::",
                cancellation.Token));
        }

        private static EnglishLexiconDataAccessor CreateAccessor(ApplicationPaths paths)
        {
            return new EnglishLexiconDataAccessor(new LexiconSqliteConnectionFactory(paths));
        }
    }
}
