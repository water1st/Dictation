using Dictation.Core.Configuration;
using Dictation.DataAccess.Lexicon;

namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 将日语词库持久化结果纯转换为查询结果。
    /// </summary>
    internal static class JapaneseLexiconMapper
    {
        /// <summary>
        /// 保留稳定代码并生成日语展示标签。
        /// </summary>
        /// <param name="value">日语词库持久化结果。</param>
        /// <returns>可供业务与界面使用的查询结果。</returns>
        internal static LexiconLookupResultDto Map(JapaneseLexiconLookupPo value)
        {
            return new LexiconLookupResultDto(
                LearningLanguage.Japanese,
                value.EntryId,
                value.EntryId,
                value.PrimaryWritten,
                value.PrimaryReading,
                MapCategory(value.LexicalCategory),
                MapAttributes(value),
                value.IsPhrase,
                value.Meanings,
                value.Forms.Select(form => new LexiconLookupFormDto(
                    form.Surface,
                    form.Reading,
                    form.FormKind,
                    form.ScriptKind,
                    form.LexicalRole,
                    MapFormKind(form.FormKind),
                    MapFormDetail(form),
                    form.IsCommon,
                    form.IsPrimary)).ToArray(),
                LexiconMeaningMetadataMapper.ParseSource(value.MeaningSource),
                LexiconMeaningMetadataMapper.ParsePrecision(value.MeaningPrecision),
                value.KanjiUsage);
        }

        private static string MapCategory(string category)
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

        private static string MapAttributes(JapaneseLexiconLookupPo value)
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

        private static string MapFormKind(string formKind)
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

        private static string MapFormDetail(JapaneseLexiconLookupFormPo form)
        {
            return $"{MapScriptKind(form.ScriptKind)} · {MapLexicalRole(form.LexicalRole)}";
        }

        private static string MapScriptKind(string scriptKind)
        {
            return scriptKind switch
            {
                "Kanji" => "汉字",
                "Kana" => "假名",
                _ => "其他文字"
            };
        }

        private static string MapLexicalRole(string lexicalRole)
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
    }
}
