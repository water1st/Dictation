using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.DataAccess
{
    public sealed class JapaneseLexiconDataAccessorTests
    {
        [Fact]
        public async Task SearchAsync_ExactPoliteForm_ReturnsIndependentEntriesWithEnglishMeaningsAndFullForms()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateJapanese(paths);
            var accessor = CreateAccessor(paths);

            var results = await accessor.SearchAsync("食べます", 20, TestContext.Current.CancellationToken);

            Assert.Equal(2, results.Count);
            Assert.NotEqual(results[0].EntryId, results[1].EntryId);
            Assert.All(results, result =>
            {
                Assert.Equal("食べる", result.PrimaryWritten);
                Assert.Equal("たべる", result.PrimaryReading);
                Assert.Equal("Verb", result.LexicalCategory);
                Assert.Equal("Ichidan", result.VerbClass);
                Assert.Equal("Transitive", result.Transitivity);
                Assert.Equal("Common", result.KanjiUsage);
                Assert.False(result.IsPhrase);
                Assert.Contains("Ichidan verb", result.PartsOfSpeech);
                Assert.Contains("to", result.Meanings[0], StringComparison.Ordinal);
                Assert.DoesNotContain(result.Meanings, meaning => meaning == "essen");
                Assert.Equal(
                    new[] { "食べる", "たべる", "食べます", "たべます", "食べて", "たべて", "食べた", "たべた" },
                    result.Forms.Select(form => form.Surface));
            });
        }

        [Fact]
        public async Task SearchAsync_PrefixMatch_ReturnsStableNumericSourceOrderAndHonorsLimit()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateJapanese(paths);
            var accessor = CreateAccessor(paths);

            var first = await accessor.SearchAsync("食", 20, TestContext.Current.CancellationToken);
            var limited = await accessor.SearchAsync("食べ", 1, TestContext.Current.CancellationToken);
            var repeated = await accessor.SearchAsync("食", 20, TestContext.Current.CancellationToken);

            Assert.Equal(
                new[] { "jmdict:1:1", "jmdict:1:2", "jmdict:2:1", "jmdict:10:1" },
                first.Select(result => result.EntryId));
            Assert.Single(limited);
            Assert.Equal("jmdict:1:1", limited[0].EntryId);
            Assert.Equal(first.Select(result => result.EntryId), repeated.Select(result => result.EntryId));
        }

        [Fact]
        public async Task SearchAsync_OnlyMatchesFormsAndEscapesLikeWildcards()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateJapanese(paths);
            var accessor = CreateAccessor(paths);

            var percent = await accessor.SearchAsync("%", 20, TestContext.Current.CancellationToken);
            var underscore = await accessor.SearchAsync("_", 20, TestContext.Current.CancellationToken);
            var backslash = await accessor.SearchAsync("\\", 20, TestContext.Current.CancellationToken);
            var meaning = await accessor.SearchAsync("to eat", 20, TestContext.Current.CancellationToken);

            Assert.Single(percent);
            Assert.Equal("jmdict:3:1", percent[0].EntryId);
            Assert.Single(underscore);
            Assert.Equal("jmdict:3:1", underscore[0].EntryId);
            Assert.Single(backslash);
            Assert.Equal("jmdict:3:1", backslash[0].EntryId);
            Assert.Empty(meaning);
        }

        [Fact]
        public async Task SearchAsync_PreCanceledToken_PropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateJapanese(paths);
            var accessor = CreateAccessor(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => accessor.SearchAsync(
                "食べる", 20, cancellation.Token));
        }

        [Fact]
        public async Task SearchAsync_MissingDatabase_ThrowsChineseLexiconDataAccessException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var accessor = CreateAccessor(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => accessor.SearchAsync(
                "食べる", 20, TestContext.Current.CancellationToken));

            Assert.Contains("日语词库", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public async Task SearchAsync_WrongSchemaVersion_ThrowsChineseLexiconDataAccessException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateJapanese(paths, "2");
            var accessor = CreateAccessor(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => accessor.SearchAsync(
                "食べる", 20, TestContext.Current.CancellationToken));

            Assert.Contains("schema_version", exception.Message, StringComparison.Ordinal);
            Assert.Contains("1", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_CorruptDatabase_ThrowsChineseLexiconDataAccessException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            Directory.CreateDirectory(paths.Lexicons);
            File.WriteAllText(paths.JapaneseLexiconDatabase, "not a sqlite database");
            var accessor = CreateAccessor(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => accessor.SearchAsync(
                "食べる", 20, TestContext.Current.CancellationToken));

            Assert.Contains("日语词库", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public async Task GetByEntryIdAsync_ReturnsOneCompleteEntryWithoutSearchingGlosses()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateJapanese(paths);
            var accessor = CreateAccessor(paths);

            var result = await accessor.GetByEntryIdAsync(
                "jmdict:1:1",
                TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal("jmdict:1:1", result!.EntryId);
            Assert.Equal("食べる", result.PrimaryWritten);
            Assert.Contains("to eat", result.Meanings[0], StringComparison.Ordinal);
            Assert.Equal(8, result.Forms.Count);
            Assert.Null(await accessor.GetByEntryIdAsync(
                "to eat",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetByEntryIdAsync_PreCanceledTokenPropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateJapanese(paths);
            var accessor = CreateAccessor(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => accessor.GetByEntryIdAsync(
                "jmdict:1:1",
                cancellation.Token));
        }

        private static JapaneseLexiconDataAccessor CreateAccessor(ApplicationPaths paths)
        {
            return new JapaneseLexiconDataAccessor(new LexiconSqliteConnectionFactory(paths));
        }
    }
}
