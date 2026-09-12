namespace Dictation.Core.Learning
{
    /// <summary>
    /// 提供学习计划读取和保存能力。
    /// </summary>
    public interface ILearningPlanEditor : ILearningPlanReader
    {
        /// <summary>
        /// 校验并保存学习计划。
        /// </summary>
        /// <param name="request">待保存的计划选项。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>规范化并补全展示信息后的学习计划。</returns>
        Task<LearningPlanDto> SaveAsync(
            SaveLearningPlanRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
