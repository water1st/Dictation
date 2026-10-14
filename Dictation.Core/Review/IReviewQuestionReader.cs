using Dictation.Core.Practice;

namespace Dictation.Core.Review
{
    /// <summary>提供复习当前题目的创建与恢复能力，不接受或评价答案。</summary>
    public interface IReviewQuestionReader
    {
        /// <summary>返回持久化待答题；已完成或整组已全部删除时返回空。</summary>
        /// <param name="sessionId">复习会话标识。</param>
        /// <param name="cancellationToken">取消本次读写操作。</param>
        Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken = default);
    }
}
