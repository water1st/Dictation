using System.Globalization;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.DataAccess.Review;

namespace Dictation.Core.Review
{
    /// <summary>组合既有按日整组查询与独立复习计划，为首页提供只读进度。</summary>
    internal sealed class ReviewProgressService : IReviewProgressReader
    {
        private readonly ReviewSessionDataAccessor sessions;
        private readonly IReviewPlanReader plans;
        private readonly TimeProvider timeProvider;

        /// <summary>注入既有数据查询、当前复习计划及应用时钟。</summary>
        /// <param name="sessions">读取复习完成日的整组计数。</param>
        /// <param name="plans">读取当前语言的每日目标。</param>
        /// <param name="timeProvider">确定用户当地的今天。</param>
        public ReviewProgressService(ReviewSessionDataAccessor sessions, IReviewPlanReader plans, TimeProvider timeProvider)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.plans = plans ?? throw new ArgumentNullException(nameof(plans));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <inheritdoc />
        public async Task<ReviewDailyProgressDto> GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            PracticePlanOptionValidator.ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var plan = await plans.LoadAsync(language, cancellationToken);
            var localDate = timeProvider.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var completed = await sessions.CountCompletedOnDateAsync(language.ToString(), localDate, cancellationToken);
            return new ReviewDailyProgressDto(language, completed, plan.DailyReviewTarget);
        }
    }
}
