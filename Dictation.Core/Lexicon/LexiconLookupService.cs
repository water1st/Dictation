using Dictation.DataAccess.Lexicon;
using Dictation.Core.Configuration;
using System.Text;

namespace Dictation.Core.Lexicon
{
    public sealed class LexiconLookupService
    {
        private readonly JapaneseLexiconDataAccessor japaneseAccessor;
        private readonly EnglishLexiconDataAccessor englishAccessor;

        public LexiconLookupService(
            JapaneseLexiconDataAccessor japaneseAccessor,
            EnglishLexiconDataAccessor englishAccessor)
        {
            this.japaneseAccessor = japaneseAccessor
                ?? throw new ArgumentNullException(nameof(japaneseAccessor));
            this.englishAccessor = englishAccessor
                ?? throw new ArgumentNullException(nameof(englishAccessor));
        }

        public async Task<IReadOnlyList<LexiconLookupResultDto>> SearchAsync(
            LexiconSearchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var normalizedQuery = NormalizeQuery(request.Language, request.Query);
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.Limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Limit, 50);
            cancellationToken.ThrowIfCancellationRequested();

            return request.Language switch
            {
                LearningLanguage.Japanese => (await japaneseAccessor.SearchAsync(
                    normalizedQuery,
                    request.Limit,
                    cancellationToken)).Select(MapJapanese).ToArray(),
                LearningLanguage.English => (await englishAccessor.SearchAsync(
                    normalizedQuery,
                    request.Limit,
                    cancellationToken)).Select(MapEnglish).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Language), request.Language, "不支持的学习语言。")
            };
        }

        public async Task<IReadOnlyList<LexiconLookupResultDto>> GetImportGroupAsync(
            LearningLanguage language,
            string sourceSenseId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceSenseId);
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(language),
                    language,
                    "不支持的学习语言。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var normalizedSourceSenseId = sourceSenseId.Trim().Normalize(NormalizationForm.FormC);
            return language switch
            {
                LearningLanguage.Japanese => await MapJapaneseImportGroupAsync(
                    normalizedSourceSenseId,
                    cancellationToken),
                LearningLanguage.English => (await englishAccessor.GetSenseGroupAsync(
                    normalizedSourceSenseId,
                    cancellationToken)).Select(MapEnglish).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "不支持的学习语言。")
            };
        }

        private static string NormalizeQuery(LearningLanguage language, string query)
        {
            ArgumentNullException.ThrowIfNull(query);
            var normalized = query.Trim().Normalize(NormalizationForm.FormC);
            return language switch
            {
                LearningLanguage.Japanese => normalized,
                LearningLanguage.English => normalized.ToLowerInvariant(),
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "不支持的学习语言。")
            };
        }

        private static LexiconLookupResultDto MapJapanese(JapaneseLexiconLookupPo value)
        {
            return new LexiconLookupResultDto(
                LearningLanguage.Japanese,
                value.EntryId,
                value.EntryId,
                value.PrimaryWritten,
                value.PrimaryReading,
                MapJapaneseCategory(value.LexicalCategory),
                MapJapaneseAttributes(value),
                value.IsPhrase,
                value.Meanings,
                value.Forms.Select(form => new LexiconLookupFormDto(
                    form.Surface,
                    form.Reading,
                    form.FormKind,
                    form.ScriptKind,
                    form.LexicalRole,
                    MapJapaneseFormKind(form.FormKind),
                    MapJapaneseFormDetail(form),
                    form.IsCommon,
                    form.IsPrimary)).ToArray(),
                ParseMeaningSource(value.MeaningSource),
                ParseMeaningPrecision(value.MeaningPrecision),
                value.KanjiUsage);
        }

        private static LexiconLookupResultDto MapEnglish(EnglishLexiconLookupPo value)
        {
            var partOfSpeechLabel = MapEnglishPartOfSpeech(value.PartOfSpeech);
            return new LexiconLookupResultDto(
                LearningLanguage.English,
                value.LearningSenseId,
                value.SourceLexemeId,
                value.Headword,
                null,
                partOfSpeechLabel,
                partOfSpeechLabel,
                value.IsPhrase,
                value.Meanings,
                value.Forms.Select(form => new LexiconLookupFormDto(
                    form.Surface,
                    null,
                    form.FormKind,
                    null,
                    null,
                    MapEnglishFormKind(form.FormKind),
                    MapEnglishFormSourceKind(form.FormSourceKind),
                    true,
                    string.Equals(form.FormKind, "Base", StringComparison.Ordinal))).ToArray(),
                ParseMeaningSource(value.MeaningSource),
                ParseMeaningPrecision(value.MeaningPrecision));
        }

        private static LexiconMeaningSource ParseMeaningSource(string value)
        {
            return Enum.TryParse<LexiconMeaningSource>(value, out var source)
                ? source
                : throw CreateInvalidMeaningMetadataException("来源", value);
        }

        private static LexiconMeaningPrecision ParseMeaningPrecision(string value)
        {
            return Enum.TryParse<LexiconMeaningPrecision>(value, out var precision)
                ? precision
                : throw CreateInvalidMeaningMetadataException("精度", value);
        }

        private static LexiconDataAccessException CreateInvalidMeaningMetadataException(
            string fieldName,
            string value)
        {
            var reason = new InvalidDataException($"不支持的中文释义{fieldName}：{value}。");
            return new LexiconDataAccessException("词库包含无效的中文释义元数据。", reason);
        }

        private async Task<IReadOnlyList<LexiconLookupResultDto>> MapJapaneseImportGroupAsync(
            string sourceSenseId,
            CancellationToken cancellationToken)
        {
            var value = await japaneseAccessor.GetByEntryIdAsync(sourceSenseId, cancellationToken);
            return value is null
                ? Array.Empty<LexiconLookupResultDto>()
                : new[] { MapJapanese(value) };
        }

        private static string MapJapaneseCategory(string category)
        {
            return category switch
            {
                "Phrase" => "短语",
                "NounVerb" => "名词/动词",
                "Verb" => "动词",
                "IAdjective" => "い形容词",
                "NaAdjective" => "な形容词",
                "Adverb" => "副词",
                "Noun" => "名词",
                _ => "其他"
            };
        }

        private static string MapJapaneseAttributes(JapaneseLexiconLookupPo value)
        {
            var labels = new List<string>();
            if (value.VerbClass is not "None" and not "")
            {
                labels.Add(value.VerbClass switch
                {
                    "Ichidan" => "二类动词（一段）",
                    "GodanU" => "一类动词（五段・う）",
                    "GodanK" => "一类动词（五段・く）",
                    "GodanG" => "一类动词（五段・ぐ）",
                    "GodanS" => "一类动词（五段・す）",
                    "GodanT" => "一类动词（五段・つ）",
                    "GodanN" => "一类动词（五段・ぬ）",
                    "GodanB" => "一类动词（五段・ぶ）",
                    "GodanM" => "一类动词（五段・む）",
                    "GodanR" => "一类动词（五段・る）",
                    "Iku" => "一类动词（行く特殊）",
                    "USpecial" => "一类动词（う特殊）",
                    "Suru" => "三类动词（する）",
                    "Zuru" => "三类动词（ずる）",
                    "Aru" => "一类动词（ある特殊）",
                    "Kuru" => "三类动词（来る）",
                    _ => value.VerbClass
                });
            }

            if (!string.IsNullOrWhiteSpace(value.Transitivity))
            {
                labels.Add(value.Transitivity switch
                {
                    "Transitive" => "他动词",
                    "Intransitive" => "自动词",
                    "Transitive;Intransitive" => "自动词/他动词",
                    _ => value.Transitivity
                });
            }

            labels.Add(value.KanjiUsage switch
            {
                "None" => "无汉字",
                "Common" => "常用汉字",
                "Rare" => "罕见汉字",
                _ => value.KanjiUsage
            });

            return labels.Count == 0 ? "其他" : string.Join("；", labels);
        }

        private static string MapJapaneseFormKind(string formKind)
        {
            return formKind switch
            {
                "Dictionary" => "普通形",
                "Polite" => "礼貌形",
                "Te" => "て形",
                "Ta" => "た形",
                _ => "其他词形"
            };
        }

        private static string MapJapaneseScriptKind(string scriptKind)
        {
            return scriptKind switch
            {
                "Kanji" => "汉字",
                "Kana" => "假名",
                _ => "其他文字"
            };
        }

        private static string MapJapaneseFormDetail(JapaneseLexiconLookupFormPo form)
        {
            return $"{MapJapaneseScriptKind(form.ScriptKind)} · {MapJapaneseLexicalRole(form.LexicalRole)}";
        }

        private static string MapJapaneseLexicalRole(string lexicalRole)
        {
            return lexicalRole switch
            {
                "Noun" => "名词",
                "Verb" => "动词",
                "Adjective" => "形容词",
                "Adverb" => "副词",
                "Other" => "其他",
                _ => lexicalRole
            };
        }

        private static string MapEnglishPartOfSpeech(string partOfSpeech)
        {
            return partOfSpeech switch
            {
                "n" => "名词",
                "v" => "动词",
                "a" or "s" => "形容词",
                "r" => "副词",
                _ => "其他"
            };
        }

        private static string MapEnglishFormKind(string formKind)
        {
            return formKind switch
            {
                "Base" => "原形",
                "PresentFirstPersonSingular" => "第一人称单数",
                "PresentNonThirdPerson" => "非第三人称现在式",
                "ThirdPersonSingular" => "第三人称单数",
                "Past" => "过去式",
                "PastParticiple" => "过去分词",
                "PresentParticiple" => "现在分词",
                "Plural" => "复数",
                "Comparative" => "比较级",
                "Superlative" => "最高级",
                _ => "其他词形"
            };
        }

        private static string MapEnglishFormSourceKind(string formSourceKind)
        {
            return formSourceKind switch
            {
                "Source" => "词库来源",
                "Rule" => "规则生成",
                _ => "词形来源"
            };
        }
    }
}
