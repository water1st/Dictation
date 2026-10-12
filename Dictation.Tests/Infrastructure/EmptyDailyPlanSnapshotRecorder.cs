using Dictation.Core.Configuration;
using Dictation.Core.Statistics;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>旧出题算法隔离测试不记录每日目标；真实启动及统计由独立集成测试覆盖。</summary>
    internal sealed class EmptyDailyPlanSnapshotRecorder : IDailyPlanSnapshotRecorder
    {
        public Task RecordTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
