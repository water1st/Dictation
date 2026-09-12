using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 提供学习计划只读能力。
    /// </summary>
    public interface ILearningPlanReader
    {
        /// <summary>
        /// 读取指定语言的已保存计划，尚无记录时返回默认计划。
        /// </summary>
        /// <param name="language">学习计划所属语言。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>指定语言当前生效的学习计划。</returns>
        Task<LearningPlanDto> LoadAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);
    }
}
