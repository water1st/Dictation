using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Practice;

namespace Dictation.DataAccess.Review
{
    /// <summary>纯转换复习联表结果与词形快照，不计算调度、评价或改变进度。</summary>
    internal static class ReviewQuestionSnapshotMapper
    {
        /// <summary>组合同一事务的主表、范围、成绩、词形和历史结果。</summary>
        /// <param name="header">复习会话概要。</param>
        /// <param name="groups">按本轮顺序排列的整组标识。</param>
        /// <param name="rows">本轮成员词形及独立成绩。</param>
        /// <param name="meanings">成员中文释义查询结果。</param>
        /// <param name="variants">成员词形与读音查询结果。</param>
        /// <param name="questions">该复习会话题目的完整历史。</param>
        internal static ReviewQuestionSnapshotPo ToPo(ReviewSessionHeaderRow header, IReadOnlyList<string> groups,
            IReadOnlyList<ReviewPracticeFormRow> rows, IReadOnlyList<WordGroupMeaningRow> meanings,
            IReadOnlyList<WordGroupFormRow> variants, IReadOnlyList<ReviewQuestionRow> questions)
        {
            var meaningLookup = meanings.ToLookup(row => row.MemberId);
            var variantLookup = variants.ToLookup(row => (row.MemberId, row.FormCode));
            var units = rows.Select(row => new PracticeFormUnitPo(row.WordGroupId, row.MemberId, row.FormCode,
                row.FormLabel, row.IsPhrase != 0, row.KanjiUsageCode,
                row.MeaningLanguage == "zh-CN" ? meaningLookup[row.MemberId].Select(value => value.Text).ToArray() : Array.Empty<string>(),
                variantLookup[(row.MemberId, row.FormCode)].Select(value => new PracticeAnswerVariantPo(value.Surface,
                    value.Reading, value.ScriptCode, value.IsCommon != 0, value.IsPrimary != 0)).ToArray(),
                row.CorrectStreak, row.IsMastered != 0, row.NextEligibleOrdinal, row.MeaningWritingCount, row.DictationCount,
                row.LatinCount, row.KanaCount, row.KanjiCount, row.CorrectCount, row.ErrorCount)).ToArray();
            var pending = questions.SingleOrDefault(row => row.Status == "Pending");
            return new ReviewQuestionSnapshotPo(ReviewSessionRowMapper.ToPo(header, groups), header.Status, header.CurrentOrdinal,
                pending is null ? null : ToQuestion(pending), units,
                questions.Select(row => new PracticeQuestionHistoryPo(row.QuestionId, row.WordGroupId, row.MemberId,
                    row.FormCode, row.Channel, row.OutputMode, row.QuestionOrdinal, row.WasCorrect is null ? null : row.WasCorrect != 0)).ToArray());
        }

        /// <summary>将题目定位列转为供两种模式纯规则使用的题目状态。</summary>
        /// <param name="row">复习题目查询结果。</param>
        internal static PracticeQuestionStatePo ToQuestion(ReviewQuestionRow row) => new(row.QuestionId,
            row.SessionId, row.WordGroupId, row.MemberId, row.FormCode, row.Channel, row.OutputMode, row.QuestionOrdinal);
    }
}
