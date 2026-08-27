using Dictation.DataAccess.Importing;
using Dictation.Services.Configuration;
using Dictation.Services.Lexicon;

namespace Dictation.Services.Importing
{
    public sealed class ImportInboxService
    {
        private readonly LexiconLookupService lexiconLookupService;
        private readonly ImportInboxDataAccessor importInboxAccessor;

        public ImportInboxService(
            LexiconLookupService lexiconLookupService,
            ImportInboxDataAccessor importInboxAccessor)
        {
            this.lexiconLookupService = lexiconLookupService
                ?? throw new ArgumentNullException(nameof(lexiconLookupService));
            this.importInboxAccessor = importInboxAccessor
                ?? throw new ArgumentNullException(nameof(importInboxAccessor));
        }

        public async Task<StageImportBatchResultDto> StageAsync(
            StageImportBatchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ValidateStageRequest(request);
            cancellationToken.ThrowIfCancellationRequested();

            var selections = request.Selections
                .Select(selection => new ImportSelectionDto(
                    NormalizeText(selection.SourceText, "来源词形"),
                    NormalizeText(selection.SourceSenseId, "来源义项")))
                .GroupBy(selection => selection.SourceSenseId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var stageItems = new List<ImportBatchStageItemPo>(selections.Length);
            foreach (var selection in selections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = await lexiconLookupService.GetImportGroupAsync(
                    request.Language,
                    selection.SourceSenseId,
                    cancellationToken);
                if (group.Count == 0)
                {
                    throw new ArgumentException(
                        $"未找到 source sense：{selection.SourceSenseId}。",
                        nameof(request));
                }

                var snapshot = new ImportWordGroupSnapshotPo(
                    request.Language.ToString(),
                    selection.SourceSenseId,
                    group.Select(MapMember).ToArray());
                stageItems.Add(new ImportBatchStageItemPo(
                    selection.SourceText,
                    selection.SourceSenseId,
                    snapshot));
            }

            var batch = await importInboxAccessor.StageAsync(
                new ImportBatchStagePo(
                    request.Language.ToString(),
                    request.SourceKind.ToString(),
                    stageItems),
                cancellationToken);
            return MapStageResult(batch);
        }

        public async Task<IReadOnlyList<ImportBatchDto>> GetPendingBatchesAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var batches = await importInboxAccessor.ListPendingAsync(
                language.ToString(),
                cancellationToken);
            return batches.Select(MapBatch).ToArray();
        }

        public async Task DecideAsync(
            DecideImportBatchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.BatchId);
            if (request.ItemIds is null || request.ItemIds.Count == 0)
            {
                throw new ArgumentException("至少选择一个批次项目。", nameof(request.ItemIds));
            }

            if (request.ItemIds.Any(itemId => itemId <= 0)
                || request.ItemIds.Distinct().Count() != request.ItemIds.Count)
            {
                throw new ArgumentException("批次项目编号必须是正数且不能重复。", nameof(request.ItemIds));
            }

            if (!Enum.IsDefined(request.Decision))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.Decision),
                    request.Decision,
                    "导入决定不受支持。");
            }

            await importInboxAccessor.DecideAsync(
                request.BatchId,
                request.ItemIds,
                request.Decision.ToString(),
                cancellationToken);
        }

        private static ImportWordGroupMemberPo MapMember(LexiconLookupResultDto value)
        {
            return new ImportWordGroupMemberPo(
                value.Language.ToString(),
                value.SourceSenseId,
                value.SourceLexemeId,
                value.Headword,
                value.Reading,
                value.PartOfSpeechLabel,
                value.AttributeLabel,
                value.IsPhrase,
                value.Meanings,
                value.Forms.Select(form => new ImportWordGroupFormPo(
                    form.Surface,
                    form.Reading,
                    form.FormLabel,
                    form.DetailLabel,
                    form.IsCommon,
                    form.IsPrimary)).ToArray());
        }

        private static StageImportBatchResultDto MapStageResult(ImportBatchPo batch)
        {
            return new StageImportBatchResultDto(
                batch.BatchId,
                ParseLanguage(batch.Language),
                ParseSourceKind(batch.SourceKind),
                ParseBatchStatus(batch.Status),
                batch.CreatedAtUtc,
                batch.CompletedAtUtc,
                batch.Items.Select(MapItem).ToArray());
        }

        private static ImportBatchDto MapBatch(ImportBatchPo batch)
        {
            return new ImportBatchDto(
                batch.BatchId,
                ParseLanguage(batch.Language),
                ParseSourceKind(batch.SourceKind),
                ParseBatchStatus(batch.Status),
                batch.CreatedAtUtc,
                batch.CompletedAtUtc,
                batch.Items.Select(MapItem).ToArray());
        }

        private static ImportBatchItemDto MapItem(ImportBatchItemPo item)
        {
            return new ImportBatchItemDto(
                item.ItemId,
                item.SourceText,
                item.AnchorSourceSenseId,
                item.WordGroupId,
                ParseDecision(item.Decision),
                item.WordGroup is null ? null : MapGroup(item.WordGroup));
        }

        private static ImportWordGroupDto MapGroup(ImportWordGroupPo group)
        {
            return new ImportWordGroupDto(
                group.WordGroupId,
                ParseLanguage(group.Language),
                group.AnchorSourceSenseId,
                ParsePlacement(group.Placement),
                group.CreatedAtUtc,
                group.Members.Select(member => new ImportWordGroupMemberDto(
                    member.Language,
                    member.SourceSenseId,
                    member.SourceLexemeId,
                    member.Headword,
                    member.Reading,
                    member.PartOfSpeechLabel,
                    member.AttributeLabel,
                    member.IsPhrase,
                    member.Meanings,
                    member.Forms.Select(form => new ImportWordGroupFormDto(
                        form.Surface,
                        form.Reading,
                        form.FormLabel,
                        form.DetailLabel,
                        form.IsCommon,
                        form.IsPrimary)).ToArray())).ToArray());
        }

        private static void ValidateStageRequest(StageImportBatchRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateLanguage(request.Language);
            if (!Enum.IsDefined(request.SourceKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.SourceKind),
                    request.SourceKind,
                    "导入来源不受支持。");
            }

            if (request.Selections is null || request.Selections.Count == 0)
            {
                throw new ArgumentException("导入批次至少需要一个选择。", nameof(request.Selections));
            }

            foreach (var selection in request.Selections)
            {
                if (selection is null)
                {
                    throw new ArgumentException("导入选择不能为空。", nameof(request.Selections));
                }

                NormalizeText(selection.SourceText, "来源词形");
                NormalizeText(selection.SourceSenseId, "来源义项");
            }
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "学习语言不受支持。");
            }
        }

        private static string NormalizeText(string value, string fieldName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        }

        private static LearningLanguage ParseLanguage(string value)
        {
            return Enum.Parse<LearningLanguage>(value, ignoreCase: false);
        }

        private static ImportSourceKind ParseSourceKind(string value)
        {
            return Enum.Parse<ImportSourceKind>(value, ignoreCase: false);
        }

        private static ImportBatchStatus ParseBatchStatus(string value)
        {
            return Enum.Parse<ImportBatchStatus>(value, ignoreCase: false);
        }

        private static ImportItemDecision ParseDecision(string value)
        {
            return Enum.Parse<ImportItemDecision>(value, ignoreCase: false);
        }

        private static ImportWordGroupPlacement ParsePlacement(string value)
        {
            return Enum.Parse<ImportWordGroupPlacement>(value, ignoreCase: false);
        }
    }
}
