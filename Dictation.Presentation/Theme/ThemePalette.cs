using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace Dictation.Presentation.Theme
{
    /// <summary>
    /// 将已解析的主题颜色写入 WPF 动态资源字典。
    /// </summary>
    internal static class ThemePalette
    {
        /// <summary>
        /// 替换颜色资源并使用可冻结画刷，供动态资源绑定刷新。
        /// </summary>
        /// <param name="resources">写入应用颜色与画刷的资源字典。</param>
        /// <param name="theme">已解析的明色或暗色主题。</param>
        public static void Apply(ResourceDictionary resources, ResolvedTheme theme)
        {
            ArgumentNullException.ThrowIfNull(resources);

            var palette = theme switch
            {
                ResolvedTheme.Light => new ThemeColors(
                    Background: MediaColor.FromRgb(246, 248, 252),
                    Foreground: MediaColor.FromRgb(31, 41, 55),
                    SecondaryForeground: MediaColor.FromRgb(100, 116, 139),
                    Card: Colors.White,
                    Border: MediaColor.FromRgb(226, 232, 240),
                    Accent: MediaColor.FromRgb(232, 78, 117),
                    AccentSoft: MediaColor.FromRgb(255, 232, 239),
                    Error: MediaColor.FromRgb(190, 45, 70)),
                ResolvedTheme.Dark => new ThemeColors(
                    Background: MediaColor.FromRgb(22, 31, 48),
                    Foreground: MediaColor.FromRgb(248, 250, 252),
                    SecondaryForeground: MediaColor.FromRgb(172, 184, 204),
                    Card: MediaColor.FromRgb(35, 48, 72),
                    Border: MediaColor.FromRgb(58, 75, 104),
                    Accent: MediaColor.FromRgb(255, 112, 148),
                    AccentSoft: MediaColor.FromRgb(93, 45, 69),
                    Error: MediaColor.FromRgb(255, 149, 164)),
                _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "解析后的主题值无效。")
            };

            resources["WindowBackgroundBrush"] = CreateBrush(palette.Background);
            resources["ForegroundBrush"] = CreateBrush(palette.Foreground);
            resources["SecondaryForegroundBrush"] = CreateBrush(palette.SecondaryForeground);
            resources["CardBackgroundBrush"] = CreateBrush(palette.Card);
            resources["BorderBrush"] = CreateBrush(palette.Border);
            resources["AccentBrush"] = CreateBrush(palette.Accent);
            resources["AccentForegroundBrush"] = CreateBrush(theme == ResolvedTheme.Dark ? palette.Background : Colors.White);
            resources["AccentSoftBrush"] = CreateBrush(palette.AccentSoft);
            resources["ErrorBrush"] = CreateBrush(palette.Error);
            var isDark = theme == ResolvedTheme.Dark;
            resources["ControlBorderBrush"] = CreateBrush(isDark ? MediaColor.FromRgb(130, 148, 176) : MediaColor.FromRgb(116, 132, 155));
            resources["ControlHoverBrush"] = CreateBrush(isDark ? MediaColor.FromRgb(49, 65, 91) : MediaColor.FromRgb(242, 227, 234));
            resources["ControlPressedBrush"] = CreateBrush(isDark ? MediaColor.FromRgb(68, 49, 72) : MediaColor.FromRgb(236, 204, 218));
            resources["PrimaryActionBrush"] = CreateBrush(isDark ? palette.Accent : MediaColor.FromRgb(185, 38, 83));
            resources["PrimaryActionHoverBrush"] = CreateBrush(isDark ? MediaColor.FromRgb(255, 140, 168) : MediaColor.FromRgb(166, 27, 69));
            resources["PrimaryActionPressedBrush"] = CreateBrush(isDark ? MediaColor.FromRgb(241, 94, 134) : MediaColor.FromRgb(144, 18, 56));
        }

        private static SolidColorBrush CreateBrush(MediaColor color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private sealed record ThemeColors(
            MediaColor Background,
            MediaColor Foreground,
            MediaColor SecondaryForeground,
            MediaColor Card,
            MediaColor Border,
            MediaColor Accent,
            MediaColor AccentSoft,
            MediaColor Error);
    }
}
