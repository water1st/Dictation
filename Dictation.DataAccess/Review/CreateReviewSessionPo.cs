namespace Dictation.DataAccess.Review
{
    /// <summary>在一个事务内创建本轮复习会话、整组及成员形式的写入输入。</summary>
    /// <param name="SessionId">新会话标识。</param>
    /// <param name="Language">持久化语言代码。</param>
    /// <param name="PlanJson">启动计划快照 JSON。</param>
    /// <param name="CreatedAtUtc">创建 UTC 时间。</param>
    /// <param name="LocalDate">开始的本地日期。</param>
    /// <param name="Groups">已由业务层按风险排序的整组与形式。</param>
    public sealed record CreateReviewSessionPo(string SessionId, string Language, string PlanJson,
        DateTimeOffset CreatedAtUtc, string LocalDate, IReadOnlyList<ReviewSessionGroupSeedPo> Groups);
}
