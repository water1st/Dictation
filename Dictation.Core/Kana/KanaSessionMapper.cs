using Dictation.Core.Review;
using Dictation.DataAccess.Kana;
using System.Globalization;

namespace Dictation.Core.Kana
{
    /// <summary>只转换会话、语言事实和FSRS列，不编排作答或访问数据库。</summary>
    internal static class KanaSessionMapper
    {
        /// <summary>把保存的轮次和项目数量映射到公开快照。</summary>
        /// <param name="session">保存的轮次。</param>
        /// <param name="items">该轮次的独立项目。</param>
        /// <returns>只读轮次数量。</returns>
        internal static KanaSessionDto ToDto(KanaSessionPo session, IReadOnlyList<KanaSessionItemPo> items) =>
            new(session.SessionId, items.Count, items.Count(item => item.IsCompleted), session.StartedLocalDate);

        /// <summary>转换项目快照为不显示答案的听写题。</summary>
        /// <param name="sessionId">轮次标识。</param>
        /// <param name="ordinal">当前题序。</param>
        /// <param name="item">当前项目事实快照。</param>
        /// <returns>字形、音类、必要行提示和仅供语音的文本。</returns>
        internal static KanaQuestionDto ToQuestion(string sessionId, int ordinal, KanaSessionItemPo item) => new(
            sessionId, ordinal, item.KanaId, Enum.Parse<KanaScript>(item.Script), Enum.Parse<KanaRange>(item.Range), item.RowHint, item.Reading);

        /// <summary>将静态事实保存为本轮不可变事实快照和初始状态。</summary>
        /// <param name="sessionId">新轮次标识。</param>
        /// <param name="symbol">原始假名事实。</param>
        /// <param name="order">已确定的本轮随机题序。</param>
        /// <returns>新项目列。</returns>
        internal static KanaSessionItemPo ToItem(string sessionId, KanaSymbol symbol, int order) => new()
        {
            SessionId = sessionId, KanaId = symbol.Id, Text = symbol.Text, Reading = symbol.Reading,
            RowHint = symbol.RowHint, Script = symbol.Script.ToString(), Range = symbol.Range.ToString(), QueueOrder = order
        };

        /// <summary>解析UTC记忆列为原有FSRS纯状态。</summary>
        /// <param name="memory">已保存列，为空表示首次评价。</param>
        /// <returns>原FSRS状态或空。</returns>
        internal static FsrsMemoryState? ToState(KanaMemoryPo? memory) => memory is null ? null : new(
            memory.Stability, memory.Difficulty, DateTimeOffset.Parse(memory.LastReviewedAtUtc, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(memory.DueAtUtc, CultureInfo.InvariantCulture), memory.ReviewCount, memory.LapseCount);

        /// <summary>转换原FSRS结果为独立五十音记忆列。</summary>
        /// <param name="kanaId">字形独立项目标识。</param>
        /// <param name="state">原算法计算结果。</param>
        /// <returns>可原子保存的记忆列。</returns>
        internal static KanaMemoryPo ToMemory(string kanaId, FsrsMemoryState state) => new()
        {
            KanaId = kanaId, Stability = state.Stability, Difficulty = state.Difficulty,
            LastReviewedAtUtc = state.LastReviewedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            DueAtUtc = state.DueAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ReviewCount = state.ReviewCount, LapseCount = state.LapseCount
        };

        /// <summary>转换已保存错误为本轮结算，不过滤已正确重试的错误。</summary>
        /// <param name="attempts">原始错误观察。</param>
        /// <returns>原错误题序、提交和正确答案。</returns>
        internal static IReadOnlyList<KanaErrorDto> ToErrors(IReadOnlyList<KanaAttemptPo> attempts) =>
            attempts.Select(item => new KanaErrorDto(item.QuestionOrdinal, item.KanaId, item.SubmittedText, item.CorrectAnswer)).ToArray();

        /// <summary>保存本次真实输入，不因规范化比较而修改原审计文本。</summary>
        /// <param name="sessionId">轮次标识。</param>
        /// <param name="ordinal">本次题序。</param>
        /// <param name="kanaId">对应独立项目。</param>
        /// <param name="submitted">用户原始输入。</param>
        /// <param name="correct">客观比较结果。</param>
        /// <param name="at">作答UTC时间。</param>
        /// <returns>本次观察列。</returns>
        internal static KanaAttemptPo ToAttempt(string sessionId, int ordinal, string kanaId, string? submitted, bool correct, DateTimeOffset at) =>
            new() { SessionId = sessionId, QuestionOrdinal = ordinal, KanaId = kanaId, SubmittedText = submitted ?? "",
                WasCorrect = correct, AnsweredAtUtc = at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
    }
}
