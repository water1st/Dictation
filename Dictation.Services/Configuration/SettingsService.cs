using Dictation.DataAccess.Runtime;
using Microsoft.Extensions.Configuration;

namespace Dictation.Services.Configuration
{
    public sealed class SettingsService
    {
        private readonly IConfiguration configuration;
        private readonly ApplicationPaths applicationPaths;

        public SettingsService(
            IConfiguration configuration,
            ApplicationPaths applicationPaths)
        {
            this.configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            this.applicationPaths = applicationPaths
                ?? throw new ArgumentNullException(nameof(applicationPaths));
        }

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
