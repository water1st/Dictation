namespace Dictation.Presentation.Theme
{
    /// <summary>
    /// 读取当前系统明暗主题并通知主题变化，隔离操作系统访问。
    /// </summary>
    public interface ISystemThemeProvider : IDisposable
    {
        ResolvedTheme CurrentTheme { get; }

        event EventHandler? ThemeChanged;
    }
}
