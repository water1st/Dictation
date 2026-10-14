using Dictation.Core.Configuration;

namespace Dictation.Core.Statistics
{
    /// <summary>为有题目的练习记录当天首次目标，不暴露进度编辑能力。</summary>
    public interface IDailyPlanSnapshotRecorder
    {
        /// <summary>计算真实可练范围并只保存第一份每日目标。</summary>
        /// <param name="language">当前练习语言。</param>
        /// <param name="cancellationToken">取消查询与写入。</param>
        Task RecordTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
