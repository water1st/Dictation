namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 表示应用当前使用的主题与学习语言设置。
    /// </summary>
    /// <param name="ThemeMode">主题模式。</param>
    /// <param name="LastLanguage">最近使用的学习语言。</param>
    public sealed record ApplicationSettingsDto(ThemeMode ThemeMode, LearningLanguage LastLanguage);
}
