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

        [Theory]
        [InlineData("burn", "burnt", "burned", "burned;burnt", "burned;burnt")]
        [InlineData("beseech", "besought", "beseeched", "beseeched;besought", "beseeched;besought")]
        [InlineData("bestrew", "bestrewn", "bestrewed", "bestrewed", "bestrewed;bestrewn")]
        public void Generate_PreservesAllCatalogAlternativesWhenSourceContainsOnlyException(
            string lemma,
            string sourceForm,
            string expectedComplement,
            string expectedPast,
            string expectedPastParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));

            Assert.Equal(SplitSourceForms(expectedPast), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SplitSourceForms(expectedPastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            if (SplitSourceForms(expectedPast).Contains(sourceForm, StringComparer.Ordinal))
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, EnglishFormKind.Past, sourceForm).SourceKind);
            }

            if (SplitSourceForms(expectedPastParticiple).Contains(sourceForm, StringComparer.Ordinal))
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, EnglishFormKind.PastParticiple, sourceForm).SourceKind);
            }

            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.Past, expectedComplement).SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.PastParticiple, expectedComplement).SourceKind);
            Assert.Equal(1, forms.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == expectedComplement));
            Assert.Equal(1, forms.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == sourceForm));
            Assert.Equal(1, forms.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == expectedComplement));
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

        [Theory]
        [InlineData("cut", "cuts", "cut", "cut", "cutting")]
        [InlineData("read", "reads", "read", "read", "reading")]
        [InlineData("hit", "hits", "hit", "hit", "hitting")]
        [InlineData("set", "sets", "set", "set", "setting")]
        [InlineData("put", "puts", "put", "put", "putting")]
        [InlineData("let", "lets", "let", "let", "letting")]
        [InlineData("shut", "shuts", "shut", "shut", "shutting")]
        [InlineData("beat", "beats", "beat", "beaten", "beating")]
        [InlineData("become", "becomes", "became", "become", "becoming")]
        [InlineData("overcome", "overcomes", "overcame", "overcome", "overcoming")]
        [InlineData("say", "says", "said", "said", "saying")]
        public void Generate_UsesZeroChangeVerbParadigmWithoutRegularPastErrors(
            string lemma,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(thirdPerson, Find(forms, EnglishFormKind.ThirdPersonSingular).Surface);
            Assert.Equal(past, Find(forms, EnglishFormKind.Past).Surface);
            Assert.Equal(pastParticiple, Find(forms, EnglishFormKind.PastParticiple).Surface);
            Assert.Equal(presentParticiple, Find(forms, EnglishFormKind.PresentParticiple).Surface);
            Assert.DoesNotContain(forms, form => form.Surface is "cutted" or "readed" or "hitted" or "setted" or "putted" or "letted" or "shutted" or "beated" or "becomed" or "overcomed" or "sayed");
        }

        [Theory]
        [InlineData("beat", "beaten", "beats", "beat", "beaten", "beating")]
        [InlineData("become", "became", "becomes", "became", "become", "becoming")]
        [InlineData("overcome", "overcame", "overcomes", "overcame", "overcome", "overcoming")]
        [InlineData("say", "said", "says", "said", "said", "saying")]
        public void Generate_UsesRequiredIrregularVerbParadigmsWithExplicitSourceForms(
            string lemma,
            string sourceForm,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));

            Assert.Equal(thirdPerson, Find(forms, EnglishFormKind.ThirdPersonSingular).Surface);
            Assert.Equal(past, Find(forms, EnglishFormKind.Past).Surface);
            Assert.Equal(pastParticiple, Find(forms, EnglishFormKind.PastParticiple).Surface);
            Assert.Equal(presentParticiple, Find(forms, EnglishFormKind.PresentParticiple).Surface);
            Assert.DoesNotContain(forms, form => form.Surface is "becomed" or "overcomed" or "sayed");
        }

        [Fact]
        public void Generate_HandlesGetAndHaveAlternateSourceFormsWithoutDroppingThem()
        {
            var generator = new EnglishFormGenerator();
            var get = generator.Generate(CreateLexeme(
                "get",
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { "got", "gotten" }));
            Assert.Equal(new[] { "got", "gotten" }, FormsOfKind(get, EnglishFormKind.PastParticiple));
            Assert.Equal(new[] { "got" }, FormsOfKind(get, EnglishFormKind.Past));
            Assert.Equal("gets", Find(get, EnglishFormKind.ThirdPersonSingular).Surface);
            Assert.Equal("getting", Find(get, EnglishFormKind.PresentParticiple).Surface);

            var have = generator.Generate(CreateLexeme(
                "have",
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { "had", "has" }));
            Assert.Equal(new[] { "has" }, FormsOfKind(have, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(new[] { "had" }, FormsOfKind(have, EnglishFormKind.Past));
            Assert.Equal(new[] { "had" }, FormsOfKind(have, EnglishFormKind.PastParticiple));
            Assert.Equal("having", Find(have, EnglishFormKind.PresentParticiple).Surface);
        }

        [Fact]
        public void Generate_UsesGetAndHaveParadigmsWithoutSourceForms()
        {
            var generator = new EnglishFormGenerator();
            var get = generator.Generate(CreateLexeme(
                "get",
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));
            Assert.Equal(new[] { "gets" }, FormsOfKind(get, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(new[] { "got" }, FormsOfKind(get, EnglishFormKind.Past));
            Assert.Equal(new[] { "got", "gotten" }, FormsOfKind(get, EnglishFormKind.PastParticiple));
            Assert.Equal(new[] { "getting" }, FormsOfKind(get, EnglishFormKind.PresentParticiple));

            var have = generator.Generate(CreateLexeme(
                "have",
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));
            Assert.Equal(new[] { "has" }, FormsOfKind(have, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(new[] { "had" }, FormsOfKind(have, EnglishFormKind.Past));
            Assert.Equal(new[] { "had" }, FormsOfKind(have, EnglishFormKind.PastParticiple));
            Assert.Equal(new[] { "having" }, FormsOfKind(have, EnglishFormKind.PresentParticiple));
            Assert.DoesNotContain(get, form => form.Surface == "getted");
            Assert.DoesNotContain(have, form => form.Surface == "haved");
        }

        [Theory]
        [InlineData("catch", "catch", "catches", "caught", "caught", "catching")]
        [InlineData("chide", "chide", "chides", "chided;chid", "chided;chid;chidden", "chiding")]
        [InlineData("choose", "choose", "chooses", "chose", "chosen", "choosing")]
        [InlineData("cleave", "cleave", "cleaves", "cleaved;cleft;clove", "cleaved;cleft;cloven", "cleaving")]
        [InlineData("cling", "cling", "clings", "clung", "clung", "clinging")]
        [InlineData("clothe", "clothe", "clothes", "clad;clothed", "clad;clothed", "clothing")]
        [InlineData("coordinate", "coordinate;co-ordinate", "coordinates;co-ordinates", "coordinated;co-ordinated", "coordinated;co-ordinated", "coordinating;co-ordinating")]
        [InlineData("countersink", "countersink", "countersinks", "countersank;countersunk", "countersunk", "countersinking")]
        [InlineData("creep", "creep", "creeps", "crept", "crept", "creeping")]
        [InlineData("curse", "curse", "curses", "cursed;curst", "cursed;curst", "cursing")]
        [InlineData("deal", "deal", "deals", "dealt", "dealt", "dealing")]
        [InlineData("dig", "dig", "digs", "dug", "dug", "digging")]
        [InlineData("dive", "dive", "dives", "dived;dove", "dived", "diving")]
        [InlineData("draw", "draw", "draws", "drew", "drawn", "drawing")]
        [InlineData("dream", "dream", "dreams", "dreamed;dreamt", "dreamed;dreamt", "dreaming")]
        [InlineData("drink", "drink", "drinks", "drank", "drunk", "drinking")]
        [InlineData("drive", "drive", "drives", "drove", "driven", "driving")]
        [InlineData("dwell", "dwell", "dwells", "dwelled;dwelt", "dwelled;dwelt", "dwelling")]
        public void Generate_UsesCompleteCdParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("catch", "caught", "Past,PastParticiple")]
        [InlineData("chide", "chid", "Past,PastParticiple")]
        [InlineData("chide", "chidden", "PastParticiple")]
        [InlineData("choose", "chose", "Past")]
        [InlineData("choose", "chosen", "PastParticiple")]
        [InlineData("cleave", "cleft", "Past,PastParticiple")]
        [InlineData("cleave", "clove", "Past")]
        [InlineData("cleave", "cloven", "PastParticiple")]
        [InlineData("cling", "clung", "Past,PastParticiple")]
        [InlineData("clothe", "clad", "Past,PastParticiple")]
        [InlineData("coordinate", "co-ordinate", "Base")]
        [InlineData("countersink", "countersank", "Past")]
        [InlineData("countersink", "countersunk", "Past,PastParticiple")]
        [InlineData("creep", "crept", "Past,PastParticiple")]
        [InlineData("curse", "curst", "Past,PastParticiple")]
        [InlineData("deal", "dealt", "Past,PastParticiple")]
        [InlineData("dig", "dug", "Past,PastParticiple")]
        [InlineData("dive", "dove", "Past")]
        [InlineData("draw", "drawn", "PastParticiple")]
        [InlineData("draw", "drew", "Past")]
        [InlineData("dream", "dreamt", "Past,PastParticiple")]
        [InlineData("drink", "drank", "Past")]
        [InlineData("drink", "drunk", "PastParticiple")]
        [InlineData("drive", "driven", "PastParticiple")]
        [InlineData("drive", "drove", "Past")]
        [InlineData("dwell", "dwelt", "Past,PastParticiple")]
        public void Generate_ClassifiesCdSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PrefersCdSourceFormsOverRulesWithoutDroppingAlternatives()
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                "chide",
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { "chid" }));

            Assert.Equal(new[] { "chid", "chided" }, FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(new[] { "chid", "chidden", "chided" }, FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(forms, EnglishFormKind.Past, "chid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(forms, EnglishFormKind.PastParticiple, "chid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.Past, "chided").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.PastParticiple, "chided").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.PastParticiple, "chidden").SourceKind);
            Assert.DoesNotContain(forms, form => form.Surface == "chid" && form.SourceKind == EnglishFormSourceKind.Rule);
            Assert.Equal(1, forms.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "chid"));
            Assert.Equal(1, forms.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "chid"));
        }

        [Theory]
        [InlineData("catch", "catched")]
        [InlineData("chide", "chideded")]
        [InlineData("choose", "chosed")]
        [InlineData("cleave", "cleaveded")]
        [InlineData("cling", "clinged")]
        [InlineData("clothe", "clotheded")]
        [InlineData("countersink", "countersinked")]
        [InlineData("creep", "creeped")]
        [InlineData("deal", "dealed")]
        [InlineData("dig", "digged")]
        [InlineData("draw", "drawed")]
        [InlineData("drink", "drinked")]
        [InlineData("drive", "drived")]
        public void Generate_DoesNotEmitCdPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("eat", "eat", "eats", "ate", "eaten", "eating")]
        [InlineData("fall", "fall", "falls", "fell", "fallen", "falling")]
        [InlineData("feel", "feel", "feels", "felt", "felt", "feeling")]
        [InlineData("fight", "fight", "fights", "fought", "fought", "fighting")]
        [InlineData("find", "find", "finds", "found", "found", "finding")]
        [InlineData("fling", "fling", "flings", "flung", "flung", "flinging")]
        [InlineData("floodlight", "floodlight", "floodlights", "floodlighted;floodlit", "floodlighted;floodlit", "floodlighting")]
        [InlineData("fly", "fly", "flies", "flied;flew", "flied;flown", "flying")]
        [InlineData("forbear", "forbear", "forbears", "forbore", "forborne", "forbearing")]
        [InlineData("forbid", "forbid", "forbids", "forbad;forbade", "forbidden", "forbidding")]
        [InlineData("forego", "forego", "foregoes", "forewent", "foregone", "foregoing")]
        [InlineData("foreknow", "foreknow", "foreknows", "foreknew", "foreknown", "foreknowing")]
        [InlineData("foresee", "foresee", "foresees", "foresaw", "foreseen", "foreseeing")]
        [InlineData("foreshow", "foreshow", "foreshows", "foreshowed", "foreshowed;foreshown", "foreshowing")]
        [InlineData("foretell", "foretell", "foretells", "foretold", "foretold", "foretelling")]
        [InlineData("forget", "forget", "forgets", "forgot", "forgot;forgotten", "forgetting")]
        [InlineData("forgive", "forgive", "forgives", "forgave", "forgiven", "forgiving")]
        [InlineData("forgo", "forgo", "forgoes", "forwent", "forgone", "forgoing")]
        [InlineData("forsake", "forsake", "forsakes", "forsook", "forsaken", "forsaking")]
        [InlineData("forswear", "forswear", "forswears", "forswore", "forsworn", "forswearing")]
        [InlineData("freeze", "freeze", "freezes", "froze", "frozen", "freezing")]
        public void Generate_UsesCompleteEfParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("eat", "ate", "Past")]
        [InlineData("eat", "eaten", "PastParticiple")]
        [InlineData("fall", "fallen", "PastParticiple")]
        [InlineData("fall", "fell", "Past")]
        [InlineData("feel", "felt", "Past,PastParticiple")]
        [InlineData("fight", "fought", "Past,PastParticiple")]
        [InlineData("find", "found", "Past,PastParticiple")]
        [InlineData("fling", "flung", "Past,PastParticiple")]
        [InlineData("floodlight", "floodlit", "Past,PastParticiple")]
        [InlineData("fly", "flew", "Past")]
        [InlineData("fly", "flown", "PastParticiple")]
        [InlineData("forbear", "forbore", "Past")]
        [InlineData("forbear", "forborne", "PastParticiple")]
        [InlineData("forbid", "forbad", "Past")]
        [InlineData("forbid", "forbade", "Past")]
        [InlineData("forbid", "forbidden", "PastParticiple")]
        [InlineData("forego", "foregone", "PastParticiple")]
        [InlineData("forego", "forewent", "Past")]
        [InlineData("foreknow", "foreknew", "Past")]
        [InlineData("foreknow", "foreknown", "PastParticiple")]
        [InlineData("foresee", "foresaw", "Past")]
        [InlineData("foresee", "foreseen", "PastParticiple")]
        [InlineData("foreshow", "foreshown", "PastParticiple")]
        [InlineData("foretell", "foretold", "Past,PastParticiple")]
        [InlineData("forget", "forgot", "Past,PastParticiple")]
        [InlineData("forget", "forgotten", "PastParticiple")]
        [InlineData("forgive", "forgave", "Past")]
        [InlineData("forgive", "forgiven", "PastParticiple")]
        [InlineData("forgo", "forgone", "PastParticiple")]
        [InlineData("forgo", "forwent", "Past")]
        [InlineData("forsake", "forsaken", "PastParticiple")]
        [InlineData("forsake", "forsook", "Past")]
        [InlineData("forswear", "forswore", "Past")]
        [InlineData("forswear", "forsworn", "PastParticiple")]
        [InlineData("freeze", "froze", "Past")]
        [InlineData("freeze", "frozen", "PastParticiple")]
        public void Generate_ClassifiesEfSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PrefersEfSourceFormsOverRulesWithoutDroppingAlternatives()
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                "fly",
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { "flew" }));

            Assert.Equal(new[] { "flew", "flied" }, FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(new[] { "flied", "flown" }, FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(forms, EnglishFormKind.Past, "flew").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.Past, "flied").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.PastParticiple, "flied").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(forms, EnglishFormKind.PastParticiple, "flown").SourceKind);
            Assert.DoesNotContain(forms, form => form.Surface == "flew" && form.SourceKind == EnglishFormSourceKind.Rule);
            Assert.Equal(1, forms.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "flew"));
        }

        [Theory]
        [InlineData("eat", "eated")]
        [InlineData("fall", "falled")]
        [InlineData("feel", "feeled")]
        [InlineData("fight", "fighted")]
        [InlineData("find", "finded")]
        [InlineData("fling", "flinged")]
        [InlineData("fly", "flyed")]
        [InlineData("forbear", "forbeared")]
        [InlineData("forbid", "forbided")]
        [InlineData("forego", "foregoed")]
        [InlineData("foreknow", "foreknowed")]
        [InlineData("foresee", "foreseed")]
        [InlineData("foretell", "foretelled")]
        [InlineData("forget", "forgetted")]
        [InlineData("forgive", "forgived")]
        [InlineData("forgo", "forgoed")]
        [InlineData("forsake", "forsaked")]
        [InlineData("forswear", "forsweared")]
        [InlineData("freeze", "freezed")]
        public void Generate_DoesNotEmitEfPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("gainsay", "gainsay", "gainsays", "gainsaid", "gainsaid", "gainsaying")]
        [InlineData("geld", "geld", "gelds", "gelded;gelt", "gelded;gelt", "gelding")]
        [InlineData("ghostwrite", "ghostwrite", "ghostwrites", "ghostwrote", "ghostwritten", "ghostwriting")]
        [InlineData("gild", "gild", "gilds", "gilded;gilt", "gilded;gilt", "gilding")]
        [InlineData("gin", "gin", "gins", "gan;ginned", "ginned", "ginning")]
        [InlineData("gird", "gird", "girds", "girded;girt", "girded;girt", "girding")]
        [InlineData("give", "give", "gives", "gave", "given", "giving")]
        [InlineData("gnaw", "gnaw", "gnaws", "gnawed", "gnawed;gnawn", "gnawing")]
        [InlineData("grave", "grave", "graves", "graved", "graved;graven", "graving")]
        [InlineData("grind", "grind", "grinds", "ground", "ground", "grinding")]
        [InlineData("grip", "grip", "grips", "gripped;gript", "gripped;gript", "gripping")]
        [InlineData("grow", "grow", "grows", "grew", "grown", "growing")]
        [InlineData("halfway", "halfway;halfways", "", "", "", "")]
        [InlineData("hamstring", "hamstring", "hamstrings", "hamstrung", "hamstrung", "hamstringing")]
        [InlineData("hang", "hang", "hangs", "hanged;hung", "hanged;hung", "hanging")]
        [InlineData("hear", "hear", "hears", "heard", "heard", "hearing")]
        [InlineData("heave", "heave", "heaves", "heaved;hove", "heaved;hove", "heaving")]
        [InlineData("hew", "hew", "hews", "hewed", "hewed;hewn", "hewing")]
        [InlineData("hide", "hide", "hides", "hid", "hidden;hid", "hiding")]
        [InlineData("hold", "hold", "holds", "held", "held", "holding")]
        public void Generate_UsesCompleteGhParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var partOfSpeech = lemma == "halfway"
                ? EnglishPartOfSpeech.Adjective
                : EnglishPartOfSpeech.Verb;
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                lemma == "halfway" ? "a" : "v",
                partOfSpeech,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            if (partOfSpeech == EnglishPartOfSpeech.Verb)
            {
                Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
                Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
                Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
                Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
            }
            else
            {
                Assert.Empty(FormsOfKind(forms, EnglishFormKind.Comparative));
                Assert.Empty(FormsOfKind(forms, EnglishFormKind.Superlative));
            }
        }

        [Theory]
        [InlineData("gainsay", "gainsaid", "Past,PastParticiple")]
        [InlineData("geld", "gelt", "Past,PastParticiple")]
        [InlineData("ghostwrite", "ghostwrote", "Past")]
        [InlineData("ghostwrite", "ghostwritten", "PastParticiple")]
        [InlineData("gild", "gilt", "Past,PastParticiple")]
        [InlineData("gin", "gan", "Past")]
        [InlineData("gird", "girt", "Past,PastParticiple")]
        [InlineData("give", "gave", "Past")]
        [InlineData("give", "given", "PastParticiple")]
        [InlineData("gnaw", "gnawn", "PastParticiple")]
        [InlineData("grave", "graven", "PastParticiple")]
        [InlineData("grind", "ground", "Past,PastParticiple")]
        [InlineData("grip", "gript", "Past,PastParticiple")]
        [InlineData("grow", "grew", "Past")]
        [InlineData("grow", "grown", "PastParticiple")]
        [InlineData("halfway", "halfways", "Base")]
        [InlineData("hamstring", "hamstrung", "Past,PastParticiple")]
        [InlineData("hang", "hung", "Past,PastParticiple")]
        [InlineData("hear", "heard", "Past,PastParticiple")]
        [InlineData("heave", "hove", "Past,PastParticiple")]
        [InlineData("hew", "hewn", "PastParticiple")]
        [InlineData("hide", "hid", "Past,PastParticiple")]
        [InlineData("hide", "hidden", "PastParticiple")]
        [InlineData("hold", "held", "Past,PastParticiple")]
        public void Generate_ClassifiesGhSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var partOfSpeech = lemma == "halfway"
                ? EnglishPartOfSpeech.Adjective
                : EnglishPartOfSpeech.Verb;
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                lemma == "halfway" ? "a" : "v",
                partOfSpeech,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesGhCatalogAlternativesAndSpecialCases()
        {
            var generator = new EnglishFormGenerator();

            var geld = generator.Generate(CreateLexeme("geld", "v", EnglishPartOfSpeech.Verb, new[] { "gelt" }));
            Assert.Equal(new[] { "gelded", "gelt" }, FormsOfKind(geld, EnglishFormKind.Past));
            Assert.Equal(new[] { "gelded", "gelt" }, FormsOfKind(geld, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(geld, EnglishFormKind.Past, "gelt").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(geld, EnglishFormKind.Past, "gelded").SourceKind);
            Assert.Equal(1, geld.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "gelt"));
            Assert.Equal(1, geld.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "gelt"));

            var gin = generator.Generate(CreateLexeme("gin", "v", EnglishPartOfSpeech.Verb, new[] { "gan" }));
            Assert.Equal(EnglishFormSourceKind.Source, Find(gin, EnglishFormKind.Past, "gan").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(gin, EnglishFormKind.Past, "ginned").SourceKind);
            Assert.DoesNotContain(gin, form => form.Surface == "gun");

            var halfway = generator.Generate(CreateLexeme("halfway", "a", EnglishPartOfSpeech.Adjective, new[] { "halfways" }));
            Assert.Equal(new[] { "halfway", "halfways" }, FormsOfKind(halfway, EnglishFormKind.Base));
            Assert.Equal(EnglishFormSourceKind.Source, Find(halfway, EnglishFormKind.Base, "halfways").SourceKind);
            Assert.Empty(FormsOfKind(halfway, EnglishFormKind.Comparative));
            Assert.Empty(FormsOfKind(halfway, EnglishFormKind.Superlative));
        }

        [Theory]
        [InlineData("gainsay", "gainsayed")]
        [InlineData("ghostwrite", "ghostwrited")]
        [InlineData("gin", "gined")]
        [InlineData("give", "gived")]
        [InlineData("grind", "grinded")]
        [InlineData("grow", "growed")]
        [InlineData("hamstring", "hamstringed")]
        [InlineData("hear", "heared")]
        [InlineData("hide", "hided")]
        [InlineData("hold", "holded")]
        public void Generate_DoesNotEmitGhPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("indwell", "indwell", "indwells", "indwelled;indwelt", "indwelled;indwelt", "indwelling")]
        [InlineData("inlay", "inlay", "inlays", "inlaid", "inlaid", "inlaying")]
        [InlineData("interweave", "interweave", "interweaves", "interweaved;interwove", "interweaved;interwoven", "interweaving")]
        [InlineData("inweave", "inweave", "inweaves", "inweaved;inwove", "inweaved;inwoven", "inweaving")]
        [InlineData("keep", "keep", "keeps", "kept", "kept", "keeping")]
        [InlineData("kneel", "kneel", "kneels", "kneeled;knelt", "kneeled;knelt", "kneeling")]
        [InlineData("know", "know", "knows", "knew", "known", "knowing")]
        [InlineData("lade", "lade", "lades", "laded", "laded;laden", "lading")]
        [InlineData("lay", "lay", "lays", "laid", "laid", "laying")]
        [InlineData("lean", "lean", "leans", "leaned;leant", "leaned;leant", "leaning")]
        [InlineData("leap", "leap", "leaps", "leaped;leapt", "leaped;leapt", "leaping")]
        [InlineData("learn", "learn", "learns", "learned;learnt", "learned;learnt", "learning")]
        [InlineData("leave", "leave", "leaves", "left", "left", "leaving")]
        [InlineData("lend", "lend", "lends", "lent", "lent", "lending")]
        [InlineData("lie", "lie", "lies", "lay;lied", "lain;lied", "lying")]
        [InlineData("light", "light", "lights", "lighted;lit", "lighted;lit", "lighting")]
        [InlineData("lose", "lose", "loses", "lost", "lost", "losing")]
        public void Generate_UsesCompleteIlParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("indwell", "indwelt", "Past,PastParticiple")]
        [InlineData("inlay", "inlaid", "Past,PastParticiple")]
        [InlineData("interweave", "interwove", "Past")]
        [InlineData("interweave", "interwoven", "PastParticiple")]
        [InlineData("inweave", "inwove", "Past")]
        [InlineData("inweave", "inwoven", "PastParticiple")]
        [InlineData("keep", "kept", "Past,PastParticiple")]
        [InlineData("kneel", "knelt", "Past,PastParticiple")]
        [InlineData("know", "knew", "Past")]
        [InlineData("know", "known", "PastParticiple")]
        [InlineData("lade", "laden", "PastParticiple")]
        [InlineData("lay", "laid", "Past,PastParticiple")]
        [InlineData("lean", "leant", "Past,PastParticiple")]
        [InlineData("leap", "leapt", "Past,PastParticiple")]
        [InlineData("learn", "learnt", "Past,PastParticiple")]
        [InlineData("leave", "left", "Past,PastParticiple")]
        [InlineData("lend", "lent", "Past,PastParticiple")]
        [InlineData("lie", "lain", "PastParticiple")]
        [InlineData("lie", "lay", "Past")]
        [InlineData("light", "lit", "Past,PastParticiple")]
        [InlineData("lose", "lost", "Past,PastParticiple")]
        public void Generate_ClassifiesIlSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesIlCatalogAlternativesAndLieSenses()
        {
            var generator = new EnglishFormGenerator();

            var indwell = generator.Generate(CreateLexeme("indwell", "v", EnglishPartOfSpeech.Verb, new[] { "indwelt" }));
            Assert.Equal(new[] { "indwelled", "indwelt" }, FormsOfKind(indwell, EnglishFormKind.Past));
            Assert.Equal(new[] { "indwelled", "indwelt" }, FormsOfKind(indwell, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(indwell, EnglishFormKind.Past, "indwelt").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(indwell, EnglishFormKind.Past, "indwelled").SourceKind);

            var interweave = generator.Generate(CreateLexeme("interweave", "v", EnglishPartOfSpeech.Verb, new[] { "interwove" }));
            Assert.Equal(new[] { "interweaved", "interwove" }, FormsOfKind(interweave, EnglishFormKind.Past));
            Assert.Equal(new[] { "interweaved", "interwoven" }, FormsOfKind(interweave, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(interweave, EnglishFormKind.Past, "interwove").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(interweave, EnglishFormKind.Past, "interweaved").SourceKind);

            var inweave = generator.Generate(CreateLexeme("inweave", "v", EnglishPartOfSpeech.Verb, new[] { "inwove" }));
            Assert.Equal(new[] { "inweaved", "inwove" }, FormsOfKind(inweave, EnglishFormKind.Past));
            Assert.Equal(new[] { "inweaved", "inwoven" }, FormsOfKind(inweave, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(inweave, EnglishFormKind.Past, "inwove").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(inweave, EnglishFormKind.Past, "inweaved").SourceKind);

            var lade = generator.Generate(CreateLexeme("lade", "v", EnglishPartOfSpeech.Verb, new[] { "laden" }));
            Assert.Equal(new[] { "laded" }, FormsOfKind(lade, EnglishFormKind.Past));
            Assert.Equal(new[] { "laded", "laden" }, FormsOfKind(lade, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(lade, EnglishFormKind.PastParticiple, "laden").SourceKind);
            Assert.DoesNotContain(lade, form => form.Kind == EnglishFormKind.Past && form.Surface == "laden");

            var lie = generator.Generate(CreateLexeme("lie", "v", EnglishPartOfSpeech.Verb, new[] { "lain", "lay", "lying" }));
            Assert.Equal(new[] { "lay", "lied" }, FormsOfKind(lie, EnglishFormKind.Past));
            Assert.Equal(new[] { "lain", "lied" }, FormsOfKind(lie, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(lie, EnglishFormKind.Past, "lay").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(lie, EnglishFormKind.Past, "lied").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(lie, EnglishFormKind.PastParticiple, "lain").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(lie, EnglishFormKind.PastParticiple, "lied").SourceKind);
            Assert.Equal(1, lie.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "lied"));
            Assert.Equal(1, lie.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "lied"));
        }

        [Theory]
        [InlineData("keep", "keeped")]
        [InlineData("know", "knowed")]
        [InlineData("lay", "layed")]
        [InlineData("leave", "leaved")]
        [InlineData("lend", "lended")]
        [InlineData("lose", "losed")]
        public void Generate_DoesNotEmitIlPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("mean", "mean", "means", "meant", "meant", "meaning")]
        [InlineData("meet", "meet", "meets", "met", "met", "meeting")]
        [InlineData("melt", "melt", "melts", "melted", "melted;molten", "melting")]
        [InlineData("misdeal", "misdeal", "misdeals", "misdealt", "misdealt", "misdealing")]
        [InlineData("misgive", "misgive", "misgives", "misgave", "misgiven", "misgiving")]
        [InlineData("mislay", "mislay", "mislays", "mislaid", "mislaid", "mislaying")]
        [InlineData("misspell", "misspell", "misspells", "misspelled;misspelt", "misspelled;misspelt", "misspelling")]
        [InlineData("misspend", "misspend", "misspends", "misspent", "misspent", "misspending")]
        [InlineData("mistake", "mistake", "mistakes", "mistook", "mistaken", "mistaking")]
        [InlineData("misunderstand", "misunderstand", "misunderstands", "misunderstood", "misunderstood", "misunderstanding")]
        [InlineData("mow", "mow", "mows", "mowed", "mowed;mown", "mowing")]
        public void Generate_UsesCompleteMParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("mean", "meant", "Past,PastParticiple")]
        [InlineData("meet", "met", "Past,PastParticiple")]
        [InlineData("melt", "molten", "PastParticiple")]
        [InlineData("misdeal", "misdealt", "Past,PastParticiple")]
        [InlineData("misgive", "misgave", "Past")]
        [InlineData("misgive", "misgiven", "PastParticiple")]
        [InlineData("mislay", "mislaid", "Past,PastParticiple")]
        [InlineData("misspell", "misspelt", "Past,PastParticiple")]
        [InlineData("misspend", "misspent", "Past,PastParticiple")]
        [InlineData("mistake", "mistaken", "PastParticiple")]
        [InlineData("mistake", "mistook", "Past")]
        [InlineData("misunderstand", "misunderstood", "Past,PastParticiple")]
        [InlineData("mow", "mown", "PastParticiple")]
        public void Generate_ClassifiesMSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesMCatalogAlternativesAndParticipleOnlyForms()
        {
            var generator = new EnglishFormGenerator();

            var melt = generator.Generate(CreateLexeme("melt", "v", EnglishPartOfSpeech.Verb, new[] { "molten" }));
            Assert.Equal(new[] { "melted" }, FormsOfKind(melt, EnglishFormKind.Past));
            Assert.Equal(new[] { "melted", "molten" }, FormsOfKind(melt, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(melt, EnglishFormKind.PastParticiple, "molten").SourceKind);
            Assert.DoesNotContain(melt, form => form.Kind == EnglishFormKind.Past && form.Surface == "molten");

            var mow = generator.Generate(CreateLexeme("mow", "v", EnglishPartOfSpeech.Verb, new[] { "mown" }));
            Assert.Equal(new[] { "mowed" }, FormsOfKind(mow, EnglishFormKind.Past));
            Assert.Equal(new[] { "mowed", "mown" }, FormsOfKind(mow, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(mow, EnglishFormKind.PastParticiple, "mown").SourceKind);
            Assert.DoesNotContain(mow, form => form.Kind == EnglishFormKind.Past && form.Surface == "mown");

            var misspell = generator.Generate(CreateLexeme("misspell", "v", EnglishPartOfSpeech.Verb, new[] { "misspelt" }));
            Assert.Equal(new[] { "misspelled", "misspelt" }, FormsOfKind(misspell, EnglishFormKind.Past));
            Assert.Equal(new[] { "misspelled", "misspelt" }, FormsOfKind(misspell, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(misspell, EnglishFormKind.Past, "misspelt").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(misspell, EnglishFormKind.Past, "misspelled").SourceKind);
            Assert.Equal(1, misspell.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "misspelt"));
            Assert.Equal(1, misspell.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "misspelt"));
        }

        [Theory]
        [InlineData("mean", "meaned")]
        [InlineData("meet", "meeted")]
        [InlineData("misdeal", "misdealed")]
        [InlineData("misgive", "misgived")]
        [InlineData("mislay", "mislayed")]
        [InlineData("misspend", "misspended")]
        [InlineData("mistake", "mistaked")]
        [InlineData("misunderstand", "misunderstanded")]
        public void Generate_DoesNotEmitMPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("outbid", "outbid", "outbids", "outbid", "outbid;outbidden", "outbidding")]
        [InlineData("outdo", "outdo", "outdoes", "outdid", "outdone", "outdoing")]
        [InlineData("outdraw", "outdraw", "outdraws", "outdrew", "outdrawn", "outdrawing")]
        [InlineData("outfight", "outfight", "outfights", "outfought", "outfought", "outfighting")]
        [InlineData("outgo", "outgo", "outgoes", "outwent", "outgone", "outgoing")]
        [InlineData("outgrow", "outgrow", "outgrows", "outgrew", "outgrown", "outgrowing")]
        [InlineData("outride", "outride", "outrides", "outrode", "outridden", "outriding")]
        [InlineData("outrun", "outrun", "outruns", "outran", "outrun", "outrunning")]
        [InlineData("outsell", "outsell", "outsells", "outsold", "outsold", "outselling")]
        [InlineData("outshine", "outshine", "outshines", "outshined;outshone", "outshined;outshone", "outshining")]
        [InlineData("outwear", "outwear", "outwears", "outwore", "outworn", "outwearing")]
        public void Generate_UsesCompleteOutParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("outbid", "outbidden", "PastParticiple")]
        [InlineData("outdo", "outdid", "Past")]
        [InlineData("outdo", "outdone", "PastParticiple")]
        [InlineData("outdraw", "outdrawn", "PastParticiple")]
        [InlineData("outdraw", "outdrew", "Past")]
        [InlineData("outfight", "outfought", "Past,PastParticiple")]
        [InlineData("outgo", "outgone", "PastParticiple")]
        [InlineData("outgo", "outwent", "Past")]
        [InlineData("outgrow", "outgrew", "Past")]
        [InlineData("outgrow", "outgrown", "PastParticiple")]
        [InlineData("outride", "outridden", "PastParticiple")]
        [InlineData("outride", "outrode", "Past")]
        [InlineData("outrun", "outran", "Past")]
        [InlineData("outsell", "outsold", "Past,PastParticiple")]
        [InlineData("outshine", "outshone", "Past,PastParticiple")]
        [InlineData("outwear", "outwore", "Past")]
        [InlineData("outwear", "outworn", "PastParticiple")]
        public void Generate_ClassifiesOutSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesOutCatalogAlternativesAndSameSurfaceKinds()
        {
            var generator = new EnglishFormGenerator();

            var outbid = generator.Generate(CreateLexeme("outbid", "v", EnglishPartOfSpeech.Verb, new[] { "outbid" }));
            Assert.Equal(new[] { "outbid" }, FormsOfKind(outbid, EnglishFormKind.Past));
            Assert.Equal(new[] { "outbid", "outbidden" }, FormsOfKind(outbid, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(outbid, EnglishFormKind.Past, "outbid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(outbid, EnglishFormKind.PastParticiple, "outbid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(outbid, EnglishFormKind.PastParticiple, "outbidden").SourceKind);
            Assert.Equal(1, outbid.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "outbid"));
            Assert.Equal(1, outbid.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "outbid"));

            var outshine = generator.Generate(CreateLexeme("outshine", "v", EnglishPartOfSpeech.Verb, new[] { "outshone" }));
            Assert.Equal(new[] { "outshined", "outshone" }, FormsOfKind(outshine, EnglishFormKind.Past));
            Assert.Equal(new[] { "outshined", "outshone" }, FormsOfKind(outshine, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(outshine, EnglishFormKind.Past, "outshone").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(outshine, EnglishFormKind.PastParticiple, "outshone").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(outshine, EnglishFormKind.Past, "outshined").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(outshine, EnglishFormKind.PastParticiple, "outshined").SourceKind);
            Assert.Equal(1, outshine.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "outshone"));
            Assert.Equal(1, outshine.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "outshone"));

            var outrun = generator.Generate(CreateLexeme("outrun", "v", EnglishPartOfSpeech.Verb, Array.Empty<string>()));
            Assert.Equal(new[] { "outrun" }, FormsOfKind(outrun, EnglishFormKind.Base));
            Assert.Equal(new[] { "outrun" }, FormsOfKind(outrun, EnglishFormKind.PastParticiple));
            Assert.Equal(1, outrun.Count(form => form.Kind == EnglishFormKind.Base && form.Surface == "outrun"));
            Assert.Equal(1, outrun.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "outrun"));
        }

        [Theory]
        [InlineData("outbid", "outbided")]
        [InlineData("outdo", "outdoed")]
        [InlineData("outdraw", "outdrawed")]
        [InlineData("outfight", "outfighted")]
        [InlineData("outgo", "outgoed")]
        [InlineData("outgrow", "outgrowed")]
        [InlineData("outride", "outrided")]
        [InlineData("outrun", "outruned")]
        [InlineData("outsell", "outselled")]
        [InlineData("outwear", "outweared")]
        public void Generate_DoesNotEmitOutPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("overbear", "overbear", "overbears", "overbore", "overborne", "overbearing")]
        [InlineData("overbid", "overbid", "overbids", "overbid", "overbid;overbidden", "overbidding")]
        [InlineData("overdo", "overdo", "overdoes", "overdid", "overdone", "overdoing")]
        [InlineData("overdraw", "overdraw", "overdraws", "overdrew", "overdrawn", "overdrawing")]
        [InlineData("overdrive", "overdrive", "overdrives", "overdrove", "overdriven", "overdriving")]
        [InlineData("overflow", "overflow", "overflows", "overflowed", "overflowed;overflown", "overflowing")]
        [InlineData("overfly", "overfly", "overflies", "overflew", "overflown", "overflying")]
        [InlineData("overgrow", "overgrow", "overgrows", "overgrew", "overgrown", "overgrowing")]
        [InlineData("overhang", "overhang", "overhangs", "overhung", "overhung", "overhanging")]
        [InlineData("overhear", "overhear", "overhears", "overheard", "overheard", "overhearing")]
        [InlineData("overlay", "overlay", "overlays", "overlaid", "overlaid", "overlaying")]
        [InlineData("overlie", "overlie", "overlies", "overlay", "overlain", "overlying")]
        [InlineData("overpay", "overpay", "overpays", "overpaid", "overpaid", "overpaying")]
        [InlineData("override", "override", "overrides", "overrode", "overridden", "overriding")]
        [InlineData("overrun", "overrun", "overruns", "overran", "overrun", "overrunning")]
        [InlineData("oversee", "oversee", "oversees", "oversaw", "overseen", "overseeing")]
        [InlineData("oversew", "oversew", "oversews", "oversewed", "oversewed;oversewn", "oversewing")]
        [InlineData("overshoot", "overshoot", "overshoots", "overshot", "overshot", "overshooting")]
        [InlineData("oversleep", "oversleep", "oversleeps", "overslept", "overslept", "oversleeping")]
        [InlineData("overspend", "overspend", "overspends", "overspent", "overspent", "overspending")]
        [InlineData("overtake", "overtake", "overtakes", "overtook", "overtaken", "overtaking")]
        [InlineData("overthrow", "overthrow", "overthrows", "overthrew", "overthrown", "overthrowing")]
        [InlineData("overwrite", "overwrite", "overwrites", "overwrote", "overwritten", "overwriting")]
        public void Generate_UsesCompleteOverParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("overbear", "overbore", "Past")]
        [InlineData("overbear", "overborne", "PastParticiple")]
        [InlineData("overbid", "overbidden", "PastParticiple")]
        [InlineData("overdo", "overdid", "Past")]
        [InlineData("overdo", "overdone", "PastParticiple")]
        [InlineData("overdraw", "overdrawn", "PastParticiple")]
        [InlineData("overdraw", "overdrew", "Past")]
        [InlineData("overdrive", "overdriven", "PastParticiple")]
        [InlineData("overdrive", "overdrove", "Past")]
        [InlineData("overflow", "overflown", "PastParticiple")]
        [InlineData("overfly", "overflown", "PastParticiple")]
        [InlineData("overfly", "overflew", "Past")]
        [InlineData("overgrow", "overgrew", "Past")]
        [InlineData("overgrow", "overgrown", "PastParticiple")]
        [InlineData("overhang", "overhung", "Past,PastParticiple")]
        [InlineData("overhear", "overheard", "Past,PastParticiple")]
        [InlineData("overlay", "overlaid", "Past,PastParticiple")]
        [InlineData("overlie", "overlay", "Past")]
        [InlineData("overlie", "overlain", "PastParticiple")]
        [InlineData("overpay", "overpaid", "Past,PastParticiple")]
        [InlineData("override", "overridden", "PastParticiple")]
        [InlineData("override", "overrode", "Past")]
        [InlineData("overrun", "overran", "Past")]
        [InlineData("oversee", "oversaw", "Past")]
        [InlineData("oversee", "overseen", "PastParticiple")]
        [InlineData("oversew", "oversewn", "PastParticiple")]
        [InlineData("overshoot", "overshot", "Past,PastParticiple")]
        [InlineData("oversleep", "overslept", "Past,PastParticiple")]
        [InlineData("overspend", "overspent", "Past,PastParticiple")]
        [InlineData("overtake", "overtaken", "PastParticiple")]
        [InlineData("overtake", "overtook", "Past")]
        [InlineData("overthrow", "overthrew", "Past")]
        [InlineData("overthrow", "overthrown", "PastParticiple")]
        [InlineData("overwrite", "overwritten", "PastParticiple")]
        [InlineData("overwrite", "overwrote", "Past")]
        public void Generate_ClassifiesOverSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesOverCatalogAlternativesAndSameSurfaceKinds()
        {
            var generator = new EnglishFormGenerator();

            var overbid = generator.Generate(CreateLexeme("overbid", "v", EnglishPartOfSpeech.Verb, new[] { "overbid" }));
            Assert.Equal(new[] { "overbid" }, FormsOfKind(overbid, EnglishFormKind.Past));
            Assert.Equal(new[] { "overbid", "overbidden" }, FormsOfKind(overbid, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(overbid, EnglishFormKind.Past, "overbid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(overbid, EnglishFormKind.PastParticiple, "overbid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(overbid, EnglishFormKind.PastParticiple, "overbidden").SourceKind);
            Assert.DoesNotContain(overbid, form => form.Kind == EnglishFormKind.Past && form.Surface == "overbidden");

            var overflow = generator.Generate(CreateLexeme("overflow", "v", EnglishPartOfSpeech.Verb, new[] { "overflown" }));
            Assert.Equal(new[] { "overflowed" }, FormsOfKind(overflow, EnglishFormKind.Past));
            Assert.Equal(new[] { "overflowed", "overflown" }, FormsOfKind(overflow, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(overflow, EnglishFormKind.PastParticiple, "overflown").SourceKind);
            Assert.DoesNotContain(overflow, form => form.Kind == EnglishFormKind.Past && form.Surface == "overflown");

            var overlay = generator.Generate(CreateLexeme("overlay", "v", EnglishPartOfSpeech.Verb, new[] { "overlaid" }));
            var overlie = generator.Generate(CreateLexeme("overlie", "v", EnglishPartOfSpeech.Verb, new[] { "overlay" }));
            Assert.Equal(new[] { "overlaid" }, FormsOfKind(overlay, EnglishFormKind.Past));
            Assert.Equal(new[] { "overlaid" }, FormsOfKind(overlay, EnglishFormKind.PastParticiple));
            Assert.Equal(new[] { "overlay" }, FormsOfKind(overlie, EnglishFormKind.Past));
            Assert.Equal(new[] { "overlain" }, FormsOfKind(overlie, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(overlay, EnglishFormKind.Past, "overlaid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(overlie, EnglishFormKind.Past, "overlay").SourceKind);

            var overrun = generator.Generate(CreateLexeme("overrun", "v", EnglishPartOfSpeech.Verb, Array.Empty<string>()));
            Assert.Equal(new[] { "overrun" }, FormsOfKind(overrun, EnglishFormKind.Base));
            Assert.Equal(new[] { "overrun" }, FormsOfKind(overrun, EnglishFormKind.PastParticiple));
            Assert.Equal(1, overrun.Count(form => form.Kind == EnglishFormKind.Base && form.Surface == "overrun"));
            Assert.Equal(1, overrun.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "overrun"));

            var oversew = generator.Generate(CreateLexeme("oversew", "v", EnglishPartOfSpeech.Verb, new[] { "oversewn" }));
            Assert.Equal(new[] { "oversewed" }, FormsOfKind(oversew, EnglishFormKind.Past));
            Assert.Equal(new[] { "oversewed", "oversewn" }, FormsOfKind(oversew, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(oversew, EnglishFormKind.PastParticiple, "oversewn").SourceKind);
            Assert.DoesNotContain(oversew, form => form.Kind == EnglishFormKind.Past && form.Surface == "oversewn");
        }

        [Theory]
        [InlineData("overbear", "overbeared")]
        [InlineData("overbid", "overbided")]
        [InlineData("overdo", "overdoed")]
        [InlineData("overdraw", "overdrawed")]
        [InlineData("overdrive", "overdrived")]
        [InlineData("overfly", "overflied")]
        [InlineData("overgrow", "overgrowed")]
        [InlineData("overhang", "overhanged")]
        [InlineData("overhear", "overheared")]
        [InlineData("overlay", "overlayed")]
        [InlineData("overlie", "overlied")]
        [InlineData("overpay", "overpayed")]
        [InlineData("override", "overrided")]
        [InlineData("overrun", "overruned")]
        [InlineData("oversee", "overseed")]
        [InlineData("overshoot", "overshooted")]
        [InlineData("oversleep", "oversleeped")]
        [InlineData("overspend", "overspended")]
        [InlineData("overtake", "overtaked")]
        [InlineData("overthrow", "overthrowed")]
        [InlineData("overwrite", "overwrited")]
        public void Generate_DoesNotEmitOverPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("partake", "partake", "partakes", "partook", "partaken", "partaking")]
        [InlineData("pay", "pay", "pays", "paid", "paid", "paying")]
        [InlineData("pen", "pen", "pens", "penned;pent", "penned;pent", "penning")]
        [InlineData("prepay", "prepay", "prepays", "prepaid", "prepaid", "prepaying")]
        [InlineData("prove", "prove", "proves", "proved", "proved;proven", "proving")]
        [InlineData("quick-freeze", "quick-freeze", "quick-freezes", "quick-froze", "quick-frozen", "quick-freezing")]
        public void Generate_UsesCompletePqParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("partake", "partaken", "PastParticiple")]
        [InlineData("partake", "partook", "Past")]
        [InlineData("pay", "paid", "Past,PastParticiple")]
        [InlineData("pen", "pent", "Past,PastParticiple")]
        [InlineData("prepay", "prepaid", "Past,PastParticiple")]
        [InlineData("prove", "proven", "PastParticiple")]
        [InlineData("quick-freeze", "quick-froze", "Past")]
        [InlineData("quick-freeze", "quick-frozen", "PastParticiple")]
        public void Generate_ClassifiesPqSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesPqAlternativesAndSeparatesProvenAndPentKinds()
        {
            var generator = new EnglishFormGenerator();

            var pen = generator.Generate(CreateLexeme("pen", "v", EnglishPartOfSpeech.Verb, new[] { "pent" }));
            Assert.Equal(new[] { "penned", "pent" }, FormsOfKind(pen, EnglishFormKind.Past));
            Assert.Equal(new[] { "penned", "pent" }, FormsOfKind(pen, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(pen, EnglishFormKind.Past, "pent").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(pen, EnglishFormKind.PastParticiple, "pent").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(pen, EnglishFormKind.Past, "penned").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(pen, EnglishFormKind.PastParticiple, "penned").SourceKind);
            Assert.DoesNotContain(pen, form => form.Surface == "pened");

            var prove = generator.Generate(CreateLexeme("prove", "v", EnglishPartOfSpeech.Verb, new[] { "proven" }));
            Assert.Equal(new[] { "proved" }, FormsOfKind(prove, EnglishFormKind.Past));
            Assert.Equal(new[] { "proved", "proven" }, FormsOfKind(prove, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(prove, EnglishFormKind.PastParticiple, "proven").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(prove, EnglishFormKind.Past, "proved").SourceKind);
            Assert.DoesNotContain(prove, form => form.Kind == EnglishFormKind.Past && form.Surface == "proven");

            var pay = generator.Generate(CreateLexeme("pay", "v", EnglishPartOfSpeech.Verb, new[] { "paid" }));
            Assert.Equal(new[] { "paid" }, FormsOfKind(pay, EnglishFormKind.Past));
            Assert.Equal(new[] { "paid" }, FormsOfKind(pay, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(pay, EnglishFormKind.Past, "paid").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(pay, EnglishFormKind.PastParticiple, "paid").SourceKind);
            Assert.DoesNotContain(pay, form => form.Surface == "payed");
        }

        [Theory]
        [InlineData("partake", "partaked")]
        [InlineData("pay", "payed")]
        [InlineData("pen", "pened")]
        [InlineData("prepay", "prepayed")]
        [InlineData("quick-freeze", "quick-freezed")]
        public void Generate_DoesNotEmitPqPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("reave", "reave", "reaves", "reaved;reft", "reaved;reft", "reaving")]
        [InlineData("rebuild", "rebuild", "rebuilds", "rebuilt", "rebuilt", "rebuilding")]
        [InlineData("redo", "redo", "redoes", "redid", "redone", "redoing")]
        [InlineData("reeve", "reeve", "reeves", "reeved;rove", "reeved;rove", "reeving")]
        [InlineData("rehear", "rehear", "rehears", "reheard", "reheard", "rehearing")]
        [InlineData("remake", "remake", "remakes", "remade", "remade", "remaking")]
        [InlineData("rend", "rend", "rends", "rent;rended", "rent;rended", "rending")]
        [InlineData("repay", "repay", "repays", "repaid", "repaid", "repaying")]
        [InlineData("rerun", "rerun", "reruns", "reran", "rerun", "rerunning")]
        [InlineData("resew", "resew", "resews", "resewed", "resewed;resewn", "resewing")]
        [InlineData("retake", "retake", "retakes", "retook", "retaken", "retaking")]
        [InlineData("retell", "retell", "retells", "retold", "retold", "retelling")]
        [InlineData("rethink", "rethink", "rethinks", "rethought", "rethought", "rethinking")]
        [InlineData("rewind", "rewind", "rewinds", "rewound", "rewound", "rewinding")]
        [InlineData("rewrite", "rewrite", "rewrites", "rewrote", "rewritten", "rewriting")]
        [InlineData("ride", "ride", "rides", "rode", "ridden", "riding")]
        [InlineData("ring", "ring", "rings", "rang;ringed", "ringed;rung", "ringing")]
        [InlineData("rise", "rise", "rises", "rose", "risen", "rising")]
        [InlineData("rive", "rive", "rives", "rived", "rived;riven", "riving")]
        [InlineData("rough-hew", "rough-hew", "rough-hews", "rough-hewed", "rough-hewed;rough-hewn", "rough-hewing")]
        public void Generate_UsesCompleteRParadigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("reave", "reft", "Past,PastParticiple")]
        [InlineData("rebuild", "rebuilt", "Past,PastParticiple")]
        [InlineData("redo", "redid", "Past")]
        [InlineData("redo", "redone", "PastParticiple")]
        [InlineData("reeve", "rove", "Past,PastParticiple")]
        [InlineData("rehear", "reheard", "Past,PastParticiple")]
        [InlineData("remake", "remade", "Past,PastParticiple")]
        [InlineData("rend", "rent", "Past,PastParticiple")]
        [InlineData("repay", "repaid", "Past,PastParticiple")]
        [InlineData("rerun", "reran", "Past")]
        [InlineData("resew", "resewn", "PastParticiple")]
        [InlineData("retake", "retaken", "PastParticiple")]
        [InlineData("retake", "retook", "Past")]
        [InlineData("retell", "retold", "Past,PastParticiple")]
        [InlineData("rethink", "rethought", "Past,PastParticiple")]
        [InlineData("rewind", "rewound", "Past,PastParticiple")]
        [InlineData("rewrite", "rewritten", "PastParticiple")]
        [InlineData("rewrite", "rewrote", "Past")]
        [InlineData("ride", "ridden", "PastParticiple")]
        [InlineData("ride", "rode", "Past")]
        [InlineData("ring", "rang", "Past")]
        [InlineData("ring", "rung", "PastParticiple")]
        [InlineData("rise", "risen", "PastParticiple")]
        [InlineData("rise", "rose", "Past")]
        [InlineData("rive", "riven", "PastParticiple")]
        [InlineData("rough-hew", "rough-hewn", "PastParticiple")]
        public void Generate_ClassifiesRSourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesRAlternativesAndSeparatesRingAndRerunKinds()
        {
            var generator = new EnglishFormGenerator();

            var reave = generator.Generate(CreateLexeme("reave", "v", EnglishPartOfSpeech.Verb, new[] { "reft" }));
            Assert.Equal(new[] { "reaved", "reft" }, FormsOfKind(reave, EnglishFormKind.Past));
            Assert.Equal(new[] { "reaved", "reft" }, FormsOfKind(reave, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(reave, EnglishFormKind.Past, "reft").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(reave, EnglishFormKind.PastParticiple, "reft").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(reave, EnglishFormKind.Past, "reaved").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(reave, EnglishFormKind.PastParticiple, "reaved").SourceKind);

            var ring = generator.Generate(CreateLexeme("ring", "v", EnglishPartOfSpeech.Verb, new[] { "rang" }));
            Assert.Equal(new[] { "rang", "ringed" }, FormsOfKind(ring, EnglishFormKind.Past));
            Assert.Equal(new[] { "ringed", "rung" }, FormsOfKind(ring, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(ring, EnglishFormKind.Past, "rang").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(ring, EnglishFormKind.Past, "ringed").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(ring, EnglishFormKind.PastParticiple, "ringed").SourceKind);
            Assert.DoesNotContain(ring, form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "rang");
            Assert.DoesNotContain(ring, form => form.Kind == EnglishFormKind.Past && form.Surface == "rung");

            var rerun = generator.Generate(CreateLexeme("rerun", "v", EnglishPartOfSpeech.Verb, Array.Empty<string>()));
            Assert.Equal(new[] { "rerun" }, FormsOfKind(rerun, EnglishFormKind.Base));
            Assert.Equal(new[] { "rerun" }, FormsOfKind(rerun, EnglishFormKind.PastParticiple));
            Assert.Equal(1, rerun.Count(form => form.Kind == EnglishFormKind.Base && form.Surface == "rerun"));
            Assert.Equal(1, rerun.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "rerun"));

            var resew = generator.Generate(CreateLexeme("resew", "v", EnglishPartOfSpeech.Verb, new[] { "resewn" }));
            Assert.Equal(new[] { "resewed" }, FormsOfKind(resew, EnglishFormKind.Past));
            Assert.Equal(new[] { "resewed", "resewn" }, FormsOfKind(resew, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(resew, EnglishFormKind.PastParticiple, "resewn").SourceKind);
            Assert.DoesNotContain(resew, form => form.Kind == EnglishFormKind.Past && form.Surface == "resewn");

            var rive = generator.Generate(CreateLexeme("rive", "v", EnglishPartOfSpeech.Verb, new[] { "riven" }));
            Assert.Equal(new[] { "rived" }, FormsOfKind(rive, EnglishFormKind.Past));
            Assert.Equal(new[] { "rived", "riven" }, FormsOfKind(rive, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(rive, EnglishFormKind.PastParticiple, "riven").SourceKind);
            Assert.DoesNotContain(rive, form => form.Kind == EnglishFormKind.Past && form.Surface == "riven");

            var roughHew = generator.Generate(CreateLexeme("rough-hew", "v", EnglishPartOfSpeech.Verb, new[] { "rough-hewn" }));
            Assert.Equal(new[] { "rough-hewed" }, FormsOfKind(roughHew, EnglishFormKind.Past));
            Assert.Equal(new[] { "rough-hewed", "rough-hewn" }, FormsOfKind(roughHew, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(roughHew, EnglishFormKind.PastParticiple, "rough-hewn").SourceKind);
            Assert.DoesNotContain(roughHew, form => form.Kind == EnglishFormKind.Past && form.Surface == "rough-hewn");
        }

        [Theory]
        [InlineData("rebuild", "rebuilded")]
        [InlineData("redo", "redoed")]
        [InlineData("rehear", "reheared")]
        [InlineData("remake", "remaked")]
        [InlineData("repay", "repayed")]
        [InlineData("rerun", "reruned")]
        [InlineData("retake", "retaked")]
        [InlineData("retell", "retelled")]
        [InlineData("rethink", "rethinked")]
        [InlineData("rewind", "rewinded")]
        [InlineData("rewrite", "rewrited")]
        [InlineData("ride", "rided")]
        [InlineData("rise", "rised")]
        public void Generate_DoesNotEmitRPseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("saw", "saw", "saws", "sawed", "sawed;sawn", "sawing")]
        [InlineData("see", "see", "sees", "saw", "seen", "seeing")]
        [InlineData("seek", "seek", "seeks", "sought", "sought", "seeking")]
        [InlineData("sell", "sell", "sells", "sold", "sold", "selling")]
        [InlineData("send", "send", "sends", "sent", "sent", "sending")]
        [InlineData("sew", "sew", "sews", "sewed", "sewed;sewn", "sewing")]
        [InlineData("shake", "shake", "shakes", "shook", "shaken", "shaking")]
        [InlineData("shave", "shave", "shaves", "shaved", "shaved;shaven", "shaving")]
        [InlineData("shew", "shew", "shews", "shewed", "shewed;shewn", "shewing")]
        [InlineData("shine", "shine", "shines", "shined;shone", "shined;shone", "shining")]
        [InlineData("shit", "shit", "shits", "shit;shat", "shit;shat", "shitting")]
        [InlineData("shoe", "shoe", "shoes", "shod;shoed", "shod;shoed", "shoeing")]
        [InlineData("shoot", "shoot", "shoots", "shot", "shot", "shooting")]
        [InlineData("show", "show", "shows", "showed", "showed;shown", "showing")]
        [InlineData("shrink", "shrink", "shrinks", "shrank;shrunk", "shrunk;shrunken", "shrinking")]
        [InlineData("shrive", "shrive", "shrives", "shrived;shrove", "shrived;shriven", "shriving")]
        public void Generate_UsesCompleteS1Paradigms(
            string lemma,
            string baseFormsText,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SortedSourceForms(baseFormsText), FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(SortedSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SortedSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SortedSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SortedSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
        }

        [Theory]
        [InlineData("saw", "sawn", "PastParticiple")]
        [InlineData("see", "saw", "Past")]
        [InlineData("see", "seen", "PastParticiple")]
        [InlineData("seek", "sought", "Past,PastParticiple")]
        [InlineData("sell", "sold", "Past,PastParticiple")]
        [InlineData("send", "sent", "Past,PastParticiple")]
        [InlineData("sew", "sewn", "PastParticiple")]
        [InlineData("shake", "shaken", "PastParticiple")]
        [InlineData("shake", "shook", "Past")]
        [InlineData("shave", "shaven", "PastParticiple")]
        [InlineData("shew", "shewn", "PastParticiple")]
        [InlineData("shine", "shone", "Past,PastParticiple")]
        [InlineData("shit", "shat", "Past,PastParticiple")]
        [InlineData("shoe", "shod", "Past,PastParticiple")]
        [InlineData("shoot", "shot", "Past,PastParticiple")]
        [InlineData("show", "shown", "PastParticiple")]
        [InlineData("shrink", "shrank", "Past")]
        [InlineData("shrink", "shrunk", "Past,PastParticiple")]
        [InlineData("shrink", "shrunken", "PastParticiple")]
        [InlineData("shrive", "shriven", "PastParticiple")]
        [InlineData("shrive", "shrove", "Past")]
        public void Generate_ClassifiesS1SourceFormsByExactFormKind(
            string lemma,
            string sourceForm,
            string sourceKindsText)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                new[] { sourceForm }));
            var expectedKinds = sourceKindsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                .OrderBy(value => value)
                .ToArray();
            var actualKinds = forms
                .Where(form => form.Surface == sourceForm && form.SourceKind == EnglishFormSourceKind.Source)
                .Select(form => form.Kind)
                .OrderBy(value => value)
                .ToArray();

            Assert.Equal(expectedKinds, actualKinds);
            foreach (var kind in expectedKinds)
            {
                Assert.Equal(EnglishFormSourceKind.Source, Find(forms, kind, sourceForm).SourceKind);
                Assert.Equal(1, forms.Count(form => form.Kind == kind && form.Surface == sourceForm));
            }
        }

        [Fact]
        public void Generate_PreservesS1AlternativesAndSeparatesSharedSurfaces()
        {
            var generator = new EnglishFormGenerator();

            var saw = generator.Generate(CreateLexeme("saw", "v", EnglishPartOfSpeech.Verb, new[] { "sawn" }));
            Assert.Equal(new[] { "sawed" }, FormsOfKind(saw, EnglishFormKind.Past));
            Assert.Equal(new[] { "sawed", "sawn" }, FormsOfKind(saw, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(saw, EnglishFormKind.PastParticiple, "sawn").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(saw, EnglishFormKind.PastParticiple, "sawed").SourceKind);
            Assert.DoesNotContain(saw, form => form.Kind == EnglishFormKind.Past && form.Surface == "sawn");

            var shine = generator.Generate(CreateLexeme("shine", "v", EnglishPartOfSpeech.Verb, new[] { "shone" }));
            Assert.Equal(new[] { "shined", "shone" }, FormsOfKind(shine, EnglishFormKind.Past));
            Assert.Equal(new[] { "shined", "shone" }, FormsOfKind(shine, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(shine, EnglishFormKind.Past, "shone").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(shine, EnglishFormKind.PastParticiple, "shone").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(shine, EnglishFormKind.Past, "shined").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Rule, Find(shine, EnglishFormKind.PastParticiple, "shined").SourceKind);

            var shit = generator.Generate(CreateLexeme("shit", "v", EnglishPartOfSpeech.Verb, Array.Empty<string>()));
            Assert.Equal(new[] { "shit" }, FormsOfKind(shit, EnglishFormKind.Base));
            Assert.Equal(1, shit.Count(form => form.Kind == EnglishFormKind.Base && form.Surface == "shit"));
            Assert.Equal(new[] { "shat", "shit" }, FormsOfKind(shit, EnglishFormKind.Past));
            Assert.Equal(new[] { "shat", "shit" }, FormsOfKind(shit, EnglishFormKind.PastParticiple));
            Assert.Equal(1, shit.Count(form => form.Kind == EnglishFormKind.Past && form.Surface == "shit"));
            Assert.Equal(1, shit.Count(form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "shit"));

            var shoe = generator.Generate(CreateLexeme("shoe", "v", EnglishPartOfSpeech.Verb, new[] { "shod" }));
            Assert.Equal(new[] { "shod", "shoed" }, FormsOfKind(shoe, EnglishFormKind.Past));
            Assert.Equal(new[] { "shod", "shoed" }, FormsOfKind(shoe, EnglishFormKind.PastParticiple));
            Assert.Equal(EnglishFormSourceKind.Source, Find(shoe, EnglishFormKind.Past, "shod").SourceKind);
            Assert.Equal(EnglishFormSourceKind.Source, Find(shoe, EnglishFormKind.PastParticiple, "shod").SourceKind);

            var shrink = generator.Generate(CreateLexeme("shrink", "v", EnglishPartOfSpeech.Verb, new[] { "shrunk" }));
            Assert.Equal(new[] { "shrank", "shrunk" }, FormsOfKind(shrink, EnglishFormKind.Past));
            Assert.Equal(new[] { "shrunk", "shrunken" }, FormsOfKind(shrink, EnglishFormKind.PastParticiple));
            Assert.DoesNotContain(shrink, form => form.Kind == EnglishFormKind.Past && form.Surface == "shrunken");

            var shrive = generator.Generate(CreateLexeme("shrive", "v", EnglishPartOfSpeech.Verb, new[] { "shrove" }));
            Assert.Equal(new[] { "shrived", "shrove" }, FormsOfKind(shrive, EnglishFormKind.Past));
            Assert.Equal(new[] { "shrived", "shriven" }, FormsOfKind(shrive, EnglishFormKind.PastParticiple));
            Assert.DoesNotContain(shrive, form => form.Kind == EnglishFormKind.PastParticiple && form.Surface == "shrove");
        }

        [Theory]
        [InlineData("see", "seed")]
        [InlineData("seek", "seeked")]
        [InlineData("sell", "selled")]
        [InlineData("send", "sended")]
        [InlineData("shake", "shaked")]
        [InlineData("shit", "shited")]
        [InlineData("shit", "shiting")]
        [InlineData("shoot", "shooted")]
        [InlineData("shrink", "shrinked")]
        public void Generate_DoesNotEmitS1PseudoForms(string lemma, string pseudoForm)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.DoesNotContain(forms, form => form.Surface == pseudoForm);
        }

        [Theory]
        [InlineData("abide", "abode", "Past,PastParticiple")]
        [InlineData("aby", "abought;abye", "Past,PastParticiple;Base")]
        [InlineData("address", "addrest", "Past,PastParticiple")]
        [InlineData("arise", "arisen;arose", "PastParticiple;Past")]
        [InlineData("awake", "awoke;awoken", "Past,PastParticiple;PastParticiple")]
        [InlineData("baby-sit", "baby-sat;baby-sitting", "Past,PastParticiple;PresentParticiple")]
        [InlineData("backbite", "backbit;backbitten", "Past;PastParticiple")]
        [InlineData("backslide", "backslid;backslidden", "Past,PastParticiple;PastParticiple")]
        [InlineData("bad", "worse;worst", "Comparative;Superlative")]
        [InlineData("bear", "bore;born;borne", "Past;PastParticiple;PastParticiple")]
        [InlineData("befall", "befallen;befell", "PastParticiple;Past")]
        [InlineData("beget", "begat;begetting;begot;begotten", "Past;PresentParticiple;Past,PastParticiple;PastParticiple")]
        [InlineData("begin", "began;beginning;begun", "Past;PresentParticiple;PastParticiple")]
        [InlineData("behold", "beheld;beholden", "Past,PastParticiple;PastParticiple")]
        [InlineData("bend", "bent", "Past,PastParticiple")]
        [InlineData("beseech", "besought", "Past,PastParticiple")]
        [InlineData("bespeak", "bespoke;bespoken", "Past;PastParticiple")]
        [InlineData("bestrew", "bestrewn", "PastParticiple")]
        [InlineData("bestride", "bestrid;bestridden;bestrode", "Past;PastParticiple;Past")]
        [InlineData("bethink", "bethought", "Past,PastParticiple")]
        [InlineData("bid", "bade;bidden;bidding", "Past,PastParticiple;PastParticiple;PresentParticiple")]
        [InlineData("bind", "bound", "Past,PastParticiple")]
        [InlineData("bite", "bit;bitten", "Past;PastParticiple")]
        [InlineData("bless", "blest", "Past,PastParticiple")]
        [InlineData("blow", "blew;blown", "Past;PastParticiple")]
        [InlineData("break", "broke;broken", "Past;PastParticiple")]
        [InlineData("bring", "brought", "Past,PastParticiple")]
        [InlineData("browbeat", "browbeaten", "PastParticiple")]
        [InlineData("build", "built", "Past,PastParticiple")]
        [InlineData("burn", "burnt", "Past,PastParticiple")]
        [InlineData("buy", "bought", "Past,PastParticiple")]
        [InlineData("bypass", "bypast", "Past,PastParticiple")]
        public void Generate_ClassifiesAbSourceFormsByExplicitKind(
            string lemma,
            string sourceFormsText,
            string sourceKindsText)
        {
            var sourceForms = SplitSourceForms(sourceFormsText);
            var expectedKindGroups = sourceKindsText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                lemma == "bad" ? EnglishPartOfSpeech.Adjective : EnglishPartOfSpeech.Verb,
                sourceForms));

            Assert.Equal(sourceForms.Length, expectedKindGroups.Length);
            for (var index = 0; index < sourceForms.Length; index++)
            {
                var expectedKinds = expectedKindGroups[index]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => Enum.Parse<EnglishFormKind>(value, ignoreCase: false))
                    .OrderBy(value => value)
                    .ToArray();
                var actualKinds = forms
                    .Where(form => form.SourceKind == EnglishFormSourceKind.Source && form.Surface == sourceForms[index])
                    .Select(form => form.Kind)
                    .OrderBy(value => value)
                    .ToArray();

                Assert.Equal(expectedKinds, actualKinds);
            }
        }

        [Theory]
        [InlineData("abide", "abides", "abided;abode", "abided;abode", "abiding")]
        [InlineData("aby", "abyes;abys", "abought;abyed", "abought;abyed", "abyeing;abying")]
        [InlineData("address", "addresses", "addressed;addrest", "addressed;addrest", "addressing")]
        [InlineData("arise", "arises", "arose", "arisen", "arising")]
        [InlineData("awake", "awakes", "awaked;awoke", "awaked;awoke;awoken", "awaking")]
        [InlineData("baby-sit", "baby-sits", "baby-sat", "baby-sat", "baby-sitting")]
        [InlineData("backbite", "backbites", "backbit", "backbitten", "backbiting")]
        [InlineData("backslide", "backslides", "backslid", "backslid;backslidden", "backsliding")]
        [InlineData("bear", "bears", "bore", "born;borne", "bearing")]
        [InlineData("befall", "befalls", "befell", "befallen", "befalling")]
        [InlineData("beget", "begets", "begat;begot", "begot;begotten", "begetting")]
        [InlineData("begin", "begins", "began", "begun", "beginning")]
        [InlineData("behold", "beholds", "beheld", "beheld;beholden", "beholding")]
        [InlineData("bend", "bends", "bent", "bent", "bending")]
        [InlineData("beseech", "beseeches", "beseeched;besought", "beseeched;besought", "beseeching")]
        [InlineData("bespeak", "bespeaks", "bespoke", "bespoken", "bespeaking")]
        [InlineData("bestrew", "bestrews", "bestrewed", "bestrewed;bestrewn", "bestrewing")]
        [InlineData("bestride", "bestrides", "bestrid;bestrode", "bestridden", "bestriding")]
        [InlineData("bethink", "bethinks", "bethought", "bethought", "bethinking")]
        [InlineData("bid", "bids", "bade;bid", "bade;bid;bidden", "bidding")]
        [InlineData("bind", "binds", "bound", "bound", "binding")]
        [InlineData("bite", "bites", "bit", "bitten", "biting")]
        [InlineData("bless", "blesses", "blessed;blest", "blessed;blest", "blessing")]
        [InlineData("blow", "blows", "blew", "blown", "blowing")]
        [InlineData("break", "breaks", "broke", "broken", "breaking")]
        [InlineData("bring", "brings", "brought", "brought", "bringing")]
        [InlineData("browbeat", "browbeats", "browbeat", "browbeaten", "browbeating")]
        [InlineData("build", "builds", "built", "built", "building")]
        [InlineData("burn", "burns", "burned;burnt", "burned;burnt", "burning")]
        [InlineData("buy", "buys", "bought", "bought", "buying")]
        [InlineData("bypass", "bypasses", "bypassed;bypast", "bypassed;bypast", "bypassing")]
        public void Generate_UsesCompleteAbVerbParadigmsWithoutPseudoForms(
            string lemma,
            string thirdPerson,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                lemma,
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(SplitSourceForms(thirdPerson), FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(SplitSourceForms(past), FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(SplitSourceForms(pastParticiple), FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(SplitSourceForms(presentParticiple), FormsOfKind(forms, EnglishFormKind.PresentParticiple));
            Assert.DoesNotContain(forms, form => form.Surface is "arised" or "backbited" or "backslided" or "beated" or "becomed" or "bended" or "binded" or "bited" or "blowed" or "breaked" or "bringed" or "builded" or "buyed" or "abies");
        }

        [Fact]
        public void Generate_AbyUsesBothBaseAndThirdPersonAlternativesWithoutAbies()
        {
            var forms = new EnglishFormGenerator().Generate(CreateLexeme(
                "aby",
                "v",
                EnglishPartOfSpeech.Verb,
                Array.Empty<string>()));

            Assert.Equal(new[] { "aby", "abye" }, FormsOfKind(forms, EnglishFormKind.Base));
            Assert.Equal(new[] { "abyes", "abys" }, FormsOfKind(forms, EnglishFormKind.ThirdPersonSingular));
            Assert.Equal(new[] { "abought", "abyed" }, FormsOfKind(forms, EnglishFormKind.Past));
            Assert.Equal(new[] { "abought", "abyed" }, FormsOfKind(forms, EnglishFormKind.PastParticiple));
            Assert.Equal(new[] { "abyeing", "abying" }, FormsOfKind(forms, EnglishFormKind.PresentParticiple));
            Assert.DoesNotContain(forms, form => form.Surface == "abies");
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

        private static EnglishForm Find(IReadOnlyList<EnglishForm> forms, EnglishFormKind kind, string surface)
        {
            return forms.Single(form => form.Kind == kind && form.Surface == surface);
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

        private static string[] SortedSourceForms(string sourceFormsText)
        {
            return SplitSourceForms(sourceFormsText)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
