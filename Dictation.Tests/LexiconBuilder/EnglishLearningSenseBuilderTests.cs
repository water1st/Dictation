using Dictation.LexiconBuilder.WordNet;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.LexiconBuilder
{
    public sealed class EnglishLearningSenseBuilderTests
    {
        [Fact]
        public void Build_CreatesExactSenseAndOneGroupedBroadFallback()
        {
            using var directory = new TemporaryDirectory();
            var ecdictPath = EcdictTranslationReaderTests.CreateDatabase(
                directory.Path,
                new Dictionary<string, string> { ["go"] = "vi. 去；行走\nn. 围棋" });
            using var ecdict = new EcdictTranslationReader(ecdictPath);
            var lexicon = CreateGoLexicon(includeExactIli: true);
            var chineseMeaningsByIli = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["i12345"] = new[] { "前往" }
            };

            var results = new EnglishLearningSenseBuilder().Build(lexicon, chineseMeaningsByIli, ecdict);

            var exact = Assert.Single(results, item => item.MeaningPrecision == "Exact");
            Assert.Equal("go%2:01:00::", exact.LearningSenseId);
            Assert.Equal("go%v", exact.SourceLexemeId);
            Assert.Equal("ChineseOpenWordNet", exact.MeaningSource);
            Assert.Equal(new[] { "go%2:01:00::" }, exact.SourceSenseIds);
            Assert.Equal(new[] { "前往" }, exact.Meanings);

            var broad = Assert.Single(results, item => item.MeaningPrecision == "Broad");
            Assert.Equal("ecdict:go:v", broad.LearningSenseId);
            Assert.Equal("oewn:go:v", broad.SourceLexemeId);
            Assert.Equal("go", broad.Headword);
            Assert.Equal(EnglishPartOfSpeech.Verb, broad.PartOfSpeech);
            Assert.False(broad.IsPhrase);
            Assert.Equal("Ecdict", broad.MeaningSource);
            Assert.Equal(new[] { "go%2:02:00::", "go%2:03:00::" }, broad.SourceSenseIds);
            Assert.Equal(new[] { "去；行走" }, broad.Meanings);
            Assert.True(exact.SortOrder < broad.SortOrder);
            Assert.DoesNotContain("去；行走", exact.Meanings);
        }

        [Fact]
        public void Build_WhenEcdictHasNoMatchingText_CreatesOneMissingFallback()
        {
            using var directory = new TemporaryDirectory();
            var ecdictPath = EcdictTranslationReaderTests.CreateDatabase(
                directory.Path,
                new Dictionary<string, string> { ["go"] = "n. 围棋" });
            using var ecdict = new EcdictTranslationReader(ecdictPath);

            var results = new EnglishLearningSenseBuilder().Build(
                CreateGoLexicon(includeExactIli: false),
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ecdict);

            var missing = Assert.Single(results);
            Assert.Equal("missing:go:v", missing.LearningSenseId);
            Assert.Equal("oewn:go:v", missing.SourceLexemeId);
            Assert.Equal("Missing", missing.MeaningSource);
            Assert.Equal("Missing", missing.MeaningPrecision);
            Assert.Equal(3, missing.SourceSenseIds.Count);
            Assert.Empty(missing.Meanings);
        }

        [Fact]
        public void Build_GroupsUncoveredSensesAcrossLexemesWithSameLemmaAndPartOfSpeech()
        {
            using var directory = new TemporaryDirectory();
            var ecdictPath = EcdictTranslationReaderTests.CreateDatabase(
                directory.Path,
                new Dictionary<string, string> { ["bank"] = "n. 银行" });
            using var ecdict = new EcdictTranslationReader(ecdictPath);
            var lexicon = new WordNetLexicon(
                new[]
                {
                    CreateLexeme("bank%n-1", "Bank", EnglishPartOfSpeech.Noun, "bank%1:01:00::", "00000001-n"),
                    CreateLexeme("bank%n-2", "bank", EnglishPartOfSpeech.Noun, "bank%1:02:00::", "00000002-n")
                },
                new[]
                {
                    CreateSynset("00000001-n", EnglishPartOfSpeech.Noun, null),
                    CreateSynset("00000002-n", EnglishPartOfSpeech.Noun, null)
                });

            var results = new EnglishLearningSenseBuilder().Build(
                lexicon,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ecdict);

            var broad = Assert.Single(results);
            Assert.Equal("ecdict:bank:n", broad.LearningSenseId);
            Assert.Equal(new[] { "bank%1:01:00::", "bank%1:02:00::" }, broad.SourceSenseIds);
        }

        [Fact]
        public void Build_TreatsEmptyCowMeaningsAsUncovered()
        {
            using var directory = new TemporaryDirectory();
            var ecdictPath = EcdictTranslationReaderTests.CreateDatabase(
                directory.Path,
                new Dictionary<string, string> { ["go"] = "v. 去" });
            using var ecdict = new EcdictTranslationReader(ecdictPath);
            var meanings = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["i12345"] = new[] { "", "   " }
            };

            var results = new EnglishLearningSenseBuilder().Build(CreateGoLexicon(includeExactIli: true), meanings, ecdict);

            var broad = Assert.Single(results);
            Assert.Equal(3, broad.SourceSenseIds.Count);
        }

        [Fact]
        public void Build_RejectsNullDependencies()
        {
            using var directory = new TemporaryDirectory();
            var ecdictPath = EcdictTranslationReaderTests.CreateDatabase(directory.Path, new Dictionary<string, string>());
            using var ecdict = new EcdictTranslationReader(ecdictPath);
            var builder = new EnglishLearningSenseBuilder();
            var lexicon = CreateGoLexicon(includeExactIli: false);
            var meanings = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            Assert.Throws<ArgumentNullException>(() => builder.Build(null!, meanings, ecdict));
            Assert.Throws<ArgumentNullException>(() => builder.Build(lexicon, null!, ecdict));
            Assert.Throws<ArgumentNullException>(() => builder.Build(lexicon, meanings, null!));
        }

        private static WordNetLexicon CreateGoLexicon(bool includeExactIli)
        {
            return new WordNetLexicon(
                new[]
                {
                    new EnglishLexeme(
                        "go%v",
                        "go",
                        "v",
                        EnglishPartOfSpeech.Verb,
                        Array.Empty<string>(),
                        new[]
                        {
                            new EnglishSense("go%2:01:00::", "00000001-v", 1, Array.Empty<string>()),
                            new EnglishSense("go%2:02:00::", "00000002-v", 2, Array.Empty<string>()),
                            new EnglishSense("go%2:03:00::", "00000003-v", 3, Array.Empty<string>())
                        },
                        isPhrase: false)
                },
                new[]
                {
                    CreateSynset("00000001-v", EnglishPartOfSpeech.Verb, includeExactIli ? "i12345" : null),
                    CreateSynset("00000002-v", EnglishPartOfSpeech.Verb, null),
                    CreateSynset("00000003-v", EnglishPartOfSpeech.Verb, null)
                });
        }

        private static EnglishLexeme CreateLexeme(
            string lexemeId,
            string lemma,
            EnglishPartOfSpeech partOfSpeech,
            string senseId,
            string synsetId)
        {
            return new EnglishLexeme(
                lexemeId,
                lemma,
                partOfSpeech == EnglishPartOfSpeech.Noun ? "n" : "v",
                partOfSpeech,
                Array.Empty<string>(),
                new[] { new EnglishSense(senseId, synsetId, 1, Array.Empty<string>()) },
                isPhrase: false);
        }

        private static EnglishSynset CreateSynset(
            string synsetId,
            EnglishPartOfSpeech partOfSpeech,
            string? ili)
        {
            return new EnglishSynset(synsetId, partOfSpeech, new[] { "source definition" }, Array.Empty<string>(), ili);
        }
    }
}
