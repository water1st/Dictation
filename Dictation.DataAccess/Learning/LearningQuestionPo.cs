namespace Dictation.DataAccess.Learning
{
    public sealed record LearningSchedulingSnapshotPo(
        LearningSessionPo Session,
        LearningQuestionStatePo? PendingQuestion,
        IReadOnlyList<LearningFormUnitPo> Units,
        IReadOnlyList<LearningQuestionHistoryPo> History);

    public sealed record LearningQuestionStatePo(
        long QuestionId,
        string SessionId,
        string WordGroupId,
        long MemberId,
        string FormCode,
        string Channel,
        string OutputMode,
        int QuestionOrdinal);

    public sealed record LearningQuestionHistoryPo(
        long QuestionId,
        string WordGroupId,
        long MemberId,
        string FormCode,
        string Channel,
        string OutputMode,
        int QuestionOrdinal,
        bool? WasCorrect);

    public sealed record LearningFormUnitPo(
        string WordGroupId,
        long MemberId,
        string FormCode,
        string FormLabel,
        bool IsPhrase,
        string? KanjiUsageCode,
        IReadOnlyList<string> Meanings,
        IReadOnlyList<LearningAnswerVariantPo> Variants,
        int CorrectStreak,
        bool IsMastered,
        int NextEligibleOrdinal,
        int MeaningWritingCount,
        int DictationCount,
        int LatinCount,
        int KanaCount,
        int KanjiCount,
        int CorrectCount,
        int ErrorCount);

    public sealed record LearningAnswerVariantPo(
        string Surface,
        string? Reading,
        string? ScriptCode,
        bool IsCommon,
        bool IsPrimary);

    public sealed record CreateLearningQuestionPo(
        string SessionId,
        string WordGroupId,
        long MemberId,
        string FormCode,
        string Channel,
        string OutputMode,
        int QuestionOrdinal,
        DateTimeOffset CreatedAtUtc);

    public sealed record ApplyLearningAnswerPo(
        string SessionId,
        long QuestionId,
        string SubmittedText,
        bool WasCorrect,
        int CorrectStreak,
        bool IsMastered,
        int NextEligibleOrdinal,
        DateTimeOffset AnsweredAtUtc,
        string AnsweredLocalDate,
        CreateLearningQuestionPo? NextQuestion,
        IReadOnlyList<string> CompletedWordGroupIds,
        bool CompleteSession);
}
