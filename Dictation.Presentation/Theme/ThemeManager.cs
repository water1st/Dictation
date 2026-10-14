using Dictation.Core.Configuration;

namespace Dictation.Presentation.Theme
{
    /// <summary>
    /// 根据用户选择或系统主题解析应用主题并发布变化通知。
    /// </summary>
    public sealed class ThemeManager : IDisposable
    {
        private readonly ISystemThemeProvider systemThemeProvider;
        private ResolvedTheme currentTheme;
        private bool isDisposed;

        /// <summary>
        /// 根据用户选择或系统主题解析应用主题并发布变化通知。
        /// </summary>
        /// <param name="systemThemeProvider">可替换的系统主题读取及通知来源。</param>
        public ThemeManager(ISystemThemeProvider systemThemeProvider)
        {
            this.systemThemeProvider = systemThemeProvider
                ?? throw new ArgumentNullException(nameof(systemThemeProvider));
            Mode = ThemeMode.System;
            currentTheme = ReadProviderThemeSafely();
            this.systemThemeProvider.ThemeChanged += OnSystemThemeChanged;
        }

        public ThemeMode Mode { get; private set; }

        public ResolvedTheme CurrentTheme => currentTheme;

        public event EventHandler? ThemeChanged;

        /// <summary>
        /// 应用明确主题或跟随系统；系统读取失败时使用明色。
        /// </summary>
        /// <param name="mode">固定明暗或跟随系统的主题模式。</param>
        public void SetThemeMode(ThemeMode mode)
        {
            ValidateThemeMode(mode);
            ThrowIfDisposed();

            var nextTheme = mode == ThemeMode.System
                ? ReadProviderThemeSafely()
                : ToResolvedTheme(mode);
            var hasThemeChanged = nextTheme != currentTheme;
            var hasModeChanged = mode != Mode;
            Mode = mode;
            currentTheme = nextTheme;

            if (hasThemeChanged || hasModeChanged && mode == ThemeMode.System)
            {
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            systemThemeProvider.ThemeChanged -= OnSystemThemeChanged;
            systemThemeProvider.Dispose();
            ThemeChanged = null;
        }

        private void OnSystemThemeChanged(object? sender, EventArgs e)
        {
            if (isDisposed || Mode != ThemeMode.System)
            {
                return;
            }

            var nextTheme = ReadProviderThemeSafely();
            if (nextTheme == currentTheme)
            {
                return;
            }

            currentTheme = nextTheme;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        private ResolvedTheme ReadProviderThemeSafely()
        {
            try
            {
                return systemThemeProvider.CurrentTheme switch
                {
                    ResolvedTheme.Light => ResolvedTheme.Light,
                    ResolvedTheme.Dark => ResolvedTheme.Dark,
                    _ => ResolvedTheme.Light
                };
            }
            catch
            {
                return ResolvedTheme.Light;
            }
        }

        private static ResolvedTheme ToResolvedTheme(ThemeMode mode)
        {
            return mode switch
            {
                ThemeMode.Light => ResolvedTheme.Light,
                ThemeMode.Dark => ResolvedTheme.Dark,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "主题模式值无效。")
            };
        }

        private static void ValidateThemeMode(ThemeMode mode)
        {
            if (mode is not (ThemeMode.System or ThemeMode.Light or ThemeMode.Dark))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "主题模式值无效。");
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(ThemeManager));
            }
        }
    }
}
