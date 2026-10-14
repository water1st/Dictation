using Dictation.LexiconBuilder.JmDict;

namespace Dictation.Tests.LexiconBuilder
{
    /// <summary>
    /// 验证日语词性、动词类别和短语分类。
    /// </summary>
    public sealed class JapaneseLexicalClassifierTests
    {
        [Theory]
        [InlineData("adverb (fukushi)")]
        [InlineData("adverb taking the 'to' particle")]
        public void ClassifyRealJmDictAdverbsWithoutVerbRole(string partOfSpeech)
        {
            var classification = JapaneseLexicalClassifier.Classify(new[] { partOfSpeech });

            Assert.Equal(JapaneseLexicalCategory.Adverb, classification.Category);
            Assert.Equal(new[] { JapaneseLexicalRole.Adverb }, classification.Roles);
            Assert.DoesNotContain(JapaneseLexicalRole.Verb, classification.Roles);
        }

        [Fact]
        public void ClassifyRealJmDictQuasiAdjectivesAsNaAdjective()
        {
            var classification = JapaneseLexicalClassifier.Classify(new[]
            {
                "adjectival nouns or quasi-adjectives (keiyodoshi)"
            });

            Assert.Equal(JapaneseLexicalCategory.NaAdjective, classification.Category);
            Assert.Equal(new[] { JapaneseLexicalRole.Adjective }, classification.Roles);
            Assert.NotEqual(JapaneseLexicalCategory.IAdjective, classification.Category);
        }

        [Theory]
        [InlineData("adjective (keiyoushi)")]
        [InlineData("adjective (keiyoushi) - yoi/ii class")]
        public void ClassifyRealJmDictIAdjectivesAsIAdjective(string partOfSpeech)
        {
            var classification = JapaneseLexicalClassifier.Classify(new[] { partOfSpeech });

            Assert.Equal(JapaneseLexicalCategory.IAdjective, classification.Category);
            Assert.Equal(new[] { JapaneseLexicalRole.Adjective }, classification.Roles);
        }

        [Fact]
        public void ClassifyRealJmDictAdverbialNounWithNounAndAdverbRoles()
        {
            var classification = JapaneseLexicalClassifier.Classify(new[]
            {
                "adverbial noun (fukushitekimeishi)"
            });

            Assert.Equal(JapaneseLexicalCategory.Adverb, classification.Category);
            Assert.Equal(new[] { JapaneseLexicalRole.Noun, JapaneseLexicalRole.Adverb }, classification.Roles);
            Assert.DoesNotContain(JapaneseLexicalRole.Verb, classification.Roles);
        }

        [Fact]
        public void ClassifyNounAndRealJmDictQuasiAdjectiveWithNounAndAdjectiveRoles()
        {
            var classification = JapaneseLexicalClassifier.Classify(new[]
            {
                "noun (common) (futsuumeishi)",
                "adjectival nouns or quasi-adjectives (keiyodoshi)"
            });

            Assert.Equal(JapaneseLexicalCategory.NaAdjective, classification.Category);
            Assert.Equal(new[] { JapaneseLexicalRole.Noun, JapaneseLexicalRole.Adjective }, classification.Roles);
        }

        [Theory]
        [InlineData("Ichidan verb", "Verb", "Ichidan")]
        [InlineData("Godan verb with 'ku' ending", "Verb", "GodanK")]
        [InlineData("transitive verb", "Verb", "None")]
        [InlineData("noun or verb acting prenominally", "NounVerb", "None")]
        public void ClassifyRealJmDictVerbDescriptionsByIndependentVerbWord(
            string partOfSpeech,
            string expectedCategory,
            string expectedVerbClass)
        {
            var classification = JapaneseLexicalClassifier.Classify(new[] { partOfSpeech });

            Assert.Equal(Enum.Parse<JapaneseLexicalCategory>(expectedCategory), classification.Category);
            Assert.Contains(JapaneseLexicalRole.Verb, classification.Roles);
            Assert.Equal(Enum.Parse<JapaneseVerbClass>(expectedVerbClass), classification.VerbClass);
        }
    }
}
