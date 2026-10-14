using Dictation.Core.Learning;
using Dictation.DataAccess.Mistakes;
using System.Globalization;

namespace Dictation.Core.Mistakes
{
    /// <summary>纯转换历史联表行为独立义项词形及详情，不查询或修改任何状态。</summary>
    internal static class WrongAnswerMapper
    {
        /// <summary>归组累计错误并优先展示错误较多的形式。</summary>
        /// <param name="rows">语言已隔离的错误观察。</param>
        /// <returns>累计次数降序、同级最近错误优先的列表。</returns>
        internal static IReadOnlyList<WrongAnswerItemDto> ToDtos(IReadOnlyList<WrongAnswerObservationPo> rows) => rows
            .GroupBy(row => (row.WordGroupId, row.SourceSenseId, row.FormCode))
            .Select(group =>
            {
                var row = group.First();
                return new WrongAnswerItemDto(row.WordGroupId, row.SourceSenseId, row.FormCode, row.Headword,
                    row.FormLabel, row.Meanings, row.Placement, row.DueAtUtc is null ? null : ParseTime(row.DueAtUtc),
                    group.Select(value => new WrongAnswerAttemptDto(value.Mode,
                        Enum.Parse<LearningChannel>(value.Channel), Enum.Parse<LearningOutputMode>(value.OutputMode),
                        value.SubmittedText, ParseTime(value.AnsweredAtUtc), value.ExpectedAnswer, value.ExpectedSpokenText))
                        .OrderByDescending(value => value.AnsweredAtUtc).ToArray());
            })
            .OrderByDescending(item => item.Attempts.Count)
            .ThenByDescending(item => item.Attempts[0].AnsweredAtUtc)
            .ThenBy(item => item.WordGroupId, StringComparer.Ordinal).ThenBy(item => item.SourceSenseId, StringComparer.Ordinal)
            .ThenBy(item => item.FormCode, StringComparer.Ordinal).ToArray();

        private static DateTimeOffset ParseTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }
}
