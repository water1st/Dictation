using Dictation.Core.Configuration;
using Dictation.Core.Statistics;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>旧首页隔离测试的未开始统计状态；真实打卡由集成测试覆盖。</summary>
    internal sealed class EmptyDailyCheckInReader : IDailyCheckInReader
    {
        public Task<DailyCheckInDto> GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DailyCheckInDto(language, new DateOnly(2026, 10, 12), 0, 0, 0, 0, 0, 0, false, false));
        }
    }
}
