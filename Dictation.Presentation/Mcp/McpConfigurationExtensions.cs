using Microsoft.Extensions.Configuration;

namespace Dictation.Presentation.Mcp
{
    /// <summary>复用微软JSON提供程序加载独立MCP配置，损坏文件只禁用MCP而不阻止应用启动。</summary>
    internal static class McpConfigurationExtensions
    {
        /// <summary>加载可选配置；解析失败保留错误标记，保存有效文件并重载后自动清除。</summary>
        /// <param name="builder">唯一Host使用的配置构建器，基准路径为应用根目录。</param>
        /// <returns>原配置构建器。</returns>
        internal static IConfigurationBuilder AddDictationMcpConfiguration(this IConfigurationBuilder builder)
        {
            return builder.AddJsonFile(source =>
            {
                source.Path = Path.Combine("CONFIG", "mcp.json");
                source.Optional = true;
                source.ReloadOnChange = false;
                source.OnLoadException = context =>
                {
                    context.Ignore = true;
                    context.Provider.Set("Mcp:LoadFailed", "true");
                };
            });
        }
    }
}
