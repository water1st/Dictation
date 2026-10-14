namespace Dictation.Core.Configuration
{
    /// <summary>对应CONFIG/mcp.json的独立根文档，避免覆盖主题设置。</summary>
    /// <param name="Mcp">MCP配置节。</param>
    internal sealed record McpConfigurationDocument(McpSettingsDocument Mcp);
}
