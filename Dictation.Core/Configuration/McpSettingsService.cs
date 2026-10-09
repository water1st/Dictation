using Dictation.DataAccess.Runtime;
using Microsoft.Extensions.Configuration;

namespace Dictation.Core.Configuration
{
    /// <summary>通过统一配置能力读取和原子保存独立MCP设置，不写主题文档。</summary>
    public sealed class McpSettingsService
    {
        private readonly IConfiguration configuration;
        private readonly ApplicationPaths paths;

        /// <summary>组合现有配置根与根目录配置路径。</summary>
        /// <param name="configuration">唯一Host的配置根。</param>
        /// <param name="paths">应用运行目录路径。</param>
        public McpSettingsService(IConfiguration configuration, ApplicationPaths paths)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        /// <summary>读取MCP设置，配置缺失时默认关闭。</summary>
        /// <returns>当前开关和固定端口。</returns>
        public McpSettingsDto Load()
        {
            try
            {
                if (configuration.GetValue("Mcp:LoadFailed", false))
                {
                    throw new SettingsConfigurationException("MCP配置无法解析，请在设置中重新保存。");
                }
                var settings = new McpSettingsDto(configuration.GetValue("Mcp:Enabled", false), configuration.GetValue("Mcp:Port", 47821));
                Validate(settings);
                return settings;
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
            {
                throw new SettingsConfigurationException("MCP配置无效，请填写有效开关和1024至65535之间的端口。");
            }
        }

        /// <summary>校验后原子保存MCP配置，取消时不替换原文件。</summary>
        /// <param name="settings">需要保存的开关和端口。</param>
        /// <param name="cancellationToken">取消写回。</param>
        public Task SaveAsync(McpSettingsDto settings, CancellationToken cancellationToken = default)
        {
            Validate(settings);
            return configuration.SetValueAsync(Path.Combine(paths.Config, "mcp.json"), McpSettingsMapper.ToDocument(settings), cancellationToken);
        }

        /// <summary>限制高位固定端口，监听地址不由配置决定。</summary>
        /// <param name="settings">待读取或写入的业务选项。</param>
        internal static void Validate(McpSettingsDto settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (settings.Port is < 1024 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(settings.Port), "MCP端口必须在1024至65535之间。");
            }
        }
    }
}
