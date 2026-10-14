using Dictation.DataAccess.Learning;
using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 负责校验和持久化学习计划、读取学习库，并依据词库完整词组生成英语示例范式。
    /// </summary>
    internal sealed class LearningPlanService :
        ILearningPlanEditor,
        ILearningLibraryManager,
        ILearningSessionStartPreparer
    {
        private readonly LearningPlanDataAccessor dataAccessor;
        private readonly PracticePlanOptionValidator optionValidator;
        private readonly EnglishExampleParadigmResolver exampleResolver;
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// 初始化学习计划服务。
        /// </summary>
        /// <param name="dataAccessor">用于读写学习计划和学习库的数据访问器。</param>
        /// <param name="optionValidator">学习与复习共用的纯选项校验。</param>
        /// <param name="exampleResolver">共用的英语示例动词范式解析能力。</param>
        /// <param name="timeProvider">用于记录计划更新时间和生成默认计划时间的时钟。</param>
        public LearningPlanService(
            LearningPlanDataAccessor dataAccessor,
            PracticePlanOptionValidator optionValidator,
            EnglishExampleParadigmResolver exampleResolver,
            TimeProvider timeProvider)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.optionValidator = optionValidator ?? throw new ArgumentNullException(nameof(optionValidator));
            this.exampleResolver = exampleResolver ?? throw new ArgumentNullException(nameof(exampleResolver));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <summary>
        /// 读取指定语言的学习计划；尚未保存计划时生成默认计划。
        /// </summary>
        /// <param name="language">学习计划所属语言。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>已保存的计划，或尚无记录时生成的默认计划。</returns>
        public async Task<LearningPlanDto> LoadAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var plan = await dataAccessor.GetPlanAsync(language.ToString(), cancellationToken);
            return plan is null
                ? CreateDefault(language)
                : await AssemblePlanAsync(plan, cancellationToken);
        }

        /// <summary>
        /// 校验并保存学习计划；示例范式模式下同时从词库解析所选动词的启用词形。
        /// </summary>
        /// <param name="request">待保存的计划选项。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>规范化并补全示例范式后的学习计划。</returns>
        public async Task<LearningPlanDto> SaveAsync(
            SaveLearningPlanRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = ValidateAndNormalize(request);
            var example = normalized.Language == LearningLanguage.English
                && normalized.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                    ? await exampleResolver.ResolveAsync(
                        normalized.EnglishExampleSourceSenseId,
                        normalized.FormCodes,
                        cancellationToken)
                    : null;
            var plan = LearningPlanMapper.ToPo(normalized, timeProvider.GetUtcNow());
            await dataAccessor.SavePlanAsync(plan, cancellationToken);
            return LearningPlanMapper.ToDto(plan, example);
        }

        /// <summary>
        /// 分页读取指定语言的学习库整组词条。
        /// </summary>
        /// <param name="language">学习库所属语言。</param>
        /// <param name="pageNumber">从 1 开始的页码。</param>
        /// <param name="pageSize">每页数量，最大为 500。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>包含总数和当前页整组词条的分页结果。</returns>
        public async Task<LearningLibraryPageDto> GetLibraryPageAsync(
            LearningLanguage language,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 500);
            cancellationToken.ThrowIfCancellationRequested();
            var page = await dataAccessor.GetLearningLibraryPageAsync(
                language.ToString(),
                checked((pageNumber - 1) * pageSize),
                pageSize,
                cancellationToken);
            return LearningPlanMapper.ToDto(page, pageNumber, pageSize);
        }

        /// <summary>
        /// 删除指定语言学习库中的完整词组及其学习进度。
        /// </summary>
        /// <param name="language">词组所属学习语言。</param>
        /// <param name="wordGroupId">要删除的词组稳定标识。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        public async Task DeleteWordGroupAsync(
            LearningLanguage language,
            string wordGroupId,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            ArgumentException.ThrowIfNullOrWhiteSpace(wordGroupId);
            cancellationToken.ThrowIfCancellationRequested();
            var result = await dataAccessor.DeleteWordGroupAsync(
                language.ToString(), wordGroupId.Trim(), timeProvider.GetUtcNow(), cancellationToken);
            switch (result)
            {
                case DeleteLearningWordGroupResult.Deleted:
                    return;
                case DeleteLearningWordGroupResult.NotFound:
                    throw new KeyNotFoundException("学习库词组不存在。");
                case DeleteLearningWordGroupResult.WrongLanguage:
                case DeleteLearningWordGroupResult.WrongPlacement:
                    throw new InvalidOperationException("只能删除当前语言学习库中的整组词条。");
                default:
                    throw new InvalidOperationException("未知的学习库删除结果。");
            }
        }

        /// <summary>
        /// 根据已保存计划生成一轮学习的启动请求，并在手动模式下校验所选词组均属于当前学习库。
        /// </summary>
        /// <param name="language">本轮学习语言。</param>
        /// <param name="selectedWordGroupIds">手动模式下选择的词组标识；其他模式下不参与范围选择。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>包含选词模式、数量上限及已验证手动词组的启动请求。</returns>
        public async Task<LearningStartRequestDto> CreateStartRequestAsync(
            LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var plan = await LoadAsync(language, cancellationToken);
            if (plan.SelectionMode != LearningSelectionMode.Manual)
            {
                return new LearningStartRequestDto(language, plan.SessionWordLimit, plan.SelectionMode, Array.Empty<string>());
            }

            if (selectedWordGroupIds is null || selectedWordGroupIds.Count == 0)
            {
                throw new ArgumentException("手动选词时至少需要选择一个词组。", nameof(selectedWordGroupIds));
            }

            var normalizedIds = selectedWordGroupIds.Select(id => NormalizeText(id, "词组标识")).ToArray();
            if (normalizedIds.Distinct(StringComparer.Ordinal).Count() != normalizedIds.Length)
            {
                throw new ArgumentException("手动选择的词组不能重复。", nameof(selectedWordGroupIds));
            }

            var existingCount = await dataAccessor.CountLearningWordGroupsAsync(language.ToString(), normalizedIds, cancellationToken);
            if (existingCount != normalizedIds.Length)
            {
                throw new ArgumentException("选择中包含不存在、其他语言或非学习库词组。", nameof(selectedWordGroupIds));
            }

            return new LearningStartRequestDto(language, plan.SessionWordLimit, plan.SelectionMode, normalizedIds);
        }

        /// <summary>
        /// 校验计划各选项的语言约束，并规范化词形、渠道、输出形式和示例义项。
        /// </summary>
        /// <param name="request">原始保存请求。</param>
        /// <returns>去重并规范化后的保存请求。</returns>
        private SaveLearningPlanRequestDto ValidateAndNormalize(SaveLearningPlanRequestDto request)
        {
            ValidateLanguage(request.Language);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.DailyNewTarget, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.DailyNewTarget, 500);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.SessionWordLimit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.SessionWordLimit, 500);
            ValidateEnum(request.SelectionMode, nameof(request.SelectionMode));
            ValidateEnum(request.EnglishFormLabelMode, nameof(request.EnglishFormLabelMode));
            var forms = optionValidator.NormalizeForms(request.Language, request.FormCodes);
            var channels = optionValidator.NormalizeChannels(request.Channels);
            var outputs = optionValidator.NormalizeOutputs(request.Language, request.OutputModes);
            optionValidator.ValidateLanguageLabels(request.Language, request.EnglishFormLabelMode, request.EnglishExampleSourceSenseId);
            var exampleSourceSenseId = request.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                ? NormalizeText(request.EnglishExampleSourceSenseId, "英语示例词义项")
                : null;
            return request with
            {
                EnglishExampleSourceSenseId = exampleSourceSenseId,
                FormCodes = forms,
                Channels = channels,
                OutputModes = outputs
            };
        }

        /// <summary>
        /// 装配持久化计划及其按需从词库加载的英语示例范式。
        /// </summary>
        /// <param name="plan">持久化的学习计划。</param>
        /// <param name="cancellationToken">取消词库解析的令牌。</param>
        /// <returns>包含可展示示例范式的学习计划。</returns>
        private async Task<LearningPlanDto> AssemblePlanAsync(
            LearningPlanPo plan,
            CancellationToken cancellationToken)
        {
            var mapped = LearningPlanMapper.ToDto(plan, null);
            var example = mapped.Language == LearningLanguage.English
                && mapped.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                ? await exampleResolver.ResolveAsync(
                    mapped.EnglishExampleSourceSenseId,
                    mapped.FormCodes,
                    cancellationToken)
                : null;
            return mapped with { EnglishExampleParadigm = example };
        }

        /// <summary>
        /// 为尚无持久化记录的语言创建默认学习计划。
        /// </summary>
        /// <param name="language">默认计划所属语言。</param>
        /// <returns>使用该语言默认词形与输出方式的未持久化计划。</returns>
        private LearningPlanDto CreateDefault(LearningLanguage language)
        {
            return new LearningPlanDto(
                language,
                20,
                20,
                LearningSelectionMode.Random,
                EnglishFormLabelMode.GrammarName,
                null,
                language == LearningLanguage.Japanese ? new[] { "Dictionary" } : new[] { "Base" },
                new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                language == LearningLanguage.Japanese
                    ? new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji }
                    : new[] { LearningOutputMode.Latin },
                timeProvider.GetUtcNow(),
                null);
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            ValidateEnum(language, nameof(language));
        }

        private static void ValidateEnum<T>(T value, string parameterName) where T : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "选项不受支持。");
            }
        }

        private static string NormalizeText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName}不能为空。", fieldName);
            }

            return value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
