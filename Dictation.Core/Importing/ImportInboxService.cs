using Dictation.DataAccess.Importing;
using Dictation.Core.Configuration;
using Dictation.Core.Lexicon;

namespace Dictation.Core.Importing
{
    public sealed class ImportInboxService
    {
        private readonly ILexiconImportGroupResolver lexiconImportGroupResolver;
        private readonly ImportInboxDataAccessor importInboxAccessor;

        public ImportInboxService(
            ILexiconImportGroupResolver lexiconImportGroupResolver,
            ImportInboxDataAccessor importInboxAccessor)
        {
            this.lexiconImportGroupResolver = lexiconImportGroupResolver
                ?? throw new ArgumentNullException(nameof(lexiconImportGroupResolver));
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
                    NormalizeText(selection.SourceSenseId, "来源义项"),
                    NormalizeMeaningList(selection.ChineseMeaningOverride, allowNull: true)))
                .GroupBy(selection => selection.SourceSenseId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var stageItems = new List<ImportBatchStageItemPo>(selections.Length);
            foreach (var selection in selections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = await lexiconImportGroupResolver.GetImportGroupAsync(
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
                    group.Select(member => MapMember(
                        member,
                        string.Equals(member.SourceSenseId, selection.SourceSenseId, StringComparison.Ordinal)
                            ? selection.ChineseMeaningOverride
                            : null)).ToArray());
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

            if (request.Decision is ImportDecision.Learning or ImportDecision.Review)
            {
                var batch = await importInboxAccessor.GetBatchAsync(request.BatchId, cancellationToken);
                var selectedItems = batch.Items
                    .Where(item => request.ItemIds.Contains(item.ItemId))
                    .ToArray();
                if (selectedItems.Length != request.ItemIds.Count
                    || selectedItems.Any(item => item.WordGroup is null))
                {
                    throw new ArgumentException("选择的批次项目不存在或没有词组。", nameof(request.ItemIds));
                }

                if (selectedItems
                    .SelectMany(item => item.WordGroup!.Members)
                    .Any(member => !HasChineseMeaning(member)))
                {
                    throw new InvalidOperationException("词组仍有成员缺少中文释义，不能进入学习或复习。");
                }
            }

            await importInboxAccessor.DecideAsync(
                request.BatchId,
                request.ItemIds,
                request.Decision.ToString(),
                cancellationToken);
        }

        public Task UpdateMeaningsAsync(
            UpdateImportMeaningsRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var wordGroupId = NormalizeText(request.WordGroupId, "词组编号");
            var sourceSenseId = NormalizeText(request.SourceSenseId, "来源义项");
            var meanings = NormalizeMeaningList(request.Meanings, allowNull: false)!;
            return importInboxAccessor.UpdateMeaningsAsync(
                wordGroupId,
                sourceSenseId,
                meanings,
                cancellationToken);
        }

        private static ImportWordGroupMemberPo MapMember(
            LexiconLookupResultDto value,
            IReadOnlyList<string>? meaningOverride)
        {
            var isUserOverride = meaningOverride is not null;
            return new ImportWordGroupMemberPo(
                value.Language.ToString(),
                value.SourceSenseId,
                value.SourceLexemeId,
                value.Headword,
                value.Reading,
                value.PartOfSpeechLabel,
                value.AttributeLabel,
                value.IsPhrase,
                "zh-CN",
                isUserOverride ? LexiconMeaningSource.User.ToString() : value.MeaningSource.ToString(),
                isUserOverride ? LexiconMeaningPrecision.UserProvided.ToString() : value.MeaningPrecision.ToString(),
                meaningOverride ?? value.Meanings,
                value.Forms.Select(form => new ImportWordGroupFormPo(
                    form.Surface,
                    form.Reading,
                    form.FormCode,
                    form.ScriptCode,
                    form.RoleCode,
                    form.FormLabel,
                    form.DetailLabel,
                    form.IsCommon,
                    form.IsPrimary)).ToArray(),
                value.KanjiUsageCode);
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
                    string.Equals(member.MeaningLanguage, "zh-CN", StringComparison.Ordinal)
                        ? member.Meanings
                        : Array.Empty<string>(),
                    member.Forms.Select(form => new ImportWordGroupFormDto(
                        form.Surface,
                        form.Reading,
                        form.FormCode,
                        form.ScriptCode,
                        form.RoleCode,
                        form.FormLabel,
                        form.DetailLabel,
                        form.IsCommon,
                        form.IsPrimary)).ToArray(),
                    ParseMeaningSource(member.MeaningSource),
                    ParseMeaningPrecision(member.MeaningPrecision),
                    member.KanjiUsageCode)).ToArray());
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
                NormalizeMeaningList(selection.ChineseMeaningOverride, allowNull: true);
            }
        }

        private static IReadOnlyList<string>? NormalizeMeaningList(
            IReadOnlyList<string>? meanings,
            bool allowNull)
        {
            if (meanings is null)
            {
                return allowNull
                    ? null
                    : throw new ArgumentNullException(nameof(meanings));
            }

            if (meanings.Count == 0)
            {
                throw new ArgumentException("至少需要一个中文释义。", nameof(meanings));
            }

            return meanings
                .Select(meaning => NormalizeText(meaning, "中文释义"))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static bool HasChineseMeaning(ImportWordGroupMemberPo member)
        {
            return string.Equals(member.MeaningLanguage, "zh-CN", StringComparison.Ordinal)
                && member.Meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning));
        }

        private static LexiconMeaningSource ParseMeaningSource(string value)
        {
            return Enum.TryParse<LexiconMeaningSource>(value, out var parsed)
                && Enum.IsDefined(parsed)
                    ? parsed
                    : LexiconMeaningSource.Missing;
        }

        private static LexiconMeaningPrecision ParseMeaningPrecision(string value)
        {
            return Enum.TryParse<LexiconMeaningPrecision>(value, out var parsed)
                && Enum.IsDefined(parsed)
                    ? parsed
                    : LexiconMeaningPrecision.Missing;
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
