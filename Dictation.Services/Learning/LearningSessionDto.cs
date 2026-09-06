using Dictation.Services.Configuration;
using Dictation.Services.Importing;

namespace Dictation.Services.Learning
{
    public enum LearningSessionStatus
    {
        InProgress,
        Completed
    }

    public sealed record StartLearningSessionRequestDto(
        LearningLanguage Language,
        IReadOnlyList<string>? SelectedWordGroupIds = null);

    public sealed record LearningSessionDto(
        string SessionId,
        LearningLanguage Language,
        LearningSessionStatus Status,
        int CurrentOrdinal,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<string> WordGroupIds);

    public sealed record LearningBrowseItemDto(
        int SortOrder,
        string WordGroupId,
        IReadOnlyList<ImportWordGroupMemberDto> Members);
}
