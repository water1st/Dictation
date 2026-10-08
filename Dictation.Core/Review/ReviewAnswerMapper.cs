using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Practice;
using Dictation.DataAccess.Practice;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>纯转换复习写入列与结算反馈，不访问数据库、计算 FSRS 或修改成绩。</summary>
    internal static class ReviewAnswerMapper
    {
        /// <summary>从已经转换的词形成绩只提取持久化所需字段。</summary>
        /// <param name="unit">业务层完成转换的词形。</param>
        internal static ReviewFormOutcomePo ToOutcome(PracticeFormUnitPo unit) => new(unit.WordGroupId, unit.MemberId,
            unit.FormCode, unit.CorrectStreak, unit.IsMastered, unit.NextEligibleOrdinal, unit.MeaningWritingCount,
            unit.DictationCount, unit.LatinCount, unit.KanaCount, unit.KanjiCount, unit.CorrectCount, unit.ErrorCount);

        /// <summary>按实际观察计题数、按整组计完成量，并投影仍需展示的本轮错误。</summary>
        /// <param name="snapshot">业务层已完成状态转换的本轮快照。</param>
        /// <param name="language">会话固定语言。</param>
        internal static ReviewSessionSummaryDto ToSummary(ReviewQuestionSnapshotPo snapshot, LearningLanguage language)
        {
            var errors = snapshot.History.Where(value => value.WasCorrect == false)
                .DistinctBy(value => (value.WordGroupId, value.MemberId, value.FormCode))
                .Select(value =>
                {
                    var unit = snapshot.Units.Single(unit => unit.MemberId == value.MemberId && unit.FormCode == value.FormCode);
                    return new ReviewErrorSummaryDto(value.WordGroupId, value.MemberId, value.FormCode,
                        PracticeQuestionPresenter.SelectPreferredAnswer(unit, Enum.Parse<LearningOutputMode>(value.OutputMode), language));
                }).ToArray();
            return new ReviewSessionSummaryDto(snapshot.Session.SessionId,
                snapshot.Units.GroupBy(unit => unit.WordGroupId).Count(group => group.All(unit => unit.IsMastered)),
                snapshot.History.Where(value => value.WasCorrect.HasValue).Select(value => value.QuestionOrdinal).Distinct().Count(), errors);
        }
    }
}
