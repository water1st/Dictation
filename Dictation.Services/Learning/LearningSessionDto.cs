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

    public sealed record LearningQuestionDto(
        long QuestionId,
        string SessionId,
        int QuestionOrdinal,
        string WordGroupId,
        LearningLanguage Language,
        LearningChannel Channel,
        LearningOutputMode OutputMode,
        string FormCode,
        string FormLabel,
        string OutputLabel,
        IReadOnlyList<string> Meanings,
        string? SpokenText);

    public sealed record SubmitLearningAnswerRequestDto(
        string SessionId,
        long QuestionId,
        string? SubmittedText);

    public sealed record LearningAnswerResultDto(
        bool WasCorrect,
        string CorrectAnswer,
        bool HadErrorInSession,
        LearningQuestionDto? NextQuestion,
        LearningSessionSummaryDto? Summary);

    public sealed record LearningSessionSummaryDto(
        string SessionId,
        int AnsweredCount,
        IReadOnlyList<LearningErrorSummaryDto> Errors);

    public sealed record LearningErrorSummaryDto(
        string WordGroupId,
        string FormCode,
        string CorrectAnswer);
}
