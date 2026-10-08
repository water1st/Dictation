using Dictation.Core.Configuration;
using Dictation.Core.Review;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>仅为表现层失败、取消和异步乱序测试提供受控的复习进度边界。</summary>
    internal sealed class StubReviewProgressReader : IReviewProgressReader
    {
        internal Func<LearningLanguage, CancellationToken, Task<ReviewDailyProgressDto>> ReadAsync { get; set; } =
            (language, _) => Task.FromResult(new ReviewDailyProgressDto(language, 0, 20));

        /// <inheritdoc />
        public Task<ReviewDailyProgressDto> GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            return ReadAsync(language, cancellationToken);
        }
    }
}
