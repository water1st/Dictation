using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>学习与复习共用的纯词形／字形适用规则，不查询数据或改变进度。</summary>
    internal static class PracticeFormApplicability
    {
        /// <summary>短语只练固定形式，不借词形选项产生虚构活用。</summary>
        /// <param name="language">练习语言。</param>
        /// <param name="isPhrase">是否为固定短语。</param>
        /// <param name="formCode">实际词形代码。</param>
        internal static bool IsFormApplicable(LearningLanguage language, bool isPhrase, string formCode) =>
            !isPhrase || formCode == (language == LearningLanguage.English ? "Base" : "Dictionary");

        /// <summary>仅允许实际存在的输出；罕见汉字按无汉字处理。</summary>
        /// <param name="language">练习语言。</param>
        /// <param name="kanjiUsageCode">日语常用／罕见／无汉字标识。</param>
        /// <param name="scriptCodes">同一形式实际可接受变体的字形代码。</param>
        /// <param name="output">计划启用的输出要求。</param>
        internal static bool HasOutput(LearningLanguage language, string? kanjiUsageCode,
            IEnumerable<string?> scriptCodes, LearningOutputMode output)
        {
            if (language == LearningLanguage.English)
            {
                return output == LearningOutputMode.Latin && scriptCodes.Any();
            }
            return output switch
            {
                LearningOutputMode.Kana => scriptCodes.Contains("Kana", StringComparer.Ordinal),
                LearningOutputMode.Kanji => kanjiUsageCode == "Common" && scriptCodes.Contains("Kanji", StringComparer.Ordinal),
                _ => false
            };
        }
    }
}
