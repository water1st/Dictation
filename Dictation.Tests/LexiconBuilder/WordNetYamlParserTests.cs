using System.Text;

using Dictation.LexiconBuilder;
using Dictation.LexiconBuilder.WordNet;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.LexiconBuilder
{
    /// <summary>
    /// 验证WordNet词元与同义词集解析及引用完整性。
    /// </summary>
    public sealed class WordNetYamlParserTests
    {
        [Fact]
        public void ParseDirectory_MapsLexicalEntriesFormsSensesAndSynsets()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-z.yaml", """
                zed:
                  n-1:
                    form:
                    - zeds
                    sense:
                    - id: 'zed%1:04:00::'
                      derivation:
                      - 'zed%2:41:00::'
                      synset: 00000001-n
                  n-2:
                    sense:
                    - id: 'zed%1:06:00::'
                      synset: 00000002-n
                  v:
                    sense:
                    - id: 'zed%2:41:00::'
                      synset: 00000003-v
                """);
            WriteFile(directory.Path, "entries-a.yaml", """
                agile:
                  a:
                    sense:
                    - id: 'agile%3:00:00::'
                      synset: 00000004-s
                """);
            WriteFile(directory.Path, "noun.test.yaml", """
                00000001-n:
                  definition:
                  - a first sense
                  example:
                  - a first example
                  ili: i12345
                  partOfSpeech: n
                00000002-n:
                  definition:
                  - a second sense
                  partOfSpeech: n
                """);
            WriteFile(directory.Path, "verb.test.yaml", """
                00000003-v:
                  definition:
                  - a verb sense
                  partOfSpeech: v
                """);
            WriteFile(directory.Path, "adj.test.yaml", """
                00000004-s:
                  definition:
                  - an adjective satellite
                  partOfSpeech: s
                """);

            var result = new WordNetYamlParser().ParseDirectory(directory.Path);

            Assert.Equal(
                new[] { "agile%a", "zed%n-1", "zed%n-2", "zed%v" },
                result.Lexemes.Select(lexeme => lexeme.LexemeId));
            var nounVariant = result.Lexemes.Single(lexeme => lexeme.SourceEntryKey == "n-1");
            Assert.Equal("zed", nounVariant.Lemma);
            Assert.Equal(EnglishPartOfSpeech.Noun, nounVariant.PartOfSpeech);
            Assert.False(nounVariant.IsPhrase);
            Assert.Equal(new[] { "zeds" }, nounVariant.SourceForms);
            Assert.Equal("zed%1:04:00::", nounVariant.Senses.Single().SenseId);
            Assert.Equal("00000001-n", nounVariant.Senses.Single().SynsetId);
            Assert.Equal(new[] { "zed%2:41:00::" }, nounVariant.Senses.Single().DerivationSenseIds);
            Assert.Equal("a first sense", result.Synsets.Single(synset => synset.SynsetId == "00000001-n").Definitions.Single());
            Assert.Equal("a first example", result.Synsets.Single(synset => synset.SynsetId == "00000001-n").Examples.Single());
            Assert.Equal("i12345", result.Synsets.Single(synset => synset.SynsetId == "00000001-n").Ili);
            Assert.Null(result.Synsets.Single(synset => synset.SynsetId == "00000002-n").Ili);
            Assert.Equal(EnglishPartOfSpeech.AdjectiveSatellite, result.Synsets.Single(synset => synset.SynsetId == "00000004-s").PartOfSpeech);
        }

        [Fact]
        public void ParseDirectory_RecognizesPhraseAndKeepsAdjectiveSatelliteSense()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-t.yaml", """
                take off:
                  v:
                    sense:
                    - id: 'take_off%2:38:00::'
                      synset: 00000010-v
                bright:
                  a:
                    sense:
                    - id: 'bright%3:00:00::'
                      synset: 00000011-s
                """);
            WriteFile(directory.Path, "verb.test.yaml", """
                00000010-v:
                  definition:
                  - leave
                  partOfSpeech: v
                """);
            WriteFile(directory.Path, "adj.test.yaml", """
                00000011-s:
                  definition:
                  - intelligent
                  partOfSpeech: s
                """);

            var result = new WordNetYamlParser().ParseDirectory(directory.Path);

            Assert.True(result.Lexemes.Single(lexeme => lexeme.Lemma == "take off").IsPhrase);
            var bright = result.Lexemes.Single(lexeme => lexeme.Lemma == "bright");
            Assert.Equal(EnglishPartOfSpeech.Adjective, bright.PartOfSpeech);
            Assert.Equal("00000011-s", bright.Senses.Single().SynsetId);
        }

        [Fact]
        public void ParseDirectory_RejectsEmptyDirectoryWithSourceContext()
        {
            using var directory = new TemporaryDirectory();

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains(directory.Path, exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseDirectory_RejectsBlankPath(string path)
        {
            Assert.Throws<ArgumentException>(() => new WordNetYamlParser().ParseDirectory(path));
        }

        [Fact]
        public void ParseDirectory_RejectsMissingDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "Dictation.Tests", Guid.NewGuid().ToString("N"));

            var exception = Assert.Throws<DirectoryNotFoundException>(() => new WordNetYamlParser().ParseDirectory(path));

            Assert.Contains(path, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsUnknownLexicalEntryKey()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", """
                apple:
                  x:
                    sense:
                    - id: 'apple%1:13:00::'
                      synset: 00000020-n
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("apple", exception.Message, StringComparison.Ordinal);
            Assert.Contains("x", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsNonDecimalLexicalEntrySuffix()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", """
                apple:
                  n-+1:
                    sense:
                    - id: 'apple%1:13:00::'
                      synset: 00000020-n
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("n-+1", exception.Message, StringComparison.Ordinal);
            Assert.Contains("entries-a.yaml", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsMalformedYamlWithFileContext()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", "apple: [");

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("entries-a.yaml", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsDuplicateSenseId()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", """
                apple:
                  n:
                    sense:
                    - id: 'shared%1:13:00::'
                      synset: 00000020-n
                apricot:
                  n:
                    sense:
                    - id: 'shared%1:13:00::'
                      synset: 00000021-n
                """);
            WriteFile(directory.Path, "noun.test.yaml", """
                00000020-n:
                  definition: [fruit]
                  partOfSpeech: n
                00000021-n:
                  definition: [another fruit]
                  partOfSpeech: n
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("shared%1:13:00::", exception.Message, StringComparison.Ordinal);
            Assert.Contains("重复", exception.Message, StringComparison.Ordinal);
            Assert.Contains("entries-a.yaml", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsMissingSynsetAndDerivationTarget()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", """
                apple:
                  n:
                    sense:
                    - id: 'apple%1:13:00::'
                      derivation:
                      - 'missing%2:41:00::'
                      synset: 00000020-n
                """);
            WriteFile(directory.Path, "noun.test.yaml", """
                00000020-n:
                  definition: [fruit]
                  partOfSpeech: n
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("missing%2:41:00::", exception.Message, StringComparison.Ordinal);
            Assert.Contains("derivation", exception.Message, StringComparison.Ordinal);
            Assert.Contains("entries-a.yaml", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsMissingSynsetWithEntrySourceContext()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", """
                apple:
                  n:
                    sense:
                    - id: 'apple%1:13:00::'
                      synset: 09999999-n
                """);
            WriteFile(directory.Path, "noun.test.yaml", """
                00000020-n:
                  definition: [fruit]
                  partOfSpeech: n
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("09999999-n", exception.Message, StringComparison.Ordinal);
            Assert.Contains("entries-a.yaml", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsPartOfSpeechMismatchWithEntrySourceContext()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", """
                apple:
                  n:
                    sense:
                    - id: 'apple%1:13:00::'
                      synset: 00000020-v
                """);
            WriteFile(directory.Path, "verb.test.yaml", """
                00000020-v:
                  definition: [to fruit]
                  partOfSpeech: v
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("apple%1:13:00::", exception.Message, StringComparison.Ordinal);
            Assert.Contains("entries-a.yaml", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseDirectory_RejectsUnknownSynsetPartOfSpeech()
        {
            using var directory = new TemporaryDirectory();
            WriteFile(directory.Path, "entries-a.yaml", """
                apple:
                  n:
                    sense:
                    - id: 'apple%1:13:00::'
                      synset: 00000020-n
                """);
            WriteFile(directory.Path, "noun.test.yaml", """
                00000020-n:
                  definition: [fruit]
                  partOfSpeech: x
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new WordNetYamlParser().ParseDirectory(directory.Path));

            Assert.Contains("00000020-n", exception.Message, StringComparison.Ordinal);
            Assert.Contains("x", exception.Message, StringComparison.Ordinal);
        }

        private static void WriteFile(string directoryPath, string fileName, string content)
        {
            File.WriteAllText(Path.Combine(directoryPath, fileName), content, Encoding.UTF8);
        }
    }
}
