using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Services.Configuration;
using Dictation.Services.Lexicon;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Services
{
    public sealed class LexiconLookupServiceTests
    {
        [Fact]
        public async Task SearchAsync_EnglishNormalizesInputMapsLabelsAndKeepsSenseRowsIndependent()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            var results = await service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.English, "  WENT  ", 20),
                TestContext.Current.CancellationToken);

            Assert.Equal(2, results.Count);
            Assert.NotEqual(results[0].SourceSenseId, results[1].SourceSenseId);
            Assert.All(results, result =>
            {
                Assert.Equal(LearningLanguage.English, result.Language);
                Assert.Equal("go%v", result.SourceLexemeId);
                Assert.Equal("go", result.Headword);
                Assert.Null(result.Reading);
                Assert.Equal("动词", result.PartOfSpeechLabel);
                Assert.Equal("动词", result.AttributeLabel);
                Assert.False(result.IsPhrase);
                Assert.Equal(
                    new[] { "原形", "第三人称单数", "过去式", "过去分词", "现在分词" },
                    result.Forms.Select(form => form.FormLabel));
            });
        }

        [Fact]
        public async Task SearchAsync_JapaneseTrimsAndNormalizesFormCAndMapsJapaneseMetadata()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            var results = await service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.Japanese, " カ\u3099 ", 20),
                TestContext.Current.CancellationToken);

            var result = Assert.Single(results);
            Assert.Equal(LearningLanguage.Japanese, result.Language);
            Assert.Equal("jmdict:3:1", result.SourceSenseId);
            Assert.Equal("jmdict:3:1", result.SourceLexemeId);
            Assert.Equal("%", result.Headword);
            Assert.Equal("%", result.Reading);
            Assert.Equal("其他", result.PartOfSpeechLabel);
            Assert.Equal("无汉字", result.AttributeLabel);
            Assert.Contains(result.Forms, form => form.Surface == "ガ" && form.DetailLabel == "假名 · 其他");
        }

        [Fact]
        public async Task SearchAsync_JapaneseMapsVerbClassesKanjiUsageAndLexicalRoleDetails()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            var ichidanResults = await service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.Japanese, "食べる", 20),
                TestContext.Current.CancellationToken);
            Assert.Equal(2, ichidanResults.Count);
            var ichidan = ichidanResults[0];
            Assert.Contains("二类动词（一段）", ichidan.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("他动词", ichidan.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("常用汉字", ichidan.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains(ichidan.Forms, form => form.Surface == "食べる" && form.DetailLabel == "汉字 · 动词");
            Assert.Contains(ichidan.Forms, form => form.Surface == "たべる" && form.DetailLabel == "假名 · 动词");

            var godan = Assert.Single(await service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.Japanese, "書く", 20),
                TestContext.Current.CancellationToken));
            Assert.Contains("一类动词（五段・く）", godan.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("他动词", godan.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("常用汉字", godan.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains(godan.Forms, form => form.DetailLabel == "汉字 · 动词");

            var rare = Assert.Single(await service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.Japanese, "跪く", 20),
                TestContext.Current.CancellationToken));
            Assert.Contains("一类动词（五段・く）", rare.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("自动词", rare.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("罕见汉字", rare.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains(rare.Forms, form => form.DetailLabel == "汉字 · 动词");

            var suru = Assert.Single(await service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.Japanese, "勉強", 20),
                TestContext.Current.CancellationToken));
            Assert.Contains("三类动词（する）", suru.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("他动词", suru.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("常用汉字", suru.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains(suru.Forms, form => form.DetailLabel == "汉字 · 名词");
            Assert.Contains(suru.Forms, form => form.DetailLabel == "假名 · 名词");
            Assert.Contains(suru.Forms, form => form.DetailLabel == "汉字 · 动词");
            Assert.Contains(suru.Forms, form => form.DetailLabel == "假名 · 动词");
        }

        [Fact]
        public async Task SearchAsync_PropagatesDataAccessFailuresInsteadOfReturningEmptyResults()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            var service = CreateService(paths);

            var exception = await Assert.ThrowsAsync<LexiconDataAccessException>(() => service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.English, "went", 20),
                TestContext.Current.CancellationToken));

            Assert.Contains("英语词库", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_EnglishDefinitionDoesNotBecomeReverseLookup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            var results = await service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.English, " to move from one place to another ", 20),
                TestContext.Current.CancellationToken);

            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchAsync_RejectsNullOrWhitespaceQuery()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.English, " \t\r\n ", 20),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SearchAsync(
                null!,
                TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(51)]
        public async Task SearchAsync_RejectsLimitOutsideOneToFifty(int limit)
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.English, "went", limit),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SearchAsync_RejectsUndefinedLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
                new LexiconSearchRequestDto((LearningLanguage)99, "went", 20),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SearchAsync_PreCanceledToken_PropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => service.SearchAsync(
                new LexiconSearchRequestDto(LearningLanguage.English, "went", 20),
                cancellation.Token));
        }

        private static LexiconLookupService CreateService(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            return new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
        }
    }
}
