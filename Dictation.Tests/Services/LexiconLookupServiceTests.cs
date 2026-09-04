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
                Assert.Equal("go", result.Headword);
                Assert.Null(result.Reading);
                Assert.Equal("动词", result.PartOfSpeechLabel);
                Assert.Equal("动词", result.AttributeLabel);
                Assert.False(result.IsPhrase);
                Assert.True(result.HasChineseMeaning);
                Assert.Equal(
                    new[] { "原形", "第三人称单数", "过去式", "过去分词", "现在分词" },
                    result.Forms.Select(form => form.FormLabel));
            });
            Assert.Equal(LexiconMeaningSource.ChineseOpenWordNet, results[0].MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.Exact, results[0].MeaningPrecision);
            Assert.Equal(new[] { "去", "行走" }, results[0].Meanings);
            Assert.Equal(LexiconMeaningSource.Ecdict, results[1].MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.Broad, results[1].MeaningPrecision);
            Assert.DoesNotContain(results.SelectMany(result => result.Meanings), meaning => meaning.Contains("to ", StringComparison.Ordinal));
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
            Assert.False(result.HasChineseMeaning);
            Assert.Equal(LexiconMeaningSource.Missing, result.MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.Missing, result.MeaningPrecision);
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

        [Fact]
        public async Task GetImportGroupAsync_ReturnsMappedJapaneseEntryAndEnglishGroup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            var japanese = Assert.Single(await service.GetImportGroupAsync(
                LearningLanguage.Japanese,
                "jmdict:1:1",
                TestContext.Current.CancellationToken));
            Assert.Equal("动词", japanese.PartOfSpeechLabel);
            Assert.Contains("二类动词（一段）", japanese.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains("常用汉字", japanese.AttributeLabel, StringComparison.Ordinal);
            Assert.Contains(japanese.Forms, form =>
                form.Surface == "食べる"
                && form.FormLabel == "普通形"
                && form.DetailLabel == "汉字 · 动词");
            Assert.Equal(new[] { "吃", "吃饭" }, japanese.Meanings);
            Assert.Equal(LexiconMeaningSource.Tomoshi, japanese.MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.Exact, japanese.MeaningPrecision);

            var english = await service.GetImportGroupAsync(
                LearningLanguage.English,
                "go%2:01:00::",
                TestContext.Current.CancellationToken);
            Assert.Equal(
                new[] { "go%2:01:00::", "convert%2:30:00::" },
                english.Select(item => item.SourceSenseId));
            var go = Assert.Single(english, item => item.SourceSenseId == "go%2:01:00::");
            Assert.Equal("动词", go.PartOfSpeechLabel);
            Assert.Contains(go.Forms, form => form.Surface == "go" && form.FormLabel == "原形");
            Assert.Contains(go.Forms, form => form.Surface == "went" && form.FormLabel == "过去式");

            var convert = Assert.Single(english, item => item.SourceSenseId == "convert%2:30:00::");
            Assert.Equal("动词", convert.PartOfSpeechLabel);
            Assert.Contains(convert.Forms, form => form.Surface == "convert" && form.FormLabel == "原形");
            Assert.Contains(convert.Forms, form => form.Surface == "converted" && form.FormLabel == "过去式");
        }

        [Fact]
        public async Task GetImportGroupAsync_ReturnsEmptyForUnknownSourceSense()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            Assert.Empty(await service.GetImportGroupAsync(
                LearningLanguage.Japanese,
                "missing-entry",
                TestContext.Current.CancellationToken));
            Assert.Empty(await service.GetImportGroupAsync(
                LearningLanguage.English,
                "missing-sense",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetImportGroupAsync_RejectsUndefinedOrEmptySourceSense()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetImportGroupAsync(
                (LearningLanguage)99,
                "source-sense",
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetImportGroupAsync(
                LearningLanguage.English,
                " ",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetImportGroupAsync_PreCanceledToken_PropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var service = CreateService(paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetImportGroupAsync(
                LearningLanguage.English,
                "go%2:01:00::",
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
