namespace Dictation.DataAccess.Review
{
    /// <summary>复习会话的持久化概要与启动计划 JSON，不计算任何业务评价。</summary>
    /// <param name="SessionId">会话标识。</param>
    /// <param name="Language">持久化语言代码。</param>
    /// <param name="PlanJson">启动计划快照。</param>
    /// <param name="CreatedAtUtc">开始时间。</param>
    /// <param name="WordGroupIds">按会话顺序返回的整组标识。</param>
    public sealed record ReviewSessionPo(string SessionId, string Language, string PlanJson,
        DateTimeOffset CreatedAtUtc, IReadOnlyList<string> WordGroupIds);
}
