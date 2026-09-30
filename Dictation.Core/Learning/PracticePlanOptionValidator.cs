using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>提供学习与复习共用的纯选项校验，不读写或混用两类计划。</summary>
    internal sealed class PracticePlanOptionValidator
    {
        private static readonly HashSet<string> JapaneseForms = new(StringComparer.Ordinal) { "Dictionary", "Polite", "Te", "Ta" };
        private static readonly HashSet<string> EnglishForms = new(StringComparer.Ordinal)
        {
            "Base", "PresentFirstPersonSingular", "PresentNonThirdPerson", "ThirdPersonSingular",
            "Past", "PastParticiple", "PresentParticiple", "Plural", "Comparative", "Superlative"
        };

        /// <summary>规范化并去重当前语言的词形代码，至少保留一个可用形式。</summary>
        /// <param name="language">词形所属语言。</param>
        /// <param name="forms">用户启用的词形代码。</param>
        /// <returns>去重并保留原顺序的代码。</returns>
        internal IReadOnlyList<string> NormalizeForms(LearningLanguage language, IReadOnlyList<string> forms)
        {
            ValidateLanguage(language);
            if (forms is null || forms.Count == 0)
            {
                throw new ArgumentException("至少需要启用一个词形。", nameof(forms));
            }
            var allowed = language == LearningLanguage.Japanese ? JapaneseForms : EnglishForms;
            var normalized = forms.Select(form => NormalizeText(form, "词形标识")).Distinct(StringComparer.Ordinal).ToArray();
            if (normalized.Any(form => !allowed.Contains(form)))
            {
                throw new ArgumentException("所选词形不适用于当前语言。", nameof(forms));
            }
            return normalized;
        }

        /// <summary>验证出题渠道，拒绝空集合和未知值，并去重。</summary>
        /// <param name="channels">用户选择的出题渠道。</param>
        /// <returns>按输入顺序去重的渠道。</returns>
        internal IReadOnlyList<LearningChannel> NormalizeChannels(IReadOnlyList<LearningChannel> channels) => NormalizeEnums(channels, nameof(channels));

        /// <summary>验证字形与语言匹配：日语假名或汉字至少一项，英语仅拉丁字母。</summary>
        /// <param name="language">计划所属语言。</param>
        /// <param name="outputs">用户启用的输出字形。</param>
        /// <returns>去重并验证后的字形。</returns>
        internal IReadOnlyList<LearningOutputMode> NormalizeOutputs(LearningLanguage language, IReadOnlyList<LearningOutputMode> outputs)
        {
            ValidateLanguage(language);
            var normalized = NormalizeEnums(outputs, nameof(outputs));
            if (language == LearningLanguage.Japanese)
            {
                if (normalized.Contains(LearningOutputMode.Latin))
                {
                    throw new ArgumentException("日语只能使用假名或汉字输出。", nameof(outputs));
                }
            }
            else if (normalized.Count != 1 || normalized[0] != LearningOutputMode.Latin)
            {
                throw new ArgumentException("英语只支持拉丁字母输出。", nameof(outputs));
            }
            return normalized;
        }

        /// <summary>拒绝未知标签模式，以及在日语计划中设置英语标签。</summary>
        /// <param name="language">计划所属语言。</param>
        /// <param name="mode">英语标签模式。</param>
        /// <param name="sourceSenseId">示例动词义项；非示例模式由用例清空。</param>
        internal void ValidateLanguageLabels(LearningLanguage language, EnglishFormLabelMode mode, string? sourceSenseId)
        {
            ValidateLanguage(language);
            if (!Enum.IsDefined(mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            if (language == LearningLanguage.Japanese && (mode != EnglishFormLabelMode.GrammarName || !string.IsNullOrWhiteSpace(sourceSenseId)))
            {
                throw new ArgumentException("日语计划不能设置英语词形标签选项。", nameof(mode));
            }
        }

        /// <summary>验证语言枚举，防止未知值被当作英语处理。</summary>
        /// <param name="language">待验证的语言。</param>
        internal static void ValidateLanguage(LearningLanguage language)
        {
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }
        }

        /// <summary>拒绝未知或空枚举集合，并按原顺序去重。</summary>
        /// <param name="values">待规范化的枚举集合。</param>
        /// <param name="parameterName">用于指出非法输入的字段名。</param>
        /// <returns>去重后的有效集合。</returns>
        private static IReadOnlyList<T> NormalizeEnums<T>(IReadOnlyList<T> values, string parameterName) where T : struct, Enum
        {
            if (values is null || values.Count == 0)
            {
                throw new ArgumentException("至少需要启用一项。", parameterName);
            }
            if (values.Any(value => !Enum.IsDefined(value)))
            {
                throw new ArgumentOutOfRangeException(parameterName, "包含不受支持的选项。");
            }
            return values.Distinct().ToArray();
        }

        /// <summary>校验非空文本并规范化首尾空白和 Unicode。</summary>
        /// <param name="value">需要规范化的文本。</param>
        /// <param name="fieldName">用于错误提示的字段名称。</param>
        /// <returns>规范化后的文本。</returns>
        internal static string NormalizeText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName}不能为空。", fieldName);
            }
            return value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
