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
        Task<LearningStartRequestDto> CreateStartRequestAsync(
            LearningLanguage language,
            IReadOnlyList<string>? selectedWordGroupIds,
            CancellationToken cancellationToken = default);
    }
}
