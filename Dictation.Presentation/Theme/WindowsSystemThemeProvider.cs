using Microsoft.Win32;

namespace Dictation.Presentation.Theme
{
    /// <summary>
    /// 从 Windows 个性化配置读取主题，并订阅系统偏好变化。
    /// </summary>
    internal sealed class WindowsSystemThemeProvider : ISystemThemeProvider
    {
        private const string PersonalizeRegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

        private readonly Func<int?> readThemeValue;
        private readonly bool subscribedToSystemEvents;
        private ResolvedTheme currentTheme;
        private bool isDisposed;

        /// <summary>
        /// 从 Windows 个性化配置读取主题，并订阅系统偏好变化。
        /// </summary>
        public WindowsSystemThemeProvider()
            : this(ReadAppsUseLightThemeValue, subscribeToSystemEvents: true)
        {
        }

        /// <summary>
        /// 从 Windows 个性化配置读取主题，并订阅系统偏好变化。
        /// </summary>
        /// <param name="readThemeValue">读取系统主题整数值的委托；为空时使用注册表。</param>
        /// <param name="subscribeToSystemEvents">是否订阅 Windows 用户偏好变化事件。</param>
        public WindowsSystemThemeProvider(
            Func<int?>? readThemeValue,
            bool subscribeToSystemEvents = true)
        {
            this.readThemeValue = readThemeValue ?? ReadAppsUseLightThemeValue;
            currentTheme = ReadThemeSafely(this.readThemeValue);
            var hasSubscribedToSystemEvents = false;

            if (subscribeToSystemEvents)
            {
                try
                {
                    SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                    hasSubscribedToSystemEvents = true;
                }
                catch
                {
                }
            }

            subscribedToSystemEvents = hasSubscribedToSystemEvents;
        }

        public ResolvedTheme CurrentTheme => currentTheme;

        public event EventHandler? ThemeChanged;

        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            if (subscribedToSystemEvents)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            }

            ThemeChanged = null;
        }

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (isDisposed)
            {
                return;
            }

            var nextTheme = ReadThemeSafely(readThemeValue);
            if (nextTheme == currentTheme)
            {
                return;
            }

            currentTheme = nextTheme;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        private static int? ReadAppsUseLightThemeValue()
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            var value = key?.GetValue(AppsUseLightThemeValueName);
            return value switch
            {
                int intValue => intValue,
                long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
                string stringValue when int.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static ResolvedTheme ReadThemeSafely(Func<int?> readValue)
        {
            try
            {
                return readValue() switch
                {
                    0 => ResolvedTheme.Dark,
                    1 => ResolvedTheme.Light,
                    _ => ResolvedTheme.Light
                };
            }
            catch
            {
                return ResolvedTheme.Light;
            }
        }
    }
}
