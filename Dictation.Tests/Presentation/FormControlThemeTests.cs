using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ThemeMode = Dictation.Core.Configuration.ThemeMode;
using Dictation.Presentation.Theme;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证共享表单模板的真实视觉状态与输入合同。</summary>
    public sealed class FormControlThemeTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Choice_ThemeChangesAndThreeStates_KeepMarksAndTextReadable(bool radio) => ReviewPlanPageTests.RunStaAsync(() =>
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
        public Task TextBox_EditingReadOnlyAndValidation_PreserveInputHostAndTheme(bool multiline) => ReviewPlanPageTests.RunStaAsync(() =>
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
        public Task ComboBox_EditingAndSelection_PreservePopupAndThemedItems(bool editable) => ReviewPlanPageTests.RunStaAsync(() =>
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task ComboBox_OpenPopup_UpdatesRealItemsAndAccessibleSelection(bool editable) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var values = new[] { "日本語", "English" };
            var selection = new ListBox { ItemsSource = values, SelectedIndex = 0 };
            var combo = new ComboBox { IsEditable = editable, ItemsSource = values };
            combo.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedItem") { Source = selection, Mode = BindingMode.TwoWay });
            var host = CreateHost(combo);
            using var source = new HwndSource(new HwndSourceParameters("表单弹出框验收") { Width = 600, Height = 300 });
            source.RootVisual = host;
            ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
            Layout(host);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            combo.IsDropDownOpen = true;
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
            var popup = Assert.IsType<Popup>(combo.Template.FindName("PART_Popup", combo));
            Assert.True(popup.IsOpen);
            var item = Assert.IsType<ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(1));
            foreach (var theme in new[] { ResolvedTheme.Dark, ResolvedTheme.Light })
            {
                ThemePalette.Apply(host.Resources, theme);
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                Assert.Equal(ColorOf(host, "ForegroundBrush"), ((SolidColorBrush)item.Foreground).Color);
                Assert.Equal(ColorOf(host, "CardBackgroundBrush"), ((SolidColorBrush)((Border)popup.Child).Background).Color);
            }
            var peer = UIElementAutomationPeer.CreatePeerForElement(combo)!;
            var itemPeer = Assert.Single(peer.GetChildren(), child => child.GetName() == "English");
            var provider = Assert.IsAssignableFrom<ISelectionItemProvider>(itemPeer.GetPattern(PatternInterface.SelectionItem));
            provider.Select();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            Assert.Equal("English", combo.SelectedItem);
            Assert.Equal("English", selection.SelectedItem);
            Assert.True(provider.IsSelected);
            combo.IsDropDownOpen = false;
            Assert.False(popup.IsOpen);
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task ComboBox_LongItems_PopupStaysWithinControlWidth(bool editable) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var combo = new ComboBox
            {
                IsEditable = editable,
                Width = 180,
                ItemsSource = new[] { new string('长', 100) }
            };
            var host = CreateHost(combo);
            using var source = new HwndSource(new HwndSourceParameters("下拉宽度验收") { Width = 600, Height = 300 });
            source.RootVisual = host;
            ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
            Layout(host);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            combo.IsDropDownOpen = true;
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            Assert.True(popup.IsOpen);
            var popupBorder = Assert.IsType<Border>(popup.Child);
            Assert.True(popupBorder.ActualWidth <= combo.ActualWidth + 1,
                $"下拉层宽度 {popupBorder.ActualWidth} 不得超出输入框 {combo.ActualWidth} 并遮挡相邻操作。");
            combo.IsDropDownOpen = false;
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task TextBox_ComposedTextInput_PreservesEditingAndReadOnly(bool multiline) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var input = new TextBox { AcceptsReturn = multiline };
            var host = CreateHost(input);
            ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
            Layout(host);
            input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,
                new TextComposition(InputManager.Current, input, "食べる"))
            {
                RoutedEvent = TextCompositionManager.TextInputEvent
            });
            Assert.Equal("食べる", input.Text);
            input.IsReadOnly = true;
            input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,
                new TextComposition(InputManager.Current, input, "変更"))
            {
                RoutedEvent = TextCompositionManager.TextInputEvent
            });
            Assert.Equal("食べる", input.Text);
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public Task Choices_SwitchBackToSystem_RenderResolvedTheme(int systemTheme) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            using var provider = new WindowsSystemThemeProvider(() => systemTheme, subscribeToSystemEvents: false);
            using var manager = new ThemeManager(provider);
            var choice = new CheckBox { Content = "主题", IsChecked = true };
            var host = CreateHost(choice);
            ThemePalette.Apply(host.Resources, manager.CurrentTheme);
            manager.ThemeChanged += (_, _) => ThemePalette.Apply(host.Resources, manager.CurrentTheme);
            foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
            {
                manager.SetThemeMode(mode);
                Layout(host);
                var expected = mode == ThemeMode.Dark || mode == ThemeMode.System && systemTheme == 0
                    ? Color.FromRgb(248, 250, 252)
                    : Color.FromRgb(31, 41, 55);
                var content = Assert.Single(Children<ContentPresenter>(choice));
                Assert.Equal(expected, ((SolidColorBrush)content.GetValue(TextElement.ForegroundProperty)).Color);
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false, 12)]
        [InlineData(false, 16)]
        [InlineData(false, 24)]
        [InlineData(true, 12)]
        [InlineData(true, 16)]
        [InlineData(true, 24)]
        public Task Choice_LabelAndIndicator_AlignVerticallyAcrossFontSizes(bool radio, double fontSize) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            ToggleButton choice = radio ? new RadioButton() : new CheckBox();
            choice.Content = "每日复习 English";
            choice.FontSize = fontSize;
            var host = CreateHost(choice);
            ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
            Layout(host);
            var indicator = (Border)choice.Template.FindName("Indicator", choice);
            var label = Assert.Single(Children<TextBlock>(choice));
            var indicatorCenter = indicator.TranslatePoint(new Point(0, indicator.ActualHeight / 2), choice).Y;
            var labelCenter = label.TranslatePoint(new Point(0, label.ActualHeight / 2), choice).Y;
            Assert.True(Math.Abs(indicatorCenter - labelCenter) <= 1,
                $"字号 {fontSize} 下标记中心 {indicatorCenter} 与文字中心 {labelCenter} 应对齐。");
            return Task.CompletedTask;
        });

        [Fact]
        public Task Choices_DefaultStyles_UseOneStandardFontSize() => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var checkbox = new CheckBox { Content = "全选" };
            var radio = new RadioButton { Content = "每天复习" };
            var host = CreateHost(checkbox);
            host.Children.Add(radio);
            ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
            Layout(host);
            Assert.Equal(16d, checkbox.FontSize);
            Assert.Equal(16d, radio.FontSize);
            return Task.CompletedTask;
        });

        internal static StackPanel CreateHost(UIElement child)
        {
            var host = new StackPanel { Width = 420 };
            host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Dictation.Presentation;component/Theme/Typography.xaml", UriKind.Relative) });
            host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Dictation.Presentation;component/Theme/Buttons.xaml", UriKind.Relative) });
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
