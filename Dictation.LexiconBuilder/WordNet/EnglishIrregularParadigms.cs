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
                [("say", EnglishPartOfSpeech.Verb)] = CreateVerb("says", "said", "said", "saying")
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
