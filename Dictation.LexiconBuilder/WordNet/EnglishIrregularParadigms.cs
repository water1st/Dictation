using System;
using System.Collections.Generic;
using System.Linq;

namespace Dictation.LexiconBuilder.WordNet
{
    internal static class EnglishIrregularParadigms
    {
        private static readonly IReadOnlyDictionary<(string Lemma, EnglishPartOfSpeech PartOfSpeech), IReadOnlyDictionary<EnglishFormKind, IReadOnlyList<string>>> Paradigms =
            new Dictionary<(string Lemma, EnglishPartOfSpeech PartOfSpeech), IReadOnlyDictionary<EnglishFormKind, IReadOnlyList<string>>>
            {
                [("be", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "is"),
                    (EnglishFormKind.PresentFirstPersonSingular, "am"),
                    (EnglishFormKind.PresentNonThirdPerson, "are"),
                    (EnglishFormKind.Past, "was"),
                    (EnglishFormKind.Past, "were"),
                    (EnglishFormKind.PastParticiple, "been"),
                    (EnglishFormKind.PresentParticiple, "being")),
                [("go", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "goes"),
                    (EnglishFormKind.Past, "went"),
                    (EnglishFormKind.PastParticiple, "gone"),
                    (EnglishFormKind.PresentParticiple, "going")),
                [("do", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "does"),
                    (EnglishFormKind.Past, "did"),
                    (EnglishFormKind.PastParticiple, "done"),
                    (EnglishFormKind.PresentParticiple, "doing")),
                [("make", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "makes"),
                    (EnglishFormKind.Past, "made"),
                    (EnglishFormKind.PastParticiple, "made"),
                    (EnglishFormKind.PresentParticiple, "making")),
                [("come", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "comes"),
                    (EnglishFormKind.Past, "came"),
                    (EnglishFormKind.PastParticiple, "come"),
                    (EnglishFormKind.PresentParticiple, "coming")),
                [("run", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "runs"),
                    (EnglishFormKind.Past, "ran"),
                    (EnglishFormKind.PastParticiple, "run"),
                    (EnglishFormKind.PresentParticiple, "running")),
                [("good", EnglishPartOfSpeech.Adjective)] = Create(
                    (EnglishFormKind.Comparative, "better"),
                    (EnglishFormKind.Superlative, "best")),
                [("cut", EnglishPartOfSpeech.Verb)] = CreateVerb("cuts", "cut", "cut", "cutting"),
                [("read", EnglishPartOfSpeech.Verb)] = CreateVerb("reads", "read", "read", "reading"),
                [("hit", EnglishPartOfSpeech.Verb)] = CreateVerb("hits", "hit", "hit", "hitting"),
                [("set", EnglishPartOfSpeech.Verb)] = CreateVerb("sets", "set", "set", "setting"),
                [("put", EnglishPartOfSpeech.Verb)] = CreateVerb("puts", "put", "put", "putting"),
                [("let", EnglishPartOfSpeech.Verb)] = CreateVerb("lets", "let", "let", "letting"),
                [("shut", EnglishPartOfSpeech.Verb)] = CreateVerb("shuts", "shut", "shut", "shutting"),
                [("beat", EnglishPartOfSpeech.Verb)] = CreateVerb("beats", "beat", "beaten", "beating"),
                [("become", EnglishPartOfSpeech.Verb)] = CreateVerb("becomes", "became", "become", "becoming"),
                [("overcome", EnglishPartOfSpeech.Verb)] = CreateVerb("overcomes", "overcame", "overcome", "overcoming"),
                [("get", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "gets"),
                    (EnglishFormKind.Past, "got"),
                    (EnglishFormKind.PastParticiple, "got"),
                    (EnglishFormKind.PastParticiple, "gotten"),
                    (EnglishFormKind.PresentParticiple, "getting")),
                [("have", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "has"),
                    (EnglishFormKind.Past, "had"),
                    (EnglishFormKind.PastParticiple, "had"),
                    (EnglishFormKind.PresentParticiple, "having")),
                [("say", EnglishPartOfSpeech.Verb)] = CreateVerb("says", "said", "said", "saying"),
                [("abide", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "abides"),
                    (EnglishFormKind.Past, "abided"),
                    (EnglishFormKind.Past, "abode"),
                    (EnglishFormKind.PastParticiple, "abided"),
                    (EnglishFormKind.PastParticiple, "abode"),
                    (EnglishFormKind.PresentParticiple, "abiding")),
                [("aby", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.Base, "abye"),
                    (EnglishFormKind.ThirdPersonSingular, "abys"),
                    (EnglishFormKind.ThirdPersonSingular, "abyes"),
                    (EnglishFormKind.Past, "abought"),
                    (EnglishFormKind.Past, "abyed"),
                    (EnglishFormKind.PastParticiple, "abought"),
                    (EnglishFormKind.PastParticiple, "abyed"),
                    (EnglishFormKind.PresentParticiple, "abying"),
                    (EnglishFormKind.PresentParticiple, "abyeing")),
                [("address", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "addresses"),
                    (EnglishFormKind.Past, "addressed"),
                    (EnglishFormKind.Past, "addrest"),
                    (EnglishFormKind.PastParticiple, "addressed"),
                    (EnglishFormKind.PastParticiple, "addrest"),
                    (EnglishFormKind.PresentParticiple, "addressing")),
                [("arise", EnglishPartOfSpeech.Verb)] = CreateVerb("arises", "arose", "arisen", "arising"),
                [("awake", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "awakes"),
                    (EnglishFormKind.Past, "awaked"),
                    (EnglishFormKind.Past, "awoke"),
                    (EnglishFormKind.PastParticiple, "awaked"),
                    (EnglishFormKind.PastParticiple, "awoke"),
                    (EnglishFormKind.PastParticiple, "awoken"),
                    (EnglishFormKind.PresentParticiple, "awaking")),
                [("baby-sit", EnglishPartOfSpeech.Verb)] = CreateVerb("baby-sits", "baby-sat", "baby-sat", "baby-sitting"),
                [("backbite", EnglishPartOfSpeech.Verb)] = CreateVerb("backbites", "backbit", "backbitten", "backbiting"),
                [("backslide", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "backslides"),
                    (EnglishFormKind.Past, "backslid"),
                    (EnglishFormKind.PastParticiple, "backslid"),
                    (EnglishFormKind.PastParticiple, "backslidden"),
                    (EnglishFormKind.PresentParticiple, "backsliding")),
                [("bad", EnglishPartOfSpeech.Adjective)] = Create(
                    (EnglishFormKind.Comparative, "worse"),
                    (EnglishFormKind.Superlative, "worst")),
                [("bear", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "bears"),
                    (EnglishFormKind.Past, "bore"),
                    (EnglishFormKind.PastParticiple, "born"),
                    (EnglishFormKind.PastParticiple, "borne"),
                    (EnglishFormKind.PresentParticiple, "bearing")),
                [("befall", EnglishPartOfSpeech.Verb)] = CreateVerb("befalls", "befell", "befallen", "befalling"),
                [("beget", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "begets"),
                    (EnglishFormKind.Past, "begat"),
                    (EnglishFormKind.Past, "begot"),
                    (EnglishFormKind.PastParticiple, "begot"),
                    (EnglishFormKind.PastParticiple, "begotten"),
                    (EnglishFormKind.PresentParticiple, "begetting")),
                [("begin", EnglishPartOfSpeech.Verb)] = CreateVerb("begins", "began", "begun", "beginning"),
                [("behold", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "beholds"),
                    (EnglishFormKind.Past, "beheld"),
                    (EnglishFormKind.PastParticiple, "beheld"),
                    (EnglishFormKind.PastParticiple, "beholden"),
                    (EnglishFormKind.PresentParticiple, "beholding")),
                [("bend", EnglishPartOfSpeech.Verb)] = CreateVerb("bends", "bent", "bent", "bending"),
                [("beseech", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "beseeches"),
                    (EnglishFormKind.Past, "beseeched"),
                    (EnglishFormKind.Past, "besought"),
                    (EnglishFormKind.PastParticiple, "beseeched"),
                    (EnglishFormKind.PastParticiple, "besought"),
                    (EnglishFormKind.PresentParticiple, "beseeching")),
                [("bespeak", EnglishPartOfSpeech.Verb)] = CreateVerb("bespeaks", "bespoke", "bespoken", "bespeaking"),
                [("bestrew", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "bestrews"),
                    (EnglishFormKind.Past, "bestrewed"),
                    (EnglishFormKind.PastParticiple, "bestrewed"),
                    (EnglishFormKind.PastParticiple, "bestrewn"),
                    (EnglishFormKind.PresentParticiple, "bestrewing")),
                [("bestride", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "bestrides"),
                    (EnglishFormKind.Past, "bestrid"),
                    (EnglishFormKind.Past, "bestrode"),
                    (EnglishFormKind.PastParticiple, "bestridden"),
                    (EnglishFormKind.PresentParticiple, "bestriding")),
                [("bethink", EnglishPartOfSpeech.Verb)] = CreateVerb("bethinks", "bethought", "bethought", "bethinking"),
                [("bid", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "bids"),
                    (EnglishFormKind.Past, "bade"),
                    (EnglishFormKind.Past, "bid"),
                    (EnglishFormKind.PastParticiple, "bade"),
                    (EnglishFormKind.PastParticiple, "bid"),
                    (EnglishFormKind.PastParticiple, "bidden"),
                    (EnglishFormKind.PresentParticiple, "bidding")),
                [("bind", EnglishPartOfSpeech.Verb)] = CreateVerb("binds", "bound", "bound", "binding"),
                [("bite", EnglishPartOfSpeech.Verb)] = CreateVerb("bites", "bit", "bitten", "biting"),
                [("bless", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "blesses"),
                    (EnglishFormKind.Past, "blessed"),
                    (EnglishFormKind.Past, "blest"),
                    (EnglishFormKind.PastParticiple, "blessed"),
                    (EnglishFormKind.PastParticiple, "blest"),
                    (EnglishFormKind.PresentParticiple, "blessing")),
                [("blow", EnglishPartOfSpeech.Verb)] = CreateVerb("blows", "blew", "blown", "blowing"),
                [("break", EnglishPartOfSpeech.Verb)] = CreateVerb("breaks", "broke", "broken", "breaking"),
                [("bring", EnglishPartOfSpeech.Verb)] = CreateVerb("brings", "brought", "brought", "bringing"),
                [("browbeat", EnglishPartOfSpeech.Verb)] = CreateVerb("browbeats", "browbeat", "browbeaten", "browbeating"),
                [("build", EnglishPartOfSpeech.Verb)] = CreateVerb("builds", "built", "built", "building"),
                [("burn", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "burns"),
                    (EnglishFormKind.Past, "burned"),
                    (EnglishFormKind.Past, "burnt"),
                    (EnglishFormKind.PastParticiple, "burned"),
                    (EnglishFormKind.PastParticiple, "burnt"),
                    (EnglishFormKind.PresentParticiple, "burning")),
                [("buy", EnglishPartOfSpeech.Verb)] = CreateVerb("buys", "bought", "bought", "buying"),
                [("bypass", EnglishPartOfSpeech.Verb)] = Create(
                    (EnglishFormKind.ThirdPersonSingular, "bypasses"),
                    (EnglishFormKind.Past, "bypassed"),
                    (EnglishFormKind.Past, "bypast"),
                    (EnglishFormKind.PastParticiple, "bypassed"),
                    (EnglishFormKind.PastParticiple, "bypast"),
                    (EnglishFormKind.PresentParticiple, "bypassing"))
            };

        internal static bool TryGetParadigm(
            string lemma,
            EnglishPartOfSpeech partOfSpeech,
            out IReadOnlyDictionary<EnglishFormKind, IReadOnlyList<string>> paradigm)
        {
            return Paradigms.TryGetValue((lemma, partOfSpeech), out paradigm!);
        }

        internal static bool TryGetKinds(
            string lemma,
            EnglishPartOfSpeech partOfSpeech,
            string surface,
            out IReadOnlyList<EnglishFormKind> kinds)
        {
            if (TryGetParadigm(lemma, partOfSpeech, out var paradigm))
            {
                var matchedKinds = new List<EnglishFormKind>();
                foreach (var pair in paradigm)
                {
                    if (pair.Value.Contains(surface, StringComparer.Ordinal))
                    {
                        matchedKinds.Add(pair.Key);
                    }
                }

                if (matchedKinds.Count > 0)
                {
                    kinds = matchedKinds.ToArray();
                    return true;
                }
            }

            kinds = Array.Empty<EnglishFormKind>();
            return false;
        }

        private static IReadOnlyDictionary<EnglishFormKind, IReadOnlyList<string>> CreateVerb(
            string thirdPersonSingular,
            string past,
            string pastParticiple,
            string presentParticiple)
        {
            return Create(
                (EnglishFormKind.ThirdPersonSingular, thirdPersonSingular),
                (EnglishFormKind.Past, past),
                (EnglishFormKind.PastParticiple, pastParticiple),
                (EnglishFormKind.PresentParticiple, presentParticiple));
        }

        private static IReadOnlyDictionary<EnglishFormKind, IReadOnlyList<string>> Create(
            params (EnglishFormKind Kind, string Surface)[] forms)
        {
            var result = new Dictionary<EnglishFormKind, List<string>>();
            foreach (var form in forms)
            {
                if (!result.TryGetValue(form.Kind, out var surfaces))
                {
                    surfaces = new List<string>();
                    result.Add(form.Kind, surfaces);
                }

                surfaces.Add(form.Surface);
            }

            return result.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
        }
    }
}
