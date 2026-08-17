using Dictation.LexiconBuilder;
using Dictation.LexiconBuilder.WordNet;

namespace Dictation.Tests.LexiconBuilder
{
    public sealed class EnglishFormGeneratorTests
    {
        [Theory]
        [InlineData("go", "gone;went", "goes", "went", "gone", "going")]
        [InlineData("do", "did;done", "does", "did", "done", "doing")]
        [InlineData("play", "", "plays", "played", "played", "playing")]
        [InlineData("study", "", "studies", "studied", "studied", "studying")]
        [InlineData("stop", "stopped;stopping", "stops", "stopped", "stopped", "stopping")]
        [InlineData("make", "made", "makes", "made", "made", "making")]
        public void Generate_VerbForms(string lemma, string sourceFormsText, string thirdPerson, string past, string pastParticiple, string presentParticiple)
        {
            var sourceForms = SplitSourceForms(sourceFormsText);
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(lemma, "v", EnglishPartOfSpeech.Verb, sourceForms));

            Assert.Equal(lemma, Find(forms, EnglishFormKind.Base).Surface);
            Assert.Equal(thirdPerson, Find(forms, EnglishFormKind.ThirdPersonSingular).Surface);
            Assert.Equal(past, Find(forms, EnglishFormKind.Past).Surface);
            Assert.Equal(pastParticiple, Find(forms, EnglishFormKind.PastParticiple).Surface);
            Assert.Equal(presentParticiple, Find(forms, EnglishFormKind.PresentParticiple).Surface);
        }

        [Fact]
        public void Generate_BeKeepsEveryIrregularPresentAndPastSurface()
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                "be",
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { "am", "are", "been", "is", "was", "were" }));

            Assert.Equal(new[] { "be", "am", "are", "is", "was", "were", "been", "being" }, forms.Select(form => form.Surface));
            Assert.Equal(new[] { "am" }, FormsOfKind(forms, EnglishFormKind.PresentFirstPersonSingular));
            Assert.Equal(new[] { "are" }, FormsOfKind(forms, EnglishFormKind.PresentNonThirdPerson));
            Assert.Equal(new[] { "is" }, FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(new[] { "was", "were" }, FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(new[] { "been" }, FormsOfKind(forms, EnglishFormKind.PastParticiple));
        }

        [Fact]
        public void Generate_ProducesRegularNounAdjectiveAndAdverbForms()
        {
            var generator = new EnglishFormGenerator();

            var child = generator.Generate(CreateLexeme("child", "n", EnglishPartOfSpeech.Noun, new[] { "children" }));
            Assert.Equal(new[] { "child", "children" }, child.Select(form => form.Surface));
            Assert.Equal(EnglishFormSourceKind.Source, Find(child, EnglishFormKind.Plural).SourceKind);

            var happy = generator.Generate(CreateLexeme("happy", "a", EnglishPartOfSpeech.Adjective, new[] { "happier", "happiest" }));
            Assert.Equal(new[] { "happy", "happier", "happiest" }, happy.Select(form => form.Surface));
            Assert.All(happy.Where(form => form.Kind != EnglishFormKind.Base), form => Assert.Equal(EnglishFormSourceKind.Source, form.SourceKind));

            var quickly = generator.Generate(CreateLexeme("quickly", "r", EnglishPartOfSpeech.Adverb, Array.Empty<string>()));
            Assert.Equal(new[] { "quickly" }, quickly.Select(form => form.Surface));

            var beautiful = generator.Generate(CreateLexeme("beautiful", "a", EnglishPartOfSpeech.Adjective, Array.Empty<string>()));
            Assert.Equal(new[] { "beautiful" }, beautiful.Select(form => form.Surface));

            var visit = generator.Generate(CreateLexeme("visit", "v", EnglishPartOfSpeech.Verb, Array.Empty<string>()));
            Assert.Equal(new[] { "visit", "visits", "visited", "visited", "visiting" }, visit.Select(form => form.Surface));
        }

        [Theory]
        [InlineData("come", "came;coming", "comes", "came", "come", "coming")]
        [InlineData("run", "ran;running", "runs", "ran", "run", "running")]
        public void Generate_UsesCompleteIrregularParadigmWithoutRegularPastErrors(
            string lemma,
            string sourceFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                SplitSourceForms(sourceFormsText)));

            Assert.Equal(thirdPerson, Find(forms, EnglishFormKind.ThirdPersonSingular).Surface);
            Assert.Equal(past, Find(forms, EnglishFormKind.Past).Surface);
            Assert.Equal(pastParticiple, Find(forms, EnglishFormKind.PastParticiple).Surface);
            Assert.Equal(presentParticiple, Find(forms, EnglishFormKind.PresentParticiple).Surface);
            Assert.DoesNotContain(forms, form => form.Surface is "comed" or "runned");
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.PastParticiple).SourceKind);
        }

        [Fact]
        public void Generate_UsesSourceFormsBeforeRulesAndAllowsOneSurfaceForTwoKinds()
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                "play",
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { "played" }));

            Assert.Equal(new[] { "played" }, FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(new[] { "played" }, FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(forms, EnglishFormKind.Past).SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(forms, EnglishFormKind.PastParticiple).SourceKind);
            Assert.DoesNotContain(forms, form => form.Surface == "played" && form.SourceKind == EnglishFormSourceKind.Rule);
        }

        [Fact]
        public void Generate_VisitDoesNotDoubleItsMiddleConsonant()
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                "visit",
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(new[] { "visited" }, FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(new[] { "visiting" }, FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Fact]
        public void Generate_DoesNotInflectPhrase()
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                "take off",
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>(),
                isPhrase: true));

            var form = Assert.Single(forms);
            Assert.Equal(EnglishFormKind.Base, form.Kind);
            Assert.Equal("take off", form.Surface);
            Assert.Equal(EnglishFormSourceKind.Source, form.SourceKind);
        }

        [Fact]
        public void Generate_RejectsUnclassifiableSourceForm()
        {
            var exception = Assert.Throws<LexiconBuildException>(() => new EnglishFormGenerator().Generate(CreateLexeme(
                "play",
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { "mystery" })));

            Assert.Contains("play", exception.Message, StringComparison.Ordinal);
            Assert.Contains("v", exception.Message, StringComparison.Ordinal);
            Assert.Contains("mystery", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Generate_RejectsUnsupportedPartOfSpeech()
        {
            var exception = Assert.Throws<LexiconBuildException>(() => new EnglishFormGenerator().Generate(CreateLexeme(
                "unknown",
                "x",
                EnglishPartOfSpeech.Unknown,
                Array.Empty<string>())));

            Assert.Contains("unknown", exception.Message, StringComparison.Ordinal);
        }

        private static EnglishLexeme CreateLexeme(
            string lemma,
            string sourceEntryKey,
            EnglishPartOfSpeech partOfSpeech,
            IReadOnlyList<string> sourceForms,
            bool isPhrase = false)
        {
            return new EnglishLexeme(
                $"{lemma}%{sourceEntryKey}",
                lemma,
                sourceEntryKey,
                partOfSpeech,
                sourceForms,
                Array.Empty<EnglishSense>(),
                isPhrase);
        }

        private static EnglishForm Find(IReadOnlyList<EnglishForm> forms, EnglishFormKind kind)
        {
            return forms.Single(form => form.Kind == kind);
        }

        private static IReadOnlyList<string> FormsOfKind(IReadOnlyList<EnglishForm> forms, EnglishFormKind kind)
        {
            return forms
                .Where(form => form.Kind == kind)
                .Select(form => form.Surface)
                .ToArray();
        }

        private static string[] SplitSourceForms(string sourceFormsText)
        {
            return sourceFormsText.Length == 0
                ? Array.Empty<string>()
                : sourceFormsText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
