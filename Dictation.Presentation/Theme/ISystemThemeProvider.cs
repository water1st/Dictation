namespace Dictation.Presentation.Theme
{
    public interface ISystemThemeProvider : IDisposable
    {
        ResolvedTheme CurrentTheme { get; }

        event EventHandler? ThemeChanged;
    }
}
