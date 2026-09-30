using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.DataAccess.Review;
using System.Globalization;

namespace Dictation.Core.Review
{
    /// <summary>编排独立复习会话的风险选词、持久化恢复和只读浏览。</summary>
    internal sealed class ReviewSessionService : IReviewSessionStarter, IReviewSessionReader
    {
        private readonly IReviewPlanReader plans;
        private readonly IReviewLibraryQuery library;
        private readonly ReviewSessionDataAccessor dataAccessor;
        private readonly TimeProvider timeProvider;
        private readonly ReviewSessionSelectionPlanner selectionPlanner;
        /// <summary>组合计划、只读风险查询、会话持久化与时钟。</summary>
        /// <param name="plans">读取独立复习计划。</param>
        /// <param name="library">读取完整复习组及当前记忆风险。</param>
        /// <param name="dataAccessor">保存和读取会话。</param>
        /// <param name="timeProvider">提供统一 UTC 与本地日期。</param>
        /// <param name="selectionPlanner">按启用适用形式进行风险选词。</param>
        public ReviewSessionService(IReviewPlanReader plans, IReviewLibraryQuery library,
            ReviewSessionDataAccessor dataAccessor, TimeProvider timeProvider, ReviewSessionSelectionPlanner selectionPlanner)
        {
            this.plans = plans ?? throw new ArgumentNullException(nameof(plans));
            this.library = library ?? throw new ArgumentNullException(nameof(library));
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.selectionPlanner = selectionPlanner ?? throw new ArgumentNullException(nameof(selectionPlanner));
        }

        /// <inheritdoc />
        public async Task<ReviewSessionDto?> StartOrResumeAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var active = await dataAccessor.GetActiveAsync(language.ToString(), cancellationToken);
            if (active is not null)
            {
                return ReviewSessionMapper.ToDto(active);
            }
            var plan = await plans.LoadAsync(language, cancellationToken);
            var snapshot = await library.LoadAsync(language, cancellationToken);
            var localDate = timeProvider.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var completed = await dataAccessor.CountCompletedOnDateAsync(language.ToString(), localDate, cancellationToken);
            var selected = selectionPlanner.Select(snapshot, plan, Math.Max(0, plan.DailyReviewTarget - completed));
            cancellationToken.ThrowIfCancellationRequested();
            if (selected.Count == 0)
            {
                return null;
            }
            var created = await dataAccessor.CreateAsync(ReviewSessionMapper.ToCreatePo(Guid.NewGuid().ToString("N"), plan, selected, timeProvider.GetUtcNow(), localDate), cancellationToken);
            return ReviewSessionMapper.ToDto(created);
        }
        /// <inheritdoc />
        public async Task<ReviewSessionDto?> GetActiveAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var active = await dataAccessor.GetActiveAsync(language.ToString(), cancellationToken);
            return active is null ? null : ReviewSessionMapper.ToDto(active);
        }
        /// <inheritdoc />
        public async Task<IReadOnlyList<ImportWordGroupDto>> GetBrowseItemsAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
            cancellationToken.ThrowIfCancellationRequested();
            var groups = await dataAccessor.GetBrowseItemsAsync(sessionId.Trim(), cancellationToken);
            return groups.Select(ImportWordGroupMapper.ToDto).ToArray();
        }

        /// <summary>拒绝没有真实语言语义的枚举值。</summary>
        /// <param name="language">调用方提供的语言。</param>
        private static void ValidateLanguage(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "复习会话语言不受支持。");
            }
        }
    }
}
