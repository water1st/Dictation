using Dictation.DataAccess.Importing;

namespace Dictation.DataAccess.Learning
{
    public sealed record LearningPlanPo(
        string Language,
        int DailyNewTarget,
        int SessionWordLimit,
        string SelectionMode,
        string EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<string> Channels,
        IReadOnlyList<string> OutputModes,
        DateTimeOffset UpdatedAtUtc);

    public sealed record LearningLibraryPagePo(
        int TotalCount,
        IReadOnlyList<LearningWordGroupPo> Groups);

    public sealed record LearningWordGroupPo(
        string WordGroupId,
        string Language,
        string AnchorSourceSenseId,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<ImportWordGroupMemberPo> Members);

    public enum DeleteLearningWordGroupResult
    {
        Deleted,
        NotFound,
        WrongLanguage,
        WrongPlacement
    }
}
