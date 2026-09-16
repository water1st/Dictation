namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 表示 settings.json 的持久化字段，不承载业务默认值。
    /// </summary>
    internal sealed class ApplicationSettingsDocument
    {
        public string? ThemeMode { get; init; }

        public string? LastLanguage { get; init; }
    }
}
