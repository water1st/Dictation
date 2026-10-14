using Dictation.Core.Configuration;
using Dictation.DataAccess.Lexicon;

namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 将英语词库持久化结果纯转换为查询结果。
    /// </summary>
    internal static class EnglishLexiconMapper
    {
        /// <summary>
        /// 保留稳定代码并生成英语展示标签。
        /// </summary>
        /// <param name="value">英语词库持久化结果。</param>
        /// <returns>可供业务与界面使用的查询结果。</returns>
        internal static LexiconLookupResultDto Map(EnglishLexiconLookupPo value)
        {
            var partOfSpeechLabel = MapPartOfSpeech(value.PartOfSpeech);
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
                    MapFormKind(form.FormKind),
                    MapFormSourceKind(form.FormSourceKind),
                    true,
                    string.Equals(form.FormKind, "Base", StringComparison.Ordinal))).ToArray(),
                LexiconMeaningMetadataMapper.ParseSource(value.MeaningSource),
                LexiconMeaningMetadataMapper.ParsePrecision(value.MeaningPrecision));
        }

        private static string MapPartOfSpeech(string partOfSpeech)
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

        private static string MapFormKind(string formKind)
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

        private static string MapFormSourceKind(string formSourceKind)
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
