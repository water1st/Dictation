using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.Presentation.Behaviors;
using Dictation.Presentation.Theme;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证共享模板在真实滚动容器、输入控件和虚拟列表中的集成。</summary>
    public sealed class ScrollBarInteractionTests
    {
        [Fact]
        public Task Viewer_OverflowThenShortContent_HidesUnusedBarsAndReclaimsViewport()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var content = new Border { Width = 900, Height = 1200 };
                var viewer = CreateViewer(content);
                var host = CreateHost(viewer);
                Assert.Equal(Visibility.Visible, viewer.ComputedVerticalScrollBarVisibility);
                Assert.Equal(Visibility.Visible, viewer.ComputedHorizontalScrollBarVisibility);
                AssertThemedBars(viewer, 2);
                content.Width = 80;
                content.Height = 40;
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(Visibility.Collapsed, viewer.ComputedVerticalScrollBarVisibility);
                Assert.Equal(Visibility.Collapsed, viewer.ComputedHorizontalScrollBarVisibility);
                Assert.InRange(viewer.ViewportWidth, 278, 280);
                Assert.InRange(viewer.ViewportHeight, 218, 220);
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(Orientation.Vertical)]
        [InlineData(Orientation.Horizontal)]
        public Task Viewer_PageCommandsAndThumbDelta_PreserveScrollDirection(Orientation orientation)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var viewer = CreateViewer(new Border { Width = 1500, Height = 1500 });
                var host = CreateHost(viewer);
                var bar = FormControlThemeTests.Children<ScrollBar>(viewer).Single(item => item.Orientation == orientation);
                var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
                Execute(track.IncreaseRepeatButton);
                ScrollBarThemeTests.Layout(host);
                var first = Offset(viewer, orientation);
                Assert.True(first > 0);
                Execute(track.DecreaseRepeatButton);
                ScrollBarThemeTests.Layout(host);
                Assert.True(Offset(viewer, orientation) < first);
                track.Thumb.RaiseEvent(new DragDeltaEventArgs(orientation == Orientation.Horizontal ? 20 : 0,
                    orientation == Orientation.Vertical ? 20 : 0) { RoutedEvent = Thumb.DragDeltaEvent });
                ScrollBarThemeTests.Layout(host);
                Assert.True(Offset(viewer, orientation) > 0);
                AssertThemedBars(viewer, 2);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Viewer_WheelKeyboardAndRecreation_PreserveBoundOffset()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var saved = new Slider { Value = 100, Maximum = 2000 };
                var viewer = CreateViewer(new Border { Width = 100, Height = 1800 });
                viewer.SetBinding(ScrollViewerOffsetBehavior.VerticalOffsetProperty,
                    new Binding("Value") { Source = saved, Mode = BindingMode.TwoWay });
                var host = CreateHost(viewer);
                using var source = new HwndSource(new HwndSourceParameters("滚动输入验收") { Width = 300, Height = 260 });
                source.RootVisual = host;
                ScrollBarThemeTests.Layout(host);
                Drain();
                Assert.Equal(100, viewer.VerticalOffset);
                viewer.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.MouseWheelEvent });
                ScrollBarThemeTests.Layout(host);
                Assert.True(viewer.VerticalOffset > 100);
                var wheelOffset = viewer.VerticalOffset;
                // 小增量与滚轮共用 WPF 输入路径，不能代替实体触控板验收。
                viewer.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -30) { RoutedEvent = Mouse.MouseWheelEvent });
                ScrollBarThemeTests.Layout(host);
                Assert.True(viewer.VerticalOffset >= wheelOffset);
                Press(viewer, source, Key.PageDown);
                ScrollBarThemeTests.Layout(host);
                Assert.True(viewer.VerticalOffset > wheelOffset);
                var pageOffset = viewer.VerticalOffset;
                Press(viewer, source, Key.PageUp);
                ScrollBarThemeTests.Layout(host);
                Assert.True(viewer.VerticalOffset < pageOffset);
                ScrollBar.ScrollToBottomCommand.Execute(null, viewer);
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(viewer.ScrollableHeight, viewer.VerticalOffset);
                ScrollBar.ScrollToTopCommand.Execute(null, viewer);
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(0, viewer.VerticalOffset);
                viewer.ScrollToVerticalOffset(240);
                ScrollBarThemeTests.Layout(host);
                Drain();
                Assert.Equal(240, saved.Value);
                host.Children.Remove(viewer);
                var restored = CreateViewer(new Border { Width = 100, Height = 1800 });
                restored.SetBinding(ScrollViewerOffsetBehavior.VerticalOffsetProperty,
                    new Binding("Value") { Source = saved, Mode = BindingMode.TwoWay });
                host.Children.Add(restored);
                ScrollBarThemeTests.Layout(host);
                Drain();
                Assert.Equal(240, restored.VerticalOffset);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task ListBox_LongList_KeepsVirtualizationAndScrollsToLastItem()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var list = new ListBox { Width = 280, Height = 220, ItemsSource = Enumerable.Range(0, 5000).Select(value => $"词条 {value}").ToArray() };
                ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
                var host = CreateHost(list);
                AssertThemedBars(list, 1);
                Assert.InRange(FormControlThemeTests.Children<ListBoxItem>(list).Count(), 1, 100);
                list.ScrollIntoView(list.Items[4999]);
                ScrollBarThemeTests.Layout(host);
                Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(4999));
                Assert.InRange(FormControlThemeTests.Children<ListBoxItem>(list).Count(), 1, 100);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task TextBox_MultilineOverflow_UsesBothThemedBarsAndPreservesEditing()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var text = new TextBox { Width = 280, Height = 180, AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Text = string.Join("\r\n", Enumerable.Repeat(new string('词', 80), 100)) };
                var host = CreateHost(text);
                AssertThemedBars(text, 2);
                text.ScrollToEnd();
                ScrollBarThemeTests.Layout(host);
                Assert.True(text.VerticalOffset > 0);
                text.SelectAll();
                text.SelectedText = "编辑后文本";
                ScrollBarThemeTests.Layout(host);
                Assert.Equal("编辑后文本", text.Text);
                Assert.Equal(0, text.VerticalOffset);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task ComboBox_LongOpenPopup_UsesThemedScrollbarAndUpdatesWithTheme()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var combo = new ComboBox { Width = 240, MaxDropDownHeight = 120,
                    ItemsSource = Enumerable.Range(0, 100).Select(value => $"候选 {value}").ToArray() };
                var host = CreateHost(combo);
                using var source = new HwndSource(new HwndSourceParameters("下拉滚动验收") { Width = 300, Height = 260 });
                source.RootVisual = host;
                ScrollBarThemeTests.Layout(host);
                Drain();
                combo.IsDropDownOpen = true;
                Drain();
                try
                {
                    var popup = Assert.IsType<Popup>(combo.Template.FindName("PART_Popup", combo));
                    Assert.True(popup.IsOpen);
                    AssertThemedBars(popup.Child, 1);
                    var thumb = FormControlThemeTests.Children<Thumb>(popup.Child).Single();
                    foreach (var theme in new[] { ResolvedTheme.Dark, ResolvedTheme.Light })
                    {
                        ThemePalette.Apply(host.Resources, theme);
                        Drain();
                        var expected = theme == ResolvedTheme.Dark ? Color.FromRgb(172, 184, 204) : Color.FromRgb(100, 116, 139);
                        Assert.Contains(FormControlThemeTests.Children<Border>(thumb), border =>
                            border.Background is SolidColorBrush brush && brush.Color == expected);
                    }
                    combo.SelectedIndex = 99;
                    Assert.Equal("候选 99", combo.SelectedItem);
                }
                finally
                {
                    combo.IsDropDownOpen = false;
                }
                return Task.CompletedTask;
            });
        }

        internal static void AssertThemedBars(DependencyObject root, int minimumVisible)
        {
            var bars = FormControlThemeTests.Children<ScrollBar>(root).Where(bar => bar.Visibility == Visibility.Visible).ToArray();
            Assert.True(bars.Length >= minimumVisible, $"可见滚动条数 {bars.Length} 少于预期 {minimumVisible}。");
            foreach (var bar in bars)
            {
                var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
                Assert.Contains(FormControlThemeTests.Children<Border>(track.Thumb), border => border.CornerRadius.TopLeft >= 3);
            }
        }

        private static ScrollViewer CreateViewer(UIElement content)
        {
            return new ScrollViewer { Width = 280, Height = 220, Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private static Grid CreateHost(UIElement content)
        {
            var host = ScrollBarThemeTests.CreateHost(content);
            ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
            ScrollBarThemeTests.Layout(host);
            return host;
        }

        private static double Offset(ScrollViewer viewer, Orientation orientation)
        {
            return orientation == Orientation.Vertical ? viewer.VerticalOffset : viewer.HorizontalOffset;
        }

        private static void Execute(RepeatButton button)
        {
            var command = Assert.IsType<RoutedCommand>(button.Command);
            Assert.True(command.CanExecute(button.CommandParameter, button.CommandTarget));
            command.Execute(button.CommandParameter, button.CommandTarget);
        }

        private static void Press(UIElement target, HwndSource source, Key key)
        {
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }

        private static void Drain()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        }
    }
}
