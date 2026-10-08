using Dictation.Core.Learning;
using Dictation.DataAccess.Practice;

namespace Dictation.Core.Practice
{
    /// <summary>复用客观结果的纯词形成绩转换，不修改输入对象或访问数据库。</summary>
    internal static class PracticeAnswerTransition
    {
        /// <summary>
        /// 根据当前题型、输出方式与判题结果生成新的词形状态；不就地修改输入快照。
        /// </summary>
        /// <param name="unit">该模式当前词形成绩。</param>
        /// <param name="question">实际作答的题型及字形。</param>
        /// <param name="wasCorrect">客观判定结果。</param>
        /// <param name="correctStreak">业务层计算的连续正确次数。</param>
        /// <param name="nextEligibleOrdinal">完成穿插后的常规重练序号。</param>
        /// <param name="hasUnresolvedRetry">是否仍有未纠正的精确题型／字形错误。</param>
        internal static PracticeFormUnitPo Apply(
            PracticeFormUnitPo unit,
            PracticeQuestionStatePo question,
            bool wasCorrect,
            int correctStreak,
            int nextEligibleOrdinal,
            bool hasUnresolvedRetry)
        {
            return unit with
            {
                CorrectStreak = correctStreak,
                IsMastered = correctStreak == 2 && !hasUnresolvedRetry,
                NextEligibleOrdinal = nextEligibleOrdinal,
                MeaningWritingCount = unit.MeaningWritingCount + (question.Channel == LearningChannel.MeaningWriting.ToString() ? 1 : 0),
                DictationCount = unit.DictationCount + (question.Channel == LearningChannel.Dictation.ToString() ? 1 : 0),
                LatinCount = unit.LatinCount + (question.OutputMode == LearningOutputMode.Latin.ToString() ? 1 : 0),
                KanaCount = unit.KanaCount + (question.OutputMode == LearningOutputMode.Kana.ToString() ? 1 : 0),
                KanjiCount = unit.KanjiCount + (question.OutputMode == LearningOutputMode.Kanji.ToString() ? 1 : 0),
                CorrectCount = unit.CorrectCount + (wasCorrect ? 1 : 0),
                ErrorCount = unit.ErrorCount + (wasCorrect ? 0 : 1)
            };
        }

    }
}
