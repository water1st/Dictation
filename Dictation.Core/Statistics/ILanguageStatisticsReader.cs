using Dictation.Core.Configuration;

namespace Dictation.Core.Statistics
{
    /// <summary>提供语言隔离的词组报表与只读记忆预测，不启动练习或更新记忆。</summary>
    public interface ILanguageStatisticsReader
    {
        /// <summary>以一份复习词库快照计算本次词组统计及预测。</summary>
        /// <param name="language">首页固定的语言。</param>
        /// <param name="cancellationToken">取消查询与计算。</param>
        /// <returns>现存完成事实、易错组及假定不再复习的预计趋势。</returns>
        Task<LanguageStatisticsDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
