using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>
    /// 为无需数据库的首页测试返回可控制的活动会话状态。
    /// </summary>
    internal sealed class StubLearningSessionReader : ILearningSessionReader
    {
        public Func<LearningLanguage, CancellationToken, Task<LearningSessionDto?>> ReadAsync { get; set; } =
            (_, _) => Task.FromResult<LearningSessionDto?>(null);

        public Task<LearningSessionDto?> GetActiveAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAsync(language, cancellationToken);
        }
    }
}
