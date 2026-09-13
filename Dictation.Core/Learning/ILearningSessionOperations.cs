using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 提供学习会话页面实际使用的会话编排能力。
    /// </summary>
    public interface ILearningSessionOperations
    {
        /// <summary>
        /// 启动指定语言的新会话，或恢复同语言尚未完成的会话。
        /// </summary>
        /// <param name="request">包含学习语言、选词方式和可选手选词组的启动请求。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>新建的会话，或同语言已存在的未完成会话。</returns>
        Task<LearningSessionDto> StartOrResumeAsync(
            StartLearningSessionRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取本轮会话可浏览的完整词组成员。
        /// </summary>
        /// <param name="sessionId">要浏览的学习会话标识。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>本轮会话纳入的完整词组及其成员；没有词组时返回空集合。</returns>
        Task<IReadOnlyList<LearningBrowseItemDto>> GetBrowseItemsAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取或创建当前待答题目。
        /// </summary>
        /// <param name="sessionId">要继续答题的学习会话标识。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>当前待答题目；会话已完成时返回 <see langword="null"/>。</returns>
        Task<LearningQuestionDto?> GetCurrentQuestionAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 提交当前题答案并返回反馈、下一题或完成摘要。
        /// </summary>
        /// <param name="request">包含会话、当前题标识和用户答案的提交请求。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>答题正误、正确答案，以及下一题或会话完成摘要。</returns>
        Task<LearningAnswerResultDto> SubmitAnswerAsync(
            SubmitLearningAnswerRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
