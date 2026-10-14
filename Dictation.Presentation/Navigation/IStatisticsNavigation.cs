using Dictation.Core.Configuration;

namespace Dictation.Presentation.Navigation
{
    /// <summary>提供仅从首页进入固定语言统计页面的窄导航能力。</summary>
    public interface IStatisticsNavigation
    {
        /// <summary>在同一窗口进入月历与报表，不允许途中切换语言。</summary>
        /// <param name="language">首页当前语言。</param>
        /// <param name="cancellationToken">取消导航与读取。</param>
        Task OpenStatisticsAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
