using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Practice;

namespace Dictation.Core.Practice
{
    /// <summary>仅合并本轮完全相同的听写观察，不合并不同义项的看义写词题。</summary>
    internal static class PracticeDictationTargets
    {
        /// <summary>选择与当前题面、发音及可接受答案完全一致的未完成目标。</summary>
        /// <param name="current">当前题目对应的词形。</param>
        /// <param name="question">当前渠道及输出字形。</param>
        /// <param name="units">本轮独立词形成绩。</param>
        /// <param name="language">本轮固定语言。</param>
        /// <param name="plan">本轮提示与范围选项。</param>
        internal static IReadOnlyList<PracticeFormUnitPo> Select(PracticeFormUnitPo current, PracticeQuestionStatePo question,
            IReadOnlyList<PracticeFormUnitPo> units, LearningLanguage language, IPracticeQuestionPlan plan)
        {
            if (question.Channel != nameof(LearningChannel.Dictation))
            {
                return new[] { current };
            }
            var output = Enum.Parse<LearningOutputMode>(question.OutputMode);
            var comparison = language == LearningLanguage.English ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var answers = PracticeQuestionPresenter.GetAnswers(current, output, language).Order(comparison).ToArray();
            var spoken = PracticeQuestionPresenter.SelectPreferredSpokenText(current, output, language);
            var label = PracticeQuestionPresenter.GetFormLabel(current, plan, language);
            return units.Where(unit => !unit.IsMastered
                && PracticeFormApplicability.HasOutput(language, unit.KanjiUsageCode, unit.Variants.Select(variant => variant.ScriptCode), output)
                && PracticeQuestionPresenter.GetFormLabel(unit, plan, language) == label
                && comparison.Equals(PracticeQuestionPresenter.SelectPreferredSpokenText(unit, output, language), spoken)
                && PracticeQuestionPresenter.GetAnswers(unit, output, language).Order(comparison).SequenceEqual(answers, comparison))
                .ToArray();
        }
    }
}
