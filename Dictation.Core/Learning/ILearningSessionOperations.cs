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
        Task<LearningSessionDto> StartOrResumeAsync(
            StartLearningSessionRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取本轮会话可浏览的完整词组成员。
        /// </summary>
        Task<IReadOnlyList<LearningBrowseItemDto>> GetBrowseItemsAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取或创建当前待答题目。
        /// </summary>
        Task<LearningQuestionDto?> GetCurrentQuestionAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 提交当前题答案并返回反馈、下一题或完成摘要。
        /// </summary>
        Task<LearningAnswerResultDto> SubmitAnswerAsync(
            SubmitLearningAnswerRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
