using Dictation.Core.Configuration;

namespace Dictation.Presentation.Mcp
{
    /// <summary>仅控制HTTP监听生命周期，不提供导入或数据库操作。</summary>
    public interface IMcpHostControl
    {
        /// <summary>串行停止旧监听器并应用设置，端口冲突保留失败状态而不退出WPF。</summary>
        /// <param name="settings">已保存或请求重试的开关和端口。</param>
        /// <param name="cancellationToken">取消等待或启动。</param>
        Task ApplyAsync(McpSettingsDto settings, CancellationToken cancellationToken = default);
    }
}
