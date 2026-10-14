namespace Dictation.Core.Kana
{
    /// <summary>描述已持久化轮次与数量，不混入词汇计数。</summary>
    /// <param name="SessionId">轮次标识。</param>
    /// <param name="TotalCount">本轮独立字形项目数。</param>
    /// <param name="CompletedCount">已经完成的项目数。</param>
    /// <param name="StartedLocalDate">首次启动的本地日期，不替代实际完成日期。</param>
    public sealed record KanaSessionDto(string SessionId, int TotalCount, int CompletedCount, string StartedLocalDate);
}
