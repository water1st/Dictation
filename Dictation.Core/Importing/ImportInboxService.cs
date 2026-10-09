using Dictation.DataAccess.Importing;
using Dictation.Core.Configuration;
using Dictation.Core.Lexicon;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 负责把词库义项解析为完整词组快照，并编排导入收件箱的暂存、审核与释义更新。
    /// </summary>
    internal sealed class ImportInboxService : IImportStaging, IImportInboxQuery, IImportInboxActions, IImportBatchReader
    {
        private readonly ILexiconImportGroupResolver lexiconImportGroupResolver;
        private readonly ImportInboxDataAccessor importInboxAccessor;

        /// <summary>
        /// 初始化导入收件箱服务。
        /// </summary>
        /// <param name="lexiconImportGroupResolver">用于按来源义项解析完整词组的词库能力。</param>
        /// <param name="importInboxAccessor">用于持久化导入批次及审核结果的数据访问器。</param>
        public ImportInboxService(
            ILexiconImportGroupResolver lexiconImportGroupResolver,
            ImportInboxDataAccessor importInboxAccessor)
        {
            this.lexiconImportGroupResolver = lexiconImportGroupResolver
                ?? throw new ArgumentNullException(nameof(lexiconImportGroupResolver));
            this.importInboxAccessor = importInboxAccessor
                ?? throw new ArgumentNullException(nameof(importInboxAccessor));
        }

        /// <summary>
        /// 规范化并去重用户选择，将每个来源义项的完整词组快照暂存到导入收件箱。
        /// </summary>
        /// <param name="request">包含学习语言、导入来源及所选义项的暂存请求。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>持久化后的批次，以及新增和重复项目统计。</returns>
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
                    group.Select(member => ImportWordGroupMapper.ToSnapshotPo(
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
            return ImportInboxMapper.ToStageResultDto(batch);
        }

        /// <summary>
        /// 读取指定学习语言尚待处理的导入批次。
        /// </summary>
        /// <param name="language">要筛选的学习语言。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>按数据访问层顺序返回的待处理批次。</returns>
        public async Task<IReadOnlyList<ImportBatchDto>> GetPendingBatchesAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var batches = await importInboxAccessor.ListPendingAsync(
                language.ToString(),
                cancellationToken);
            return batches.Select(ImportInboxMapper.ToDto).ToArray();
        }

        /// <inheritdoc />
        public async Task<ImportBatchDto> GetBatchAsync(string batchId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
            cancellationToken.ThrowIfCancellationRequested();
            return ImportInboxMapper.ToDto(await importInboxAccessor.GetBatchAsync(batchId, cancellationToken));
        }

        /// <summary>
        /// 对批次中的指定项目执行导入决定；进入学习或复习前会确认整组成员均有中文释义。
        /// </summary>
        /// <param name="request">批次编号、项目编号及目标决定。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
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

        /// <summary>
        /// 规范化并更新导入词组中指定来源义项的中文释义。
        /// </summary>
        /// <param name="request">词组、来源义项及替换后的中文释义。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
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

        /// <summary>
        /// 拒绝空白项，规范化首尾空白并按序去重人工释义；不判断文本是否包含汉字。
        /// </summary>
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

        /// <summary>
        /// 根据中文语言标记和至少一条非空释义判断完整性；不在此处重新识别文本语言。
        /// </summary>
        private static bool HasChineseMeaning(ImportWordGroupMemberPo member)
        {
            return string.Equals(member.MeaningLanguage, "zh-CN", StringComparison.Ordinal)
                && member.Meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning));
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
    }
}
