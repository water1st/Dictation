using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Practice;

namespace Dictation.Core.Practice
{
    /// <summary>集中学习与复习共用的题面提示和答案字形选择，保持看义写词的答案隐藏边界。</summary>
    internal static class PracticeQuestionPresenter
    {
        /// <summary>
        /// 按计划选择提示标签与发音字段并组装题目；看义写词在提交前不携带发音答案，因此不是无条件字段复制。
        /// </summary>
        /// <param name="language">会话固定的目标语言。</param>
        /// <param name="units">该会话自己的成绩与词形快照。</param>
        /// <param name="question">已持久化的当前题目定位。</param>
        /// <param name="plan">当前模式生效的提示选项。</param>
        /// <returns>隐藏看义写词答案的共用题面。</returns>
        internal static PracticeQuestionDto Create(
            LearningLanguage language,
            IReadOnlyList<PracticeFormUnitPo> units,
            PracticeQuestionStatePo question,
            IPracticeQuestionPlan plan)
        {
            var channel = ParseEnum<LearningChannel>(question.Channel, "题型");
            var output = ParseEnum<LearningOutputMode>(question.OutputMode, "输出方式");
            var unit = units.SingleOrDefault(value => value.MemberId == question.MemberId && value.FormCode == question.FormCode)
                ?? throw new InvalidDataException("题目对应的词形状态不存在。");
            return new PracticeQuestionDto(
                question.QuestionId,
                question.SessionId,
                question.QuestionOrdinal,
                question.WordGroupId,
                language,
                channel,
                output,
                question.FormCode,
                GetFormLabel(unit, plan, language),
                GetOutputLabel(output),
                unit.Meanings,
                channel == LearningChannel.Dictation ? SelectPreferredSpokenText(unit, output, language) : null);
        }

        /// <summary>
        /// 按计划选择英语示例词形标签；没有对应示例时使用原语法标签。
        /// </summary>
        internal static string GetFormLabel(
            PracticeFormUnitPo unit,
            IPracticeQuestionPlan plan,
            LearningLanguage language)
        {
            if (language == LearningLanguage.English
                && plan.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                && plan.EnglishExampleParadigm is not null)
            {
                var example = plan.EnglishExampleParadigm.Forms.FirstOrDefault(form =>
                    string.Equals(form.FormCode, unit.FormCode, StringComparison.Ordinal));
                if (example is not null)
                {
                    return example.Surface;
                }
            }

            return unit.FormLabel;
        }

        private static string GetOutputLabel(LearningOutputMode output) => output switch
        {
            LearningOutputMode.Latin => "英文",
            LearningOutputMode.Kana => "假名",
            LearningOutputMode.Kanji => "汉字",
            _ => throw new ArgumentOutOfRangeException(nameof(output))
        };

        /// <summary>
        /// 按语言与输出字形筛选可接受答案并去重；候选为空表示持久化状态不完整。
        /// </summary>
        internal static IReadOnlyList<string> GetAnswers(
            PracticeFormUnitPo unit,
            LearningOutputMode output,
            LearningLanguage language)
        {
            var answers = unit.Variants
                .Where(variant => language == LearningLanguage.English
                    ? output == LearningOutputMode.Latin
                    : string.Equals(variant.ScriptCode, output.ToString(), StringComparison.Ordinal))
                .Select(variant => variant.Surface)
                .Distinct(language == LearningLanguage.English ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .ToArray();
            return answers.Length > 0
                ? answers
                : throw new InvalidDataException("练习题目没有可接受答案。");
        }

        internal static string SelectPreferredAnswer(
            PracticeFormUnitPo unit,
            LearningOutputMode output,
            LearningLanguage language) => GetAnswers(unit, output, language)[0];

        /// <summary>
        /// 先选当前输出方式的首个答案；日语优先读音，英语直接使用表面形式。
        /// </summary>
        internal static string SelectPreferredSpokenText(
            PracticeFormUnitPo unit,
            LearningOutputMode output,
            LearningLanguage language)
        {
            var answer = SelectPreferredAnswer(unit, output, language);
            if (language == LearningLanguage.English)
            {
                return answer;
            }

            var variant = unit.Variants.First(value => value.Surface == answer
                && string.Equals(value.ScriptCode, output.ToString(), StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(variant.Reading) ? answer : variant.Reading;
        }

        /// <summary>拒绝不在合同中的持久化题型或字形代码。</summary>
        /// <param name="value">数据库中的稳定枚举名称。</param>
        /// <param name="fieldName">用于诊断的字段说明。</param>
        private static T ParseEnum<T>(string value, string fieldName) where T : struct, Enum =>
            Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
                ? parsed : throw new InvalidDataException($"数据库中的{fieldName}无效：{value}。");
    }
}
