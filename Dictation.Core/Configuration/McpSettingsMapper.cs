namespace Dictation.Core.Configuration
{
    /// <summary>转换MCP配置模型，不读取文件或操作监听器。</summary>
    internal static class McpSettingsMapper
    {
        /// <summary>将已校验的业务设置转换为独立配置文档。</summary>
        /// <param name="settings">有效的本机监听选项。</param>
        /// <returns>只包含MCP的根文档。</returns>
        internal static McpConfigurationDocument ToDocument(McpSettingsDto settings) => new(new(settings.Enabled, settings.Port));
    }
}
