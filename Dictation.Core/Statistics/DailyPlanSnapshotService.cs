using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.Core.Kana;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Review;
using Dictation.DataAccess.Statistics;
using System.Globalization;

namespace Dictation.Core.Statistics
{
    /// <summary>将已有出题范围与完成事实组成一次性每日目标，不依赖会话业务服务。</summary>
    internal sealed class DailyPlanSnapshotService : IDailyPlanSnapshotRecorder
    {
        private readonly ILearningPlanReader learningPlans;
        private readonly IReviewPlanReader reviewPlans;
        private readonly IReviewLibraryQuery reviewLibrary;
        private readonly LearningSessionDataAccessor learning;
        private readonly ReviewSessionDataAccessor review;
        private readonly ReviewSessionSelectionPlanner reviewSelection;
        private readonly KanaDailyAvailabilityService kana;
        private readonly DailyCheckInDataAccessor snapshots;
        private readonly TimeProvider clock;

        /// <summary>组合现有计划、纯筛选和持久化，不形成会话服务之间的环。</summary>
        /// <param name="learningPlans">读取学习目标和启用形式。</param>
        /// <param name="reviewPlans">读取复习目标和范围。</param>
        /// <param name="reviewLibrary">现有词形风险查询。</param>
        /// <param name="learning">原学习候选与活动组查询。</param>
        /// <param name="review">原复习活动组查询。</param>
        /// <param name="reviewSelection">共享复习适用范围筛选。</param>
        /// <param name="kana">独立五十音只读范围。</param>
        /// <param name="snapshots">首次目标与完成事实访问。</param>
        /// <param name="clock">统一日期。</param>
        public DailyPlanSnapshotService(ILearningPlanReader learningPlans, IReviewPlanReader reviewPlans,
            IReviewLibraryQuery reviewLibrary, LearningSessionDataAccessor learning, ReviewSessionDataAccessor review,
            ReviewSessionSelectionPlanner reviewSelection, KanaDailyAvailabilityService kana, DailyCheckInDataAccessor snapshots, TimeProvider clock)
        {
            this.learningPlans = learningPlans; this.reviewPlans = reviewPlans; this.reviewLibrary = reviewLibrary;
            this.learning = learning; this.review = review; this.reviewSelection = reviewSelection;
            this.kana = kana; this.snapshots = snapshots; this.clock = clock;
        }

        /// <inheritdoc />
        public async Task RecordTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            PracticePlanOptionValidator.ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            var date = clock.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var code = language.ToString();
            if (await snapshots.GetSnapshotAsync(code, date, cancellationToken) is not null) return;
            var learningPlan = await learningPlans.LoadAsync(language, cancellationToken);
            var learningCompleted = await snapshots.GetCompletedGroupIdsAsync(code, date, false, cancellationToken);
            var enabled = learningPlan.FormCodes.ToHashSet(StringComparer.Ordinal);
            var learningIds = (await learning.GetCandidatesAsync(code, cancellationToken))
                .Where(candidate => LearningSessionSelectionEligibility.NeedsPractice(candidate,
                    LearningSessionSelectionEligibility.GetForms(candidate, enabled, learningPlan.OutputModes, language)))
                .Select(candidate => candidate.WordGroupId).ToHashSet(StringComparer.Ordinal);
            // 原候选查询已包含活动学习组；已掌握的昨日组不能随整个轮次再次进入今天目标。
            learningIds.UnionWith(learningCompleted);

            var reviewPlan = await reviewPlans.LoadAsync(language, cancellationToken);
            var reviewCompleted = await snapshots.GetCompletedGroupIdsAsync(code, date, true, cancellationToken);
            var library = await reviewLibrary.LoadAsync(language, cancellationToken);
            // 每轮数量不能缩小当天目标；已完成、当前到期和恢复轮次按组ID合并。
            var reviewIds = reviewSelection.GetCandidates(library, reviewPlan).Select(item => item.WordGroupId).ToHashSet(StringComparer.Ordinal);
            var reviewActive = await review.GetActiveAsync(code, cancellationToken);
            if (reviewActive is not null)
                reviewIds.UnionWith(await review.GetIncompleteWordGroupIdsAsync(reviewActive.SessionId, cancellationToken));
            reviewIds.UnionWith(reviewCompleted);
            var kanaTarget = language == LearningLanguage.Japanese ? (await kana.GetProgressAsync(cancellationToken)).Target : 0;
            var learningTarget = Math.Min(learningPlan.DailyNewTarget, learningIds.Count);
            var reviewTarget = Math.Min(reviewPlan.DailyReviewTarget, reviewIds.Count);
            if (learningTarget == 0 && reviewTarget == 0 && kanaTarget == 0) return;
            await snapshots.InsertIfAbsentAsync(new DailyPlanSnapshotPo(code, date, learningTarget, reviewTarget, kanaTarget), cancellationToken);
        }
    }
}
