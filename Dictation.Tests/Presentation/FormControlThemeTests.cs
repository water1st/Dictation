using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Dictation.Presentation.Theme;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证共享表单模板的真实视觉状态与输入合同。</summary>
    public sealed class FormControlThemeTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task 选项_主题切换与三态_标记和文字保持可读(bool radio) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            ToggleButton choice = radio ? new RadioButton() : new CheckBox { IsThreeState = true };
            choice.Content = "测试选项";
            var host = CreateHost(choice);
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(host.Resources, theme);
                Layout(host);
                var mark = Assert.Single(Children<FrameworkElement>(choice), element => element.Name == "CheckedMark");
                choice.IsChecked = false;
                Assert.Equal(Visibility.Collapsed, mark.Visibility);
                choice.IsChecked = true;
                Assert.Equal(Visibility.Visible, mark.Visibility);
                var content = Assert.Single(Children<ContentPresenter>(choice));
                Assert.Equal(ColorOf(host, "ForegroundBrush"), ((SolidColorBrush)content.GetValue(TextElement.ForegroundProperty)).Color);
                choice.IsEnabled = false;
                Assert.Contains(Children<Border>(choice), border => border.Opacity < 1);
                choice.IsEnabled = true;
                if (!radio)
                {
                    choice.IsChecked = null;
                    var dash = Assert.Single(Children<FrameworkElement>(choice), element => element.Name == "IndeterminateMark");
                    Assert.Equal(Visibility.Visible, dash.Visibility);
                    Assert.Equal(Visibility.Collapsed, mark.Visibility);
                }
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task 文本框_编辑只读和错误_保留输入宿主与主题(bool multiline) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var source = new TextBox { Text = "初始文本" };
            var input = new TextBox { AcceptsReturn = multiline, Height = multiline ? 100 : 40 };
            input.SetBinding(TextBox.TextProperty, new Binding("Text") { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            var host = CreateHost(input);
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(host.Resources, theme);
                Layout(host);
                Assert.IsType<ScrollViewer>(input.Template.FindName("PART_ContentHost", input));
                Assert.Contains(Children<Border>(input), border => border.CornerRadius.TopLeft > 0);
                Assert.Equal(ColorOf(host, "ForegroundBrush"), ((SolidColorBrush)input.Foreground).Color);
                Assert.Equal(ColorOf(host, "AccentBrush"), ((SolidColorBrush)input.CaretBrush).Color);
                input.SetCurrentValue(TextBox.TextProperty, multiline ? "日本語\r\nEnglish" : "日本語 English");
                Assert.Equal(input.Text, source.Text);
                input.SelectAll();
                Assert.Equal(input.Text, input.SelectedText);
                input.IsReadOnly = true;
                Assert.True(input.IsReadOnly);
                var expression = input.GetBindingExpression(TextBox.TextProperty)!;
                Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "输入错误", null));
                Layout(host);
                Assert.Contains(Children<Border>(input), border => border.BorderBrush is SolidColorBrush brush && brush.Color == ColorOf(host, "ErrorBrush"));
                Validation.ClearInvalid(expression);
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task 下拉框_编辑与选择_保留弹出宿主及主题项(bool editable) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var combo = new ComboBox { IsEditable = editable, ItemsSource = new[] { "日本語", "English" }, SelectedIndex = 0 };
            var host = CreateHost(combo);
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(host.Resources, theme);
                Layout(host);
                Assert.IsType<Popup>(combo.Template.FindName("PART_Popup", combo));
                Assert.Contains(Children<Border>(combo), border => border.CornerRadius.TopLeft > 0);
                if (editable)
                {
                    var input = Assert.IsType<TextBox>(combo.Template.FindName("PART_EditableTextBox", combo));
                    input.Text = "検索";
                    Assert.Equal("検索", combo.Text);
                }
                combo.SelectedIndex = 1;
                Assert.Equal("English", combo.SelectedItem);
                var item = new ComboBoxItem { Content = "候选", IsSelected = true };
                host.Children.Add(item);
                Layout(host);
                Assert.Contains(Children<Border>(item), border => border.Background is SolidColorBrush brush && brush.Color == ColorOf(host, "AccentSoftBrush"));
                host.Children.Remove(item);
            }
            return Task.CompletedTask;
        });

        internal static StackPanel CreateHost(UIElement child)
        {
            var host = new StackPanel { Width = 420 };
            host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Dictation.Presentation;component/Theme/FormControls.xaml", UriKind.Relative) });
            host.Children.Add(child);
            return host;
        }

        internal static void Layout(FrameworkElement host)
        {
            host.Measure(new Size(600, 600));
            host.Arrange(new Rect(0, 0, 600, 600));
            host.UpdateLayout();
        }

        private static Color ColorOf(FrameworkElement host, string key)
        {
            return ((SolidColorBrush)host.Resources[key]).Color;
        }

        internal static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item)
            {
                yield return item;
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }
    }
}
