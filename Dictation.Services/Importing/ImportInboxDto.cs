using Dictation.Services.Configuration;

namespace Dictation.Services.Importing
{
    public enum ImportSourceKind
    {
        Manual,
        Text,
        Mcp
    }

    public enum ImportDecision
    {
        Learning,
        Review,
        Discard
    }

    public enum ImportBatchStatus
    {
        Pending,
        Completed
    }

    public enum ImportItemDecision
    {
        Pending,
        Duplicate,
        Learning,
        Review,
        Discard
    }

    public enum ImportWordGroupPlacement
    {
        Inbox,
        Learning,
        Review
    }

    public sealed record ImportSelectionDto(
        string SourceText,
        string SourceSenseId);

    public sealed record StageImportBatchRequestDto(
        LearningLanguage Language,
        ImportSourceKind SourceKind,
        IReadOnlyList<ImportSelectionDto> Selections);

    public sealed record DecideImportBatchRequestDto(
        string BatchId,
        IReadOnlyList<long> ItemIds,
        ImportDecision Decision);

    public class ImportBatchDto
    {
        public ImportBatchDto(
            string batchId,
            LearningLanguage language,
            ImportSourceKind sourceKind,
            ImportBatchStatus status,
            DateTimeOffset createdAtUtc,
            DateTimeOffset? completedAtUtc,
            IReadOnlyList<ImportBatchItemDto> items)
        {
            BatchId = batchId;
            Language = language;
            SourceKind = sourceKind;
            Status = status;
            CreatedAtUtc = createdAtUtc;
            CompletedAtUtc = completedAtUtc;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public string BatchId { get; }

        public LearningLanguage Language { get; }

        public ImportSourceKind SourceKind { get; }

        public ImportBatchStatus Status { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public DateTimeOffset? CompletedAtUtc { get; }

        public IReadOnlyList<ImportBatchItemDto> Items { get; }
    }

    public sealed class StageImportBatchResultDto : ImportBatchDto
    {
        public StageImportBatchResultDto(
            string batchId,
            LearningLanguage language,
            ImportSourceKind sourceKind,
            ImportBatchStatus status,
            DateTimeOffset createdAtUtc,
            DateTimeOffset? completedAtUtc,
            IReadOnlyList<ImportBatchItemDto> items)
            : base(batchId, language, sourceKind, status, createdAtUtc, completedAtUtc, items)
        {
        }

        public int AddedCount => Items.Count(item => item.Decision == ImportItemDecision.Pending);

        public int DuplicateCount => Items.Count(item => item.Decision == ImportItemDecision.Duplicate);
    }

    public sealed class ImportBatchItemDto
    {
        public ImportBatchItemDto(
            long itemId,
            string sourceText,
            string anchorSourceSenseId,
            string? wordGroupId,
            ImportItemDecision decision,
            ImportWordGroupDto? wordGroup)
        {
            ItemId = itemId;
            SourceText = sourceText;
            AnchorSourceSenseId = anchorSourceSenseId;
            WordGroupId = wordGroupId;
            Decision = decision;
            WordGroup = wordGroup;
        }

        public long ItemId { get; }

        public string SourceText { get; }

        public string AnchorSourceSenseId { get; }

        public string? WordGroupId { get; }

        public ImportItemDecision Decision { get; }

        public ImportWordGroupDto? WordGroup { get; }
    }

    public sealed class ImportWordGroupDto
    {
        public ImportWordGroupDto(
            string wordGroupId,
            LearningLanguage language,
            string anchorSourceSenseId,
            ImportWordGroupPlacement placement,
            DateTimeOffset createdAtUtc,
            IReadOnlyList<ImportWordGroupMemberDto> members)
        {
            WordGroupId = wordGroupId;
            Language = language;
            AnchorSourceSenseId = anchorSourceSenseId;
            Placement = placement;
            CreatedAtUtc = createdAtUtc;
            Members = members ?? throw new ArgumentNullException(nameof(members));
        }

        public string WordGroupId { get; }

        public LearningLanguage Language { get; }

        public string AnchorSourceSenseId { get; }

        public ImportWordGroupPlacement Placement { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public IReadOnlyList<ImportWordGroupMemberDto> Members { get; }
    }

    public sealed class ImportWordGroupMemberDto
    {
        public ImportWordGroupMemberDto(
            string language,
            string sourceSenseId,
            string sourceLexemeId,
            string headword,
            string? reading,
            string partOfSpeechLabel,
            string attributeLabel,
            bool isPhrase,
            IReadOnlyList<string> meanings,
            IReadOnlyList<ImportWordGroupFormDto> forms)
        {
            Language = language;
            SourceSenseId = sourceSenseId;
            SourceLexemeId = sourceLexemeId;
            Headword = headword;
            Reading = reading;
            PartOfSpeechLabel = partOfSpeechLabel;
            AttributeLabel = attributeLabel;
            IsPhrase = isPhrase;
            Meanings = meanings ?? throw new ArgumentNullException(nameof(meanings));
            Forms = forms ?? throw new ArgumentNullException(nameof(forms));
        }

        public string Language { get; }

        public string SourceSenseId { get; }

        public string SourceLexemeId { get; }

        public string Headword { get; }

        public string? Reading { get; }

        public string PartOfSpeechLabel { get; }

        public string AttributeLabel { get; }

        public bool IsPhrase { get; }

        public IReadOnlyList<string> Meanings { get; }

        public IReadOnlyList<ImportWordGroupFormDto> Forms { get; }
    }

    public sealed class ImportWordGroupFormDto
    {
        public ImportWordGroupFormDto(
            string surface,
            string? reading,
            string formLabel,
            string detailLabel,
            bool isCommon,
            bool isPrimary)
        {
            Surface = surface;
            Reading = reading;
            FormLabel = formLabel;
            DetailLabel = detailLabel;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string? Reading { get; }

        public string FormLabel { get; }

        public string DetailLabel { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }
}
