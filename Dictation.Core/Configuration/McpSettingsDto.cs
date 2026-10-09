namespace Dictation.Core.Configuration
{
    /// <summary>表示可选本机MCP服务的开关与监听端口。</summary>
    /// <param name="Enabled">是否启用本机监听。</param>
    /// <param name="Port">1024至65535之间的固定端口。</param>
    public sealed record McpSettingsDto(bool Enabled = false, int Port = 47821);
}
