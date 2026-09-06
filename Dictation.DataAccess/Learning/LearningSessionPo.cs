using Dictation.DataAccess.Importing;

namespace Dictation.DataAccess.Learning
{
    public sealed record LearningSessionPo(
        string SessionId,
        string Language,
        string Status,
        int CurrentOrdinal,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<string> WordGroupIds);

    public sealed record LearningSessionCandidatePo(
        string WordGroupId,
        DateTimeOffset CreatedAtUtc,
        bool HasProgress,
        IReadOnlyList<LearningFormIdentityPo> AvailableForms,
        IReadOnlyList<LearningFormStateSummaryPo> FormStates);

    public sealed record LearningFormIdentityPo(long MemberId, string FormCode);

    public sealed record LearningFormStateSummaryPo(long MemberId, string FormCode, bool IsMastered);

    public sealed record LearningSessionGroupSeedPo(
        string WordGroupId,
        IReadOnlyList<LearningFormIdentityPo> Forms);

    public sealed record CreateLearningSessionPo(
        string SessionId,
        string Language,
        string Status,
        string StartedLocalDate,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<LearningSessionGroupSeedPo> Groups);

    public sealed record LearningBrowseItemPo(
        int SortOrder,
        string WordGroupId,
        IReadOnlyList<ImportWordGroupMemberPo> Members);
}
