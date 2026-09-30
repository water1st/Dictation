using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>编排独立复习计划的校验、读取、保存和整组管理。</summary>
    internal sealed class ReviewPlanService : IReviewPlanReader, IReviewPlanEditor, IReviewLibraryManager
    {
        private readonly ReviewPlanDataAccessor dataAccessor;
        private readonly PracticePlanOptionValidator optionValidator;
        private readonly EnglishExampleParadigmResolver exampleResolver;
        private readonly TimeProvider timeProvider;

        /// <summary>注入独立存储、共用纯规则和例词解析能力。</summary>
        /// <param name="dataAccessor">独立复习计划的数据访问器。</param>
        /// <param name="optionValidator">不读写任何计划的纯选项校验组件。</param>
        /// <param name="exampleResolver">按需解析示例范式的共用组件。</param>
        /// <param name="timeProvider">用于记录计划更新时间的时钟。</param>
        public ReviewPlanService(ReviewPlanDataAccessor dataAccessor, PracticePlanOptionValidator optionValidator,
            EnglishExampleParadigmResolver exampleResolver, TimeProvider timeProvider)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.optionValidator = optionValidator ?? throw new ArgumentNullException(nameof(optionValidator));
            this.exampleResolver = exampleResolver ?? throw new ArgumentNullException(nameof(exampleResolver));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <inheritdoc/>
        public async Task<ReviewPlanDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            PracticePlanOptionValidator.ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var plan = await dataAccessor.GetPlanAsync(language.ToString(), cancellationToken);
            if (plan is null)
            {
                return new ReviewPlanDto(language, 20, 20, ReviewRangeMode.All, Array.Empty<string>(),
                    language == LearningLanguage.Japanese ? new[] { "Dictionary" } : new[] { "Base" },
                    new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                    language == LearningLanguage.Japanese ? new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji } : new[] { LearningOutputMode.Latin },
                    EnglishFormLabelMode.GrammarName, null, timeProvider.GetUtcNow(), null);
            }
            var mapped = ReviewPlanMapper.ToDto(plan, null);
            return mapped with { EnglishExampleParadigm = await ResolveExampleAsync(mapped.EnglishFormLabelMode, mapped.EnglishExampleSourceSenseId, mapped.FormCodes, cancellationToken) };
        }

        /// <inheritdoc/>
        public async Task<ReviewPlanDto> SaveAsync(SaveReviewPlanRequestDto request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            PracticePlanOptionValidator.ValidateLanguage(request.Language);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.DailyReviewTarget, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.DailyReviewTarget, 500);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.SessionWordLimit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.SessionWordLimit, 500);
            if (!Enum.IsDefined(request.RangeMode))
            {
                throw new ArgumentOutOfRangeException(nameof(request.RangeMode));
            }
            ArgumentNullException.ThrowIfNull(request.WordGroupIds);
            optionValidator.ValidateLanguageLabels(request.Language, request.EnglishFormLabelMode, request.EnglishExampleSourceSenseId);
            var groups = request.RangeMode == ReviewRangeMode.All ? Array.Empty<string>()
                : request.WordGroupIds.Select(id => PracticePlanOptionValidator.NormalizeText(id, "词组标识")).Distinct(StringComparer.Ordinal).ToArray();
            if (request.RangeMode == ReviewRangeMode.Selected && groups.Length == 0)
            {
                throw new ArgumentException("指定范围至少需要勾选一个词组。", nameof(request.WordGroupIds));
            }
            var normalized = request with
            {
                WordGroupIds = groups,
                FormCodes = optionValidator.NormalizeForms(request.Language, request.FormCodes),
                Channels = optionValidator.NormalizeChannels(request.Channels),
                OutputModes = optionValidator.NormalizeOutputs(request.Language, request.OutputModes),
                EnglishExampleSourceSenseId = request.EnglishFormLabelMode == EnglishFormLabelMode.ExampleParadigm
                    ? PracticePlanOptionValidator.NormalizeText(request.EnglishExampleSourceSenseId, "英语示例词义项") : null
            };
            var example = await ResolveExampleAsync(normalized.EnglishFormLabelMode, normalized.EnglishExampleSourceSenseId, normalized.FormCodes, cancellationToken);
            var plan = ReviewPlanMapper.ToPo(normalized, timeProvider.GetUtcNow());
            // SQL 在保存事务中校验实际组归属，避免 UI 勾选之后词组被删除或移库造成半保存。
            await dataAccessor.SavePlanAsync(plan, cancellationToken);
            return ReviewPlanMapper.ToDto(plan, example);
        }

        /// <inheritdoc/>
        public async Task DeleteWordGroupAsync(LearningLanguage language, string wordGroupId, CancellationToken cancellationToken = default)
        {
            PracticePlanOptionValidator.ValidateLanguage(language);
            var id = PracticePlanOptionValidator.NormalizeText(wordGroupId, nameof(wordGroupId));
            cancellationToken.ThrowIfCancellationRequested();
            await dataAccessor.DeleteWordGroupAsync(language.ToString(), id, cancellationToken);
        }

        /// <summary>仅在示例标签模式下解析范式，语法名称模式不查询词库。</summary>
        /// <param name="mode">当前英语标签模式。</param>
        /// <param name="sourceSenseId">所选示例动词义项。</param>
        /// <param name="forms">当前启用的词形。</param>
        /// <param name="cancellationToken">取消词库解析的令牌。</param>
        /// <returns>示例模式的范式，或语法名称模式的空值。</returns>
        private async Task<EnglishExampleParadigmDto?> ResolveExampleAsync(EnglishFormLabelMode mode, string? sourceSenseId,
            IReadOnlyList<string> forms, CancellationToken cancellationToken)
        {
            return mode == EnglishFormLabelMode.ExampleParadigm
                ? await exampleResolver.ResolveAsync(sourceSenseId, forms, cancellationToken) : null;
        }
    }
}
