namespace Dictation.Core.Configuration
{
    /// <summary>保存MCP开关和固定端口的配置字段，不包含运行状态。</summary>
    /// <param name="Enabled">启动时是否开启监听。</param>
    /// <param name="Port">固定的本机监听端口。</param>
    internal sealed record McpSettingsDocument(bool Enabled, int Port);
}
