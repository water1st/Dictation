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
            Assert.NotEqual(results[0].SenseId, results[1].SenseId);
            Assert.All(results, result =>
            {
                Assert.Equal("go%v", result.LexemeId);
                Assert.Equal("go", result.Lemma);
                Assert.Equal("v", result.SourceEntryKey);
                Assert.Equal("v", result.PartOfSpeech);
                Assert.False(result.IsPhrase);
                Assert.NotEmpty(result.Meanings);
                Assert.Equal(new[] { "go", "goes", "went", "gone", "going" }, result.Forms.Select(form => form.Surface));
            });
            Assert.Contains("move", results[0].Meanings[0], StringComparison.Ordinal);
            Assert.Contains("operate", results[1].Meanings[0], StringComparison.Ordinal);
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

            Assert.Equal(new[] { "go%2:01:00::", "go%2:02:00::" }, first.Select(result => result.SenseId));
            Assert.Single(limited);
            Assert.Equal("go%2:01:00::", limited[0].SenseId);
            Assert.Equal(first.Select(result => result.SenseId), repeated.Select(result => result.SenseId));
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
            Assert.Equal("symbols%2:30:00::", percent[0].SenseId);
            Assert.Single(underscore);
            Assert.Equal("symbols%2:30:00::", underscore[0].SenseId);
            Assert.Single(backslash);
            Assert.Equal("symbols%2:30:00::", backslash[0].SenseId);
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
            LexiconTestDatabases.CreateEnglish(paths, "2");
            var accessor = CreateAccessor(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => accessor.SearchAsync(
                "went", 20, TestContext.Current.CancellationToken));

            Assert.Contains("schema_version", exception.Message, StringComparison.Ordinal);
            Assert.Contains("1", exception.Message, StringComparison.Ordinal);
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

        private static EnglishLexiconDataAccessor CreateAccessor(ApplicationPaths paths)
        {
            return new EnglishLexiconDataAccessor(new LexiconSqliteConnectionFactory(paths));
        }
    }
}
