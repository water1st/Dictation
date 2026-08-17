using Dictation.DataAccess.Configuration;

namespace Dictation.Services.Configuration
{
    public sealed class SettingsService
    {
        private readonly SettingsDataAccessor settingsDataAccessor;

        public SettingsService(SettingsDataAccessor settingsDataAccessor)
        {
            this.settingsDataAccessor = settingsDataAccessor ?? throw new ArgumentNullException(nameof(settingsDataAccessor));
        }

        public async Task<ApplicationSettingsDto> LoadAsync(CancellationToken cancellationToken = default)
        {
            var settings = await settingsDataAccessor.LoadAsync(cancellationToken);
            return settings is null
                ? new ApplicationSettingsDto(ThemeMode.System, LearningLanguage.Japanese)
                : ApplicationSettingsMapper.ToDto(settings);
        }

        public async Task SaveAsync(ApplicationSettingsDto settings, CancellationToken cancellationToken = default)
        {
            var persistedSettings = ApplicationSettingsMapper.ToPo(settings);
            await settingsDataAccessor.SaveAsync(persistedSettings, cancellationToken);
        }
    }
}
