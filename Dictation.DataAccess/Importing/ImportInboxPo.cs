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
        IReadOnlyList<ImportWordGroupFormPo> Forms,
        string? KanjiUsageCode = null);

    public sealed record ImportWordGroupFormPo
    {
        public ImportWordGroupFormPo(
            string surface,
            string? reading,
            string formCode,
            string? scriptCode,
            string? roleCode,
            string formLabel,
            string detailLabel,
            bool isCommon,
            bool isPrimary)
        {
            Surface = surface;
            Reading = reading;
            FormCode = string.IsNullOrWhiteSpace(formCode)
                ? throw new ArgumentException("词形稳定标识不能为空。", nameof(formCode))
                : formCode;
            ScriptCode = scriptCode;
            RoleCode = roleCode;
            FormLabel = formLabel;
            DetailLabel = detailLabel;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string? Reading { get; }

        public string FormCode { get; }

        public string? ScriptCode { get; }

        public string? RoleCode { get; }

        public string FormLabel { get; }

        public string DetailLabel { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }

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
