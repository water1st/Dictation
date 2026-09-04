using System.Text;

using Dictation.LexiconBuilder;
using Dictation.LexiconBuilder.WordNet;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.LexiconBuilder
{
    public sealed class ChineseOpenWordNetParserTests
    {
        [Fact]
        public void ParseByIli_ResolvesMemberOrderAndDeduplicatesMeanings()
        {
            using var directory = new TemporaryDirectory();
            var path = WriteXml(directory.Path, """
                <LexicalResource>
                  <Lexicon id="test" language="cmn-Hans">
                    <LexicalEntry id="eat"><Lemma writtenForm="吃" /><Sense id="sense-eat" synset="synset-1" /></LexicalEntry>
                    <LexicalEntry id="consume"><Lemma writtenForm="食用" /><Sense id="sense-consume" synset="synset-1" /></LexicalEntry>
                    <LexicalEntry id="eat-again"><Lemma writtenForm="吃" /><Sense id="sense-eat-again" synset="synset-1" /></LexicalEntry>
                    <Synset id="synset-1" ili="i12345" members="sense-consume sense-eat sense-eat-again" />
                  </Lexicon>
                </LexicalResource>
                """);

            var result = new ChineseOpenWordNetParser().ParseByIli(path);

            Assert.Equal(new[] { "食用", "吃" }, result["i12345"]);
        }

        [Fact]
        public void ParseByIli_SkipsSynsetsWithoutUsableIli()
        {
            using var directory = new TemporaryDirectory();
            var path = WriteXml(directory.Path, """
                <LexicalResource>
                  <Lexicon id="test" language="cmn-Hans">
                    <LexicalEntry id="eat"><Lemma writtenForm="吃" /><Sense id="sense-eat" synset="synset-1" /></LexicalEntry>
                    <Synset id="synset-1" members="sense-eat" />
                    <Synset id="synset-2" ili="   " members="sense-eat" />
                  </Lexicon>
                </LexicalResource>
                """);

            var result = new ChineseOpenWordNetParser().ParseByIli(path);

            Assert.Empty(result);
        }

        [Fact]
        public void ParseByIli_RejectsUnknownMemberReference()
        {
            using var directory = new TemporaryDirectory();
            var path = WriteXml(directory.Path, """
                <LexicalResource>
                  <Lexicon id="test" language="cmn-Hans">
                    <Synset id="synset-1" ili="i12345" members="missing-sense" />
                  </Lexicon>
                </LexicalResource>
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new ChineseOpenWordNetParser().ParseByIli(path));

            Assert.Contains("missing-sense", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ParseByIli_RejectsConflictingSenseDefinitions()
        {
            using var directory = new TemporaryDirectory();
            var path = WriteXml(directory.Path, """
                <LexicalResource>
                  <Lexicon id="test" language="cmn-Hans">
                    <LexicalEntry id="eat"><Lemma writtenForm="吃" /><Sense id="same-sense" synset="synset-1" /></LexicalEntry>
                    <LexicalEntry id="consume"><Lemma writtenForm="食用" /><Sense id="same-sense" synset="synset-1" /></LexicalEntry>
                    <Synset id="synset-1" ili="i12345" members="same-sense" />
                  </Lexicon>
                </LexicalResource>
                """);

            var exception = Assert.Throws<LexiconBuildException>(() => new ChineseOpenWordNetParser().ParseByIli(path));

            Assert.Contains("same-sense", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseByIli_RejectsBlankPath(string path)
        {
            Assert.Throws<ArgumentException>(() => new ChineseOpenWordNetParser().ParseByIli(path));
        }

        [Fact]
        public void ParseByIli_RejectsMissingFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "Dictation.Tests", Guid.NewGuid().ToString("N"), "omw-cmn.xml");

            Assert.Throws<FileNotFoundException>(() => new ChineseOpenWordNetParser().ParseByIli(path));
        }

        private static string WriteXml(string directory, string content)
        {
            var path = Path.Combine(directory, "omw-cmn.xml");
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return path;
        }
    }
}
