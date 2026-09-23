using System.Globalization;
using Dictation.Core.Configuration;
using Dictation.DataAccess.Learning;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 将进度表的按日词组计数与当前语言计划目标组合为首页进度。
    /// </summary>
    internal sealed class LearningProgressService : ILearningProgressReader
    {
        private readonly LearningSessionDataAccessor dataAccessor;
        private readonly ILearningPlanReader planReader;
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// 初始化每日学习进度业务服务。
        /// </summary>
        /// <param name="dataAccessor">读取已开始和已完成词组计数。</param>
        /// <param name="planReader">读取当前语言计划目标。</param>
        /// <param name="timeProvider">确定今天的本地日期。</param>
        public LearningProgressService(
            LearningSessionDataAccessor dataAccessor,
            ILearningPlanReader planReader,
            TimeProvider timeProvider)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.planReader = planReader ?? throw new ArgumentNullException(nameof(planReader));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <inheritdoc />
        public async Task<LearningDailyProgressDto> GetTodayAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "学习语言无效。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var plan = await planReader.LoadAsync(language, cancellationToken);
            var localDate = timeProvider.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var counts = await dataAccessor.GetDailyProgressCountsAsync(
                language.ToString(), localDate, cancellationToken);
            return new LearningDailyProgressDto(
                language, counts.StartedGroupCount, counts.CompletedGroupCount, plan.DailyNewTarget);
        }
    }
}
