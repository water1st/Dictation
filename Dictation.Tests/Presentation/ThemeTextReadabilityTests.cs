using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.Presentation.Theme;

namespace Dictation.Tests.Presentation
{
    /// <summary>检查真正绘制文字的模板子元素，保留主次文字的语义颜色。</summary>
    public sealed class ThemeTextReadabilityTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task PrimaryButton_DarkTheme_KeepsSemanticTextColorAcrossEnabledStates(bool disabled) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var button = new Button { Content = "保存计划 日本語 English", IsEnabled = !disabled };
            var host = FormControlThemeTests.CreateHost(button);
            button.Style = (Style)host.Resources["PrimaryActionButtonStyle"];
            ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
            FormControlThemeTests.Layout(host);
            var text = Assert.Single(FormControlThemeTests.Children<TextBlock>(button));
            var foreground = Assert.IsType<SolidColorBrush>(text.Foreground).Color;
            var background = Assert.IsType<SolidColorBrush>(button.Background).Color;
            Assert.Equal(Color.FromRgb(22, 31, 48), foreground);
            if (!disabled)
            {
                var contrast = Contrast(foreground, background);
                Assert.True(contrast >= 4.5, $"正常主按钮实际文字对比度仅为 {contrast:F2}。");
            }
            else
            {
                Assert.Equal(0.55, button.Opacity);
            }
            return Task.CompletedTask;
        });

        [Fact]
        public Task PrimaryButton_ThemeChanges_RefreshesExistingTextAndPreservesLightAppearance() => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var button = new Button { Content = "保存" };
            var host = FormControlThemeTests.CreateHost(button);
            button.Style = (Style)host.Resources["PrimaryActionButtonStyle"];
            TextBlock? original = null;
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark, ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(host.Resources, theme);
                FormControlThemeTests.Layout(host);
                var text = Assert.Single(FormControlThemeTests.Children<TextBlock>(button));
                original ??= text;
                Assert.Same(original, text);
                var expected = theme == ResolvedTheme.Light ? Colors.White : Color.FromRgb(22, 31, 48);
                Assert.Equal(expected, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
                var presenter = Assert.Single(FormControlThemeTests.Children<ContentPresenter>(button));
                Assert.Equal(expected, Assert.IsType<SolidColorBrush>(presenter.GetValue(TextElement.ForegroundProperty)).Color);
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task ComboBox_OpenPopup_RefreshesRenderedTextWithoutReopening(bool editable) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var combo = new ComboBox { IsEditable = editable, ItemsSource = new[] { "日本語", "English" }, SelectedIndex = 0 };
            var host = FormControlThemeTests.CreateHost(combo);
            using var source = new HwndSource(new HwndSourceParameters("弹层文字主题验收") { Width = 600, Height = 600 });
            source.RootVisual = host;
            ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
            FormControlThemeTests.Layout(host);
            combo.IsDropDownOpen = true;
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            var popup = Assert.IsType<Popup>(combo.Template.FindName("PART_Popup", combo));
            Assert.True(popup.IsOpen);
            var item = Assert.IsType<ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(1));
            foreach (var theme in new[] { ResolvedTheme.Dark, ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(host.Resources, theme);
                FormControlThemeTests.Layout(host);
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                Assert.True(popup.IsOpen);
                Assert.Same(item, combo.ItemContainerGenerator.ContainerFromIndex(1));
                var expected = ((SolidColorBrush)host.Resources["ForegroundBrush"]).Color;
                var texts = FormControlThemeTests.Children<TextBlock>(item).Where(text => text.Text == "English").ToArray();
                Assert.NotEmpty(texts);
                Assert.All(texts, text => Assert.Equal(expected, Assert.IsType<SolidColorBrush>(text.Foreground).Color));
            }
            combo.IsDropDownOpen = false;
            return Task.CompletedTask;
        });

        private static double Contrast(Color first, Color second)
        {
            var firstLuminance = Luminance(first);
            var secondLuminance = Luminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
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
