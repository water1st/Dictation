using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>
    /// 为无需真实数据库的表现层测试提供可控制的每日学习进度。
    /// </summary>
    internal sealed class StubLearningProgressReader : ILearningProgressReader
    {
        public Func<LearningLanguage, CancellationToken, Task<LearningDailyProgressDto>> ReadAsync { get; set; } =
            (language, _) => Task.FromResult(new LearningDailyProgressDto(language, 0, 0, 20));

        public Task<LearningDailyProgressDto> GetTodayAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAsync(language, cancellationToken);
        }
    }
}
