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
