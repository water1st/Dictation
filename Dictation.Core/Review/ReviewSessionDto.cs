using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>保留复习本轮词组顺序和启动计划快照，不借用学习进度。</summary>
    /// <param name="SessionId">可恢复的会话标识。</param>
    /// <param name="Language">会话固定语言。</param>
    /// <param name="CreatedAtUtc">开始时间。</param>
    /// <param name="WordGroupIds">本轮有序整组标识。</param>
    /// <param name="PlanSnapshot">本轮生效的计划与例词范式。</param>
    public sealed record ReviewSessionDto(string SessionId, LearningLanguage Language, DateTimeOffset CreatedAtUtc,
        IReadOnlyList<string> WordGroupIds, ReviewPlanDto PlanSnapshot);
}
