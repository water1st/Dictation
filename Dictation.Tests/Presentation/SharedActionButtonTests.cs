using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;
using Dictation.Presentation.Theme;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>检查主次操作的文字对比与常用命令的矢量图标。</summary>
    public sealed class SharedActionButtonTests
    {
        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task Buttons_EnabledThemes_KeepReadableForeground(ResolvedTheme theme)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                foreach (var key in new[] { "PrimaryActionButtonStyle", "SecondaryActionButtonStyle" })
                {
                    var button = new Button { Content = "保存计划" };
                    var host = FormControlThemeTests.CreateHost(button);
                    button.Style = (Style)host.Resources[key];
                    ThemePalette.Apply(host.Resources, theme);
                    FormControlThemeTests.Layout(host);
                    var label = Assert.Single(FormControlThemeTests.Children<TextBlock>(button));
                    var foreground = ((SolidColorBrush)label.Foreground).Color;
                    var background = ((SolidColorBrush)button.Background).Color;
                    var ratio = (Math.Max(Luminance(foreground), Luminance(background)) + 0.05)
                        / (Math.Min(Luminance(foreground), Luminance(background)) + 0.05);
                    Assert.True(ratio >= 4.5, $"{theme} {key} 的文字对比度为 {ratio:F2}，应至少为 4.5。");
                    Assert.Equal(1, button.Opacity);
                }
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(typeof(LearningPlanPage))]
        [InlineData(typeof(ReviewPlanPage))]
        [InlineData(typeof(KanaPlanPage))]
        [InlineData(typeof(LexiconLookupPage))]
        public Task Toolbar_CommonCommands_ShowVectorIconsAndReadableLabels(Type pageType)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var page = (UserControl)Activator.CreateInstance(pageType)!;
                var host = FormControlThemeTests.CreateHost(page);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
                FormControlThemeTests.Layout(host);
                var buttons = FormControlThemeTests.Children<Button>(page).Where(button =>
                    button.GetBindingExpression(Button.CommandProperty)?.ParentBinding.Path?.Path is "RefreshCommand" or "SearchCommand" or "SaveCommand").ToArray();
                Assert.NotEmpty(buttons);
                foreach (var button in buttons)
                {
                    Assert.Contains(FormControlThemeTests.Children<Path>(button), path => path.Data is not null && path.ActualWidth > 0);
                    Assert.Contains(FormControlThemeTests.Children<TextBlock>(button), label => !string.IsNullOrWhiteSpace(label.Text));
                }
                return Task.CompletedTask;
            });
        }

        private static double Luminance(Color color)
        {
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }

        private static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
    }
}
