using Dictation.Services.Configuration;
using Dictation.Services.Importing;

namespace Dictation.Services.Learning
{
    public enum LearningSelectionMode
    {
        Random,
        Sequential,
        Manual
    }

    public enum LearningChannel
    {
        MeaningWriting,
        Dictation
    }

    public enum LearningOutputMode
    {
        Latin,
        Kana,
        Kanji
    }

    public enum EnglishFormLabelMode
    {
        GrammarName,
        ExampleParadigm
    }

    public sealed record SaveLearningPlanRequestDto(
        LearningLanguage Language,
        int DailyNewTarget,
        int SessionWordLimit,
        LearningSelectionMode SelectionMode,
        EnglishFormLabelMode EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<LearningChannel> Channels,
        IReadOnlyList<LearningOutputMode> OutputModes);

    public sealed record LearningPlanDto(
        LearningLanguage Language,
        int DailyNewTarget,
        int SessionWordLimit,
        LearningSelectionMode SelectionMode,
        EnglishFormLabelMode EnglishFormLabelMode,
        string? EnglishExampleSourceSenseId,
        IReadOnlyList<string> FormCodes,
        IReadOnlyList<LearningChannel> Channels,
        IReadOnlyList<LearningOutputMode> OutputModes,
        DateTimeOffset UpdatedAtUtc,
        EnglishExampleParadigmDto? EnglishExampleParadigm);

    public sealed record EnglishExampleParadigmDto(
        string SourceSenseId,
        string Headword,
        IReadOnlyList<EnglishExampleFormDto> Forms);

    public sealed record EnglishExampleFormDto(string FormCode, string Surface);

    public sealed record LearningLibraryPageDto(
        int PageNumber,
        int PageSize,
        int TotalCount,
        IReadOnlyList<LearningWordGroupDto> Groups);

    public sealed record LearningWordGroupDto(
        string WordGroupId,
        LearningLanguage Language,
        string AnchorSourceSenseId,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<ImportWordGroupMemberDto> Members);

    public sealed record LearningStartRequestDto(
        LearningLanguage Language,
        int SessionWordLimit,
        LearningSelectionMode SelectionMode,
        IReadOnlyList<string> SelectedWordGroupIds);
}
