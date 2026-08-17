using System.Text;

using Dictation.LexiconBuilder.JmDict;

namespace Dictation.Tests.LexiconBuilder
{

    public sealed class JmDictParserTests
    {
        [Fact]
        public void Parse_ReadsEntryAndDefaultEnglishGloss()
        {
            var parser = new JmDictParser();
            var entries = parser.Parse(CreateXmlStream($"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE JMdict [<!ENTITY v1 "Ichidan verb">]>
                <JMdict>
                  <entry>
                    <ent_seq>12345</ent_seq>
                    <k_ele><keb>食べる</keb><ke_pri>ichi1</ke_pri></k_ele>
                    <r_ele><reb>たべる</reb><re_pri>ichi1</re_pri></r_ele>
                    <sense><pos>&v1;</pos><gloss>to eat</gloss><gloss xml:lang="dut">eten</gloss></sense>
                  </entry>
                </JMdict>
                """)).ToArray();

            Assert.Single(entries);
            Assert.Equal(12345, entries[0].Sequence);
            Assert.Equal(new[] { "食べる" }, entries[0].Orthographies.Select(x => x.Text));
            Assert.Equal(new[] { "たべる" }, entries[0].Readings.Select(x => x.Text));
            Assert.Equal("Ichidan verb", entries[0].Senses[0].PartsOfSpeech.Single());
            Assert.Equal("eng", entries[0].Senses[0].Glosses[0].Language);
            Assert.Equal("dut", entries[0].Senses[0].Glosses[1].Language);
        }

        [Fact]
        public void Parse_InheritsPartsOfSpeechAndPreservesRestrictions()
        {
            var parser = new JmDictParser();
            var entry = parser.Parse(CreateXmlStream("""
                <!DOCTYPE JMdict [<!ENTITY v5 "Godan verb">]>
                <JMdict><entry>
                  <ent_seq>12346</ent_seq>
                  <k_ele><keb>書く</keb><ke_pri>ichi1</ke_pri></k_ele>
                  <k_ele><keb>かく</keb></k_ele>
                  <r_ele><reb>かく</reb><re_restr>書く</re_restr></r_ele>
                  <r_ele><reb>カク</reb><re_nokanji>nokanji</re_nokanji></r_ele>
                  <sense><pos>&v5;</pos><stagk>書く</stagk><stagr>かく</stagr><gloss>to write</gloss></sense>
                  <sense><gloss>to draw</gloss></sense>
                </entry></JMdict>
                """)).Single();

            Assert.Equal(new[] { "Godan verb" }, entry.Senses[0].PartsOfSpeech);
            Assert.Equal(new[] { "Godan verb" }, entry.Senses[1].PartsOfSpeech);
            Assert.Equal(new[] { "書く" }, entry.Readings[0].Restrictions);
            Assert.False(entry.Readings[0].NoKanji);
            Assert.True(entry.Readings[1].NoKanji);
            Assert.Equal(new[] { "書く" }, entry.Senses[0].KanjiRestrictions);
            Assert.Equal(new[] { "かく" }, entry.Senses[0].ReadingRestrictions);
        }

        [Fact]
        public void Parse_EmptyStreamProducesNoEntries()
        {
            Assert.Empty(new JmDictParser().Parse(new MemoryStream()));
        }

        [Fact]
        public void Parse_RejectsInvalidSequenceAndMissingReading()
        {
            var parser = new JmDictParser();
            Assert.Throws<LexiconBuildException>(() => parser.Parse(CreateXmlStream("<JMdict><entry><ent_seq>nope</ent_seq><r_ele><reb>x</reb></r_ele></entry></JMdict>")).ToArray());
            Assert.Throws<LexiconBuildException>(() => parser.Parse(CreateXmlStream("<JMdict><entry><ent_seq>1</ent_seq></entry></JMdict>")).ToArray());
        }

        [Fact]
        public void Parse_InlineDtdDoesNotResolveExternalResources()
        {
            var xml = "<!DOCTYPE JMdict [<!ENTITY v1 \"Ichidan verb\">]><JMdict><entry><ent_seq>1</ent_seq><r_ele><reb>x</reb></r_ele><sense><pos>&v1;</pos><gloss>x</gloss></sense></entry></JMdict>";
            var entry = new JmDictParser().Parse(CreateXmlStream(xml)).Single();

            Assert.Equal("Ichidan verb", entry.Senses[0].PartsOfSpeech.Single());
        }

        [Fact]
        public void Parse_SkipsEmptyAndNonEnglishOnlySensesButKeepsTypedCanonicalGlosses()
        {
            var entry = new JmDictParser().Parse(CreateXmlStream("""
                <JMdict><entry>
                  <ent_seq>12347</ent_seq><r_ele><reb>たべる</reb></r_ele>
                  <sense><pos>Ichidan verb</pos><gloss g_type="verb">to eat</gloss><gloss xml:lang="dut">eten</gloss><gloss xml:lang="spa"> </gloss></sense>
                  <sense><pos>Common noun</pos><gloss xml:lang="ger">essen</gloss></sense>
                  <sense><gloss>to consume</gloss></sense>
                </entry></JMdict>
                """)).Single();

            Assert.Equal(new[] { 1, 3 }, entry.Senses.Select(sense => sense.Index));
            Assert.Equal(new[] { "Common noun" }, entry.Senses[1].PartsOfSpeech);
            Assert.Equal(new[] { "eng", "dut" }, entry.Senses[0].Glosses.Select(gloss => gloss.Language));
            Assert.Equal("verb", entry.Senses[0].Glosses[0].Type);
            Assert.Equal("to consume", entry.Senses[1].Glosses.Single().Text);
        }

        [Fact]
        public void Parse_AllowsNestedEntityExpansionWithinExplicitLimit()
        {
            var entries = new JmDictParser().Parse(CreateXmlStream(CreateLargeEntityXml())).ToArray();

            var gloss = entries.Single().Senses.Single().Glosses.Single();
            Assert.Equal(10_000_010, gloss.Text.Length);
        }

        [Fact]
        public void Parse_InvalidXmlReportsNonZeroXmlPosition()
        {
            var xml = """
                <JMdict>
                  <entry>
                    <ent_seq>1</ent_seq>
                    <r_ele><reb>かな</reb></r_ele>
                    <sense><gloss>&missing;</gloss></sense>
                  </entry>
                </JMdict>
                """;

            var exception = Assert.Throws<LexiconBuildException>(() => new JmDictParser().Parse(CreateXmlStream(xml)).ToArray());

            Assert.Contains("解析 JMdict 失败", exception.Message);
            Assert.DoesNotContain("XML 位置 0:0", exception.Message);
        }

        private static Stream CreateXmlStream(string xml)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(xml));
        }

        private static string CreateLargeEntityXml()
        {
            var entityDefinitions = new StringBuilder();
            entityDefinitions.Append("<!ENTITY e0 \"0123456789\">");
            for (var index = 1; index <= 6; index++)
            {
                entityDefinitions.Append("<!ENTITY e").Append(index).Append(" \"");
                for (var repeat = 0; repeat < 10; repeat++)
                {
                    entityDefinitions.Append("&e").Append(index - 1).Append(";");
                }

                entityDefinitions.Append("\">");
            }

            return $"<!DOCTYPE JMdict [{entityDefinitions}]><JMdict><entry><ent_seq>1</ent_seq><r_ele><reb>かな</reb></r_ele><sense><gloss>&e6;&e0;</gloss></sense></entry></JMdict>";
        }
    }
}
