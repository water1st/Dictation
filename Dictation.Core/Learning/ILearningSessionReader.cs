using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 只读取各语言尚未结束的学习会话，供首页判断应恢复还是先选词。
    /// </summary>
    public interface ILearningSessionReader
    {
        /// <summary>
        /// 获取指定语言的活动学习会话，不创建新会话。
        /// </summary>
        /// <param name="language">要读取的学习语言。</param>
        /// <param name="cancellationToken">取消数据库读取的令牌。</param>
        /// <returns>现有活动会话；不存在时为空。</returns>
        Task<LearningSessionDto?> GetActiveAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);
    }
}
