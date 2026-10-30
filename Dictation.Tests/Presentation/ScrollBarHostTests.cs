using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Dictation.Presentation.Behaviors;
using Dictation.Presentation.Controls;
using Dictation.Presentation.Theme;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证固定页头与嵌入页面的主轨道统一在宿主右缘。</summary>
    public sealed class ScrollBarHostTests
    {
        [Theory]
        [InlineData(32)]
        [InlineData(80)]
        public Task Host_InsetPageViewer_PlacesOnlyMainBarAtWindowRightEdge(double inset)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var viewer = CreateViewer();
                var page = new Grid { Margin = new Thickness(inset, 24, inset, 24) };
                page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
                page.RowDefinitions.Add(new RowDefinition());
                var heading = new TextBlock { Text = "固定页头" };
                page.Children.Add(heading);
                Grid.SetRow(viewer, 1);
                page.Children.Add(viewer);
                var shell = new PageScrollHost { Content = page };
                var host = CreateHost(shell);
                var bar = MainBar(shell);
                Assert.Equal(ScrollBarVisibility.Hidden, viewer.VerticalScrollBarVisibility);
                var bounds = bar.TransformToAncestor(shell).TransformBounds(new Rect(bar.RenderSize));
                Assert.Equal(shell.ActualWidth, bounds.Right, 3);
                Assert.Equal(0, bounds.Top, 3);
                Assert.Equal(shell.ActualHeight, bounds.Bottom, 3);
                Assert.InRange(bounds.Width, 10, 14);
                bar.Value = 120;
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(120, viewer.VerticalOffset);
                Assert.Equal(0, heading.TranslatePoint(new Point(), page).Y);
                viewer.ScrollToBottom();
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(viewer.ScrollableHeight, bar.Value, 3);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Host_ContentChanges_RestoresOldViewerAndHidesUnneededMainBar()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var first = CreateViewer();
                var shell = new PageScrollHost { Content = first };
                var host = CreateHost(shell);
                var bar = MainBar(shell);
                Assert.Equal(Visibility.Visible, bar.Visibility);
                var second = CreateViewer();
                second.Content = new Border { Height = 20 };
                shell.Content = second;
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(ScrollBarVisibility.Auto, first.VerticalScrollBarVisibility);
                Assert.Equal(Visibility.Collapsed, bar.Visibility);
                Assert.Equal(shell.ActualWidth, second.ActualWidth, 3);
                ((Border)second.Content).Height = 2000;
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(Visibility.Visible, bar.Visibility);
                bar.Value = 100;
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(100, second.VerticalOffset);
                Assert.Equal(0, first.VerticalOffset);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Host_NestedListAndBoundOffset_PreservesLocalScrollingAndMainPosition()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var list = new ListBox { Height = 120, ItemsSource = Enumerable.Range(0, 5000).ToArray() };
                var content = new StackPanel();
                content.Children.Add(list);
                content.Children.Add(new Border { Height = 1800 });
                var viewer = CreateViewer();
                viewer.Content = content;
                var saved = new Slider { Maximum = 2000, Value = 80 };
                viewer.SetBinding(ScrollViewerOffsetBehavior.VerticalOffsetProperty,
                    new Binding("Value") { Source = saved, Mode = BindingMode.TwoWay });
                var shell = new PageScrollHost { Content = viewer };
                var host = CreateHost(shell);
                var bar = MainBar(shell);
                Assert.Equal(80, viewer.VerticalOffset);
                Assert.Equal(80, bar.Value);
                ScrollBarInteractionTests.AssertThemedBars(list, 1);
                Assert.InRange(FormControlThemeTests.Children<ListBoxItem>(list).Count(), 1, 100);
                var local = FormControlThemeTests.Children<ScrollViewer>(list).Single();
                local.ScrollToVerticalOffset(50);
                ScrollBarThemeTests.Layout(host);
                Assert.True(local.VerticalOffset > 0);
                Assert.Equal(80, viewer.VerticalOffset);
                bar.Value = 180;
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(180, saved.Value);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Host_WheelOverMainRail_UsesViewerInputInBothDirectionsAndRespectsDisabled()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var viewer = CreateViewer();
                var shell = new PageScrollHost { Content = viewer };
                var host = CreateHost(shell);
                var bar = MainBar(shell);
                var down = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.MouseWheelEvent };
                bar.RaiseEvent(down);
                ScrollBarThemeTests.Layout(host);
                Assert.True(down.Handled);
                Assert.True(viewer.VerticalOffset > 0);
                var offset = viewer.VerticalOffset;
                bar.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent });
                ScrollBarThemeTests.Layout(host);
                Assert.True(viewer.VerticalOffset < offset);
                Assert.Equal(0, viewer.VerticalOffset);
                bar.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent });
                ScrollBarThemeTests.Layout(host);
                Assert.Equal(0, viewer.VerticalOffset);
                viewer.IsEnabled = false;
                bar.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.MouseWheelEvent });
                ScrollBarThemeTests.Layout(host);
                Assert.False(bar.IsEnabled);
                Assert.Equal(0, viewer.VerticalOffset);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Host_KeyboardFocusInPageViewer_UpdatesMainThumbOutline()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var input = new TextBox { Height = 40 };
                var content = new StackPanel();
                content.Children.Add(input);
                content.Children.Add(new Border { Height = 2000 });
                var viewer = CreateViewer();
                viewer.Content = content;
                var outside = new TextBox { Height = 30 };
                var page = new DockPanel();
                DockPanel.SetDock(outside, Dock.Top);
                page.Children.Add(outside);
                page.Children.Add(viewer);
                var shell = new PageScrollHost { Content = page };
                var host = CreateHost(shell);
                using var source = new HwndSource(new HwndSourceParameters("页面主轨道焦点验收") { Width = 860, Height = 556 });
                source.RootVisual = host;
                ScrollBarThemeTests.Layout(host);
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Assert.Same(input, Keyboard.Focus(input));
                ScrollBarThemeTests.Layout(host);
                Assert.True(viewer.IsKeyboardFocusWithin);
                var track = (Track)MainBar(shell).Template.FindName("PART_Track", MainBar(shell));
                var body = Assert.Single(FormControlThemeTests.Children<Border>(track.Thumb));
                Assert.Equal(Color.FromRgb(255, 112, 148), Assert.IsType<SolidColorBrush>(body.BorderBrush).Color);
                Assert.Same(outside, Keyboard.Focus(outside));
                ScrollBarThemeTests.Layout(host);
                Assert.False(viewer.IsKeyboardFocusWithin);
                Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(body.BorderBrush).Color);
                return Task.CompletedTask;
            });
        }

        private static ScrollViewer CreateViewer()
        {
            var viewer = new ScrollViewer { Content = new Border { Height = 2000 },
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            PageScrollHost.SetIsPageScrollViewer(viewer, true);
            return viewer;
        }

        private static Grid CreateHost(PageScrollHost shell)
        {
            var host = ScrollBarThemeTests.CreateHost(shell);
            host.Width = 860;
            host.Height = 556;
            ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
            ScrollBarThemeTests.Layout(host);
            return host;
        }

        private static ScrollBar MainBar(PageScrollHost shell)
        {
            return Assert.IsType<ScrollBar>(shell.Template.FindName("PART_PageScrollBar", shell));
        }
    }
}
