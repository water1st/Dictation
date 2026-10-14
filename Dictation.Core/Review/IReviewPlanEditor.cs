namespace Dictation.Core.Review
{
    /// <summary>提供经过业务校验的复习计划保存能力。</summary>
    public interface IReviewPlanEditor
    {
        /// <summary>原子保存选项与词组范围，不改变学习计划或记忆。</summary>
        /// <param name="request">待校验并规范化的计划选项。</param>
        /// <param name="cancellationToken">取消保存的令牌。</param>
        /// <returns>实际保存的规范化计划及例词范式。</returns>
        Task<ReviewPlanDto> SaveAsync(SaveReviewPlanRequestDto request, CancellationToken cancellationToken = default);
    }
}
