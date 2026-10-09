namespace Dictation.Presentation.Navigation
{
    /// <summary>提供首页进入AI导入设置的最小导航能力，不暴露其它业务导航。</summary>
    public interface IMcpSettingsNavigation
    {
        /// <summary>在同一窗口打开本机MCP设置。</summary>
        /// <param name="cancellationToken">取消导航和设置读取。</param>
        Task OpenMcpSettingsAsync(CancellationToken cancellationToken = default);
    }
}
