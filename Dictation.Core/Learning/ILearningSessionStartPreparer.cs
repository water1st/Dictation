using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 提供学习会话开始前的计划读取与手选词组校验能力。
    /// </summary>
    public interface ILearningSessionStartPreparer
    {
        /// <summary>
        /// 根据已保存计划生成经过校验的学习启动请求。
        /// </summary>
        /// <param name="language">本轮学习语言。</param>
        /// <param name="selectedWordGroupIds">手动模式下选择的词组标识；其他模式下不参与范围选择。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>包含选词方式、数量上限和已验证手选词组的启动请求。</returns>
        Task<LearningStartRequestDto> CreateStartRequestAsync(
            LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds,
            CancellationToken cancellationToken = default);
    }
}
