using Dictation.DataAccess.Runtime;
using Microsoft.Extensions.Configuration;

namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 读取和保存应用设置，并在配置完全缺失时提供业务默认值。
    /// </summary>
    public sealed class SettingsService
    {
        private readonly IConfiguration configuration;
        private readonly ApplicationPaths applicationPaths;

        /// <summary>
        /// 使用现有配置管线读取设置，并使用应用路径确定原子写回目标。
        /// </summary>
        /// <param name="configuration">包含 settings.json 的应用配置根。</param>
        /// <param name="applicationPaths">提供设置文件的运行时路径。</param>
        public SettingsService(
            IConfiguration configuration,
            ApplicationPaths applicationPaths)
        {
            this.configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            this.applicationPaths = applicationPaths
                ?? throw new ArgumentNullException(nameof(applicationPaths));
        }

        /// <summary>
        /// 加载当前设置；仅当两个字段都不存在时返回业务默认值。
        /// </summary>
        public ApplicationSettingsDto Load()
        {
            var themeMode = configuration.GetValue<string>(
                nameof(ApplicationSettingsDocument.ThemeMode));
            var lastLanguage = configuration.GetValue<string>(
                nameof(ApplicationSettingsDocument.LastLanguage));
            if (themeMode is null && lastLanguage is null)
            {
                return new ApplicationSettingsDto(ThemeMode.System, LearningLanguage.Japanese);
            }

            return ApplicationSettingsMapper.ToDto(new ApplicationSettingsDocument
            {
                ThemeMode = themeMode,
                LastLanguage = lastLanguage
            });
        }

        /// <summary>
        /// 按既有文档格式原子保存设置并刷新配置视图。
        /// </summary>
        /// <param name="settings">待保存的应用设置。</param>
        /// <param name="cancellationToken">用于中止序列化和写回的取消标记。</param>
        public async Task SaveAsync(ApplicationSettingsDto settings, CancellationToken cancellationToken = default)
        {
            var document = ApplicationSettingsMapper.ToDocument(settings);
            await configuration.SetValueAsync(
                applicationPaths.SettingsFile,
                document,
                cancellationToken);
        }
    }
}
