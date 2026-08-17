namespace Dictation.Services.Configuration
{
    public static class ApplicationSettingsMapper
    {
        public static ApplicationSettingsDto ToDto(ApplicationSettingsDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            return new ApplicationSettingsDto(
                ParseThemeMode(document.ThemeMode),
                ParseLearningLanguage(document.LastLanguage));
        }

        public static ApplicationSettingsDocument ToDocument(ApplicationSettingsDto settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return new ApplicationSettingsDocument
            {
                ThemeMode = settings.ThemeMode switch
                {
                    ThemeMode.System => nameof(ThemeMode.System),
                    ThemeMode.Light => nameof(ThemeMode.Light),
                    ThemeMode.Dark => nameof(ThemeMode.Dark),
                    _ => throw new SettingsConfigurationException("持久化设置中的主题值无效。")
                },
                LastLanguage = settings.LastLanguage switch
                {
                    LearningLanguage.Japanese => nameof(LearningLanguage.Japanese),
                    LearningLanguage.English => nameof(LearningLanguage.English),
                    _ => throw new SettingsConfigurationException("持久化设置中的学习语言值无效。")
                }
            };
        }

        private static ThemeMode ParseThemeMode(string? value)
        {
            return value switch
            {
                nameof(ThemeMode.System) => ThemeMode.System,
                nameof(ThemeMode.Light) => ThemeMode.Light,
                nameof(ThemeMode.Dark) => ThemeMode.Dark,
                _ => throw new SettingsConfigurationException("持久化设置中的主题值无效。")
            };
        }

        private static LearningLanguage ParseLearningLanguage(string? value)
        {
            return value switch
            {
                nameof(LearningLanguage.Japanese) => LearningLanguage.Japanese,
                nameof(LearningLanguage.English) => LearningLanguage.English,
                _ => throw new SettingsConfigurationException("持久化设置中的学习语言值无效。")
            };
        }
    }
}
