namespace Dictation.Core.Review
{
    /// <summary>提供复习客观作答和原子进度提交能力，不管理计划或启动范围。</summary>
    public interface IReviewSessionAnswerer
    {
        /// <summary>评价当前待答题，保存重练状态，并仅在形式完成时更新一次记忆。</summary>
        /// <param name="request">会话、当前题目标识和用户实际输入。</param>
        /// <param name="cancellationToken">取消本次业务操作。</param>
        /// <returns>即时正确答案，以及下一题或结算结果。</returns>
        Task<ReviewAnswerResultDto> SubmitAnswerAsync(SubmitReviewAnswerRequestDto request, CancellationToken cancellationToken = default);
    }
}
