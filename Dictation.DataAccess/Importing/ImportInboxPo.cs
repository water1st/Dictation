namespace Dictation.DataAccess.Importing
{
    public sealed record ImportBatchStagePo(
        string Language,
        string SourceKind,
        IReadOnlyList<ImportBatchStageItemPo> Items);

    public sealed record ImportBatchStageItemPo(
        string SourceText,
        string AnchorSourceSenseId,
        ImportWordGroupSnapshotPo Group);

    public sealed record ImportWordGroupSnapshotPo(
        string Language,
        string AnchorSourceSenseId,
        IReadOnlyList<ImportWordGroupMemberPo> Members);

    public sealed record ImportWordGroupMemberPo(
        string Language,
        string SourceSenseId,
        string SourceLexemeId,
        string Headword,
        string? Reading,
        string PartOfSpeechLabel,
        string AttributeLabel,
        bool IsPhrase,
        string MeaningLanguage,
        string MeaningSource,
        string MeaningPrecision,
        IReadOnlyList<string> Meanings,
        IReadOnlyList<ImportWordGroupFormPo> Forms);

    public sealed record ImportWordGroupFormPo(
        string Surface,
        string? Reading,
        string FormLabel,
        string DetailLabel,
        bool IsCommon,
        bool IsPrimary);

    public sealed record ImportBatchPo(
        string BatchId,
        string Language,
        string SourceKind,
        string Status,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<ImportBatchItemPo> Items);

    public sealed record ImportBatchItemPo(
        long ItemId,
        string SourceText,
        string AnchorSourceSenseId,
        string? WordGroupId,
        string Decision,
        ImportWordGroupPo? WordGroup);

    public sealed record ImportWordGroupPo(
        string WordGroupId,
        string Language,
        string AnchorSourceSenseId,
        string Placement,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<ImportWordGroupMemberPo> Members);
}
