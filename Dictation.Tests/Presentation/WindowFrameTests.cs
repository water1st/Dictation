using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Core.Configuration;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证主窗口真实外轮廓与页面内容之间的绘制边界。</summary>
    public sealed class WindowFrameTests
    {
        [Theory]
        [InlineData(ResolvedTheme.Light, 1)]
        [InlineData(ResolvedTheme.Dark, 1)]
        [InlineData(ResolvedTheme.Light, 1.25)]
        [InlineData(ResolvedTheme.Dark, 1.25)]
        [InlineData(ResolvedTheme.Light, 1.5)]
        [InlineData(ResolvedTheme.Dark, 1.5)]
        public Task Frame_OverflowingPage_KeepsThemedEdgesAndClearCorners(ResolvedTheme theme, double scale)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                var window = CreateWindow(provider);
                try
                {
                    ThemePalette.Apply(window.Resources, theme);
                    var presenter = (ContentControl)window.FindName("CurrentPagePresenter");
                    presenter.Content = new Border { Background = Brushes.Magenta, Margin = new Thickness(-40) };
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(860, 620));
                    content.Arrange(new Rect(0, 0, 860, 620));
                    content.UpdateLayout();
                    var bitmap = Render(content, scale);
                    Export(bitmap, $"overflow-{theme}-{scale}");
                    var expected = theme == ResolvedTheme.Light
                        ? Color.FromRgb(226, 232, 240)
                        : Color.FromRgb(58, 75, 104);
                    AssertEdge(bitmap, bitmap.PixelWidth / 2, 0, expected);
                    AssertEdge(bitmap, 0, bitmap.PixelHeight / 2, expected);
                    AssertEdge(bitmap, bitmap.PixelWidth - 1, bitmap.PixelHeight / 2, expected);
                    AssertEdge(bitmap, bitmap.PixelWidth / 2, bitmap.PixelHeight - 1, expected);
                    AssertPerimeter(bitmap, expected, scale);
                    foreach (var point in new[] { new Point(0, 0), new Point(bitmap.PixelWidth - 1, 0),
                        new Point(0, bitmap.PixelHeight - 1), new Point(bitmap.PixelWidth - 1, bitmap.PixelHeight - 1) })
                    {
                        Assert.Equal(0, Pixel(bitmap, (int)point.X, (int)point.Y).A);
                    }
                    Assert.Equal(Colors.Magenta, Pixel(bitmap, bitmap.PixelWidth / 2, bitmap.PixelHeight - (int)(20 * scale)));
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public Task NativeWindow_MinimizeAndRestore_PreservesRoundedRegionAndFixedSize()
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                var window = CreateWindow(provider);
                try
                {
                    window.ShowActivated = false;
                    window.Show();
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    AssertRoundedRegion(window);
                    window.WindowState = WindowState.Minimized;
                    window.RestoreAndActivate();
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(WindowState.Normal, window.WindowState);
                    AssertRoundedRegion(window);
                    Assert.Equal(860, window.Width);
                    Assert.Equal(620, window.Height);
                    Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
                    Assert.False(window.AllowsTransparency);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        private static HomeWindow CreateWindow(ServiceProvider provider)
        {
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeHome(provider.GetRequiredService<LandingViewModel>());
            var window = provider.GetRequiredService<HomeWindow>();
            foreach (var name in new[] { "Typography", "Layout", "FormControls", "NavigationCards" })
            {
                window.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/Dictation.Presentation;component/Theme/{name}.xaml", UriKind.Relative)
                });
            }
            ThemePalette.Apply(window.Resources, ResolvedTheme.Light);
            return window;
        }

        [Theory]
        [InlineData(LearningLanguage.Japanese, 0)]
        [InlineData(LearningLanguage.English, 0)]
        [InlineData(LearningLanguage.Japanese, 1)]
        [InlineData(LearningLanguage.English, 1)]
        public Task Frame_ThemeNavigationAndOverlayChanges_KeepContentInsideBorder(LearningLanguage language, int systemTheme)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                using var manager = new ThemeManager(new WindowsSystemThemeProvider(() => systemTheme, subscribeToSystemEvents: false));
                var window = CreateWindow(provider);
                try
                {
                    manager.ThemeChanged += (_, _) => ThemePalette.Apply(window.Resources, manager.CurrentTheme);
                    foreach (var entry in new[] { (typeof(LandingViewModel), typeof(HomePage)), (typeof(HomeViewModel), typeof(LanguageWorkspacePage)) })
                    {
                        var template = (DataTemplate)XamlReader.Parse($$"""
                            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                          xmlns:vm="clr-namespace:Dictation.Presentation.ViewModels;assembly=Dictation.Presentation"
                                          xmlns:views="clr-namespace:Dictation.Presentation.Views;assembly=Dictation.Presentation"
                                          DataType="{x:Type vm:{{entry.Item1.Name}}}">
                                <views:{{entry.Item2.Name}} />
                            </DataTemplate>
                            """);
                        window.Resources.Add(template.DataTemplateKey, template);
                    }
                    window.ShowActivated = false;
                    window.Show();
                    var root = provider.GetRequiredService<LandingViewModel>();
                    await root.SelectLanguageAsync(language, TestContext.Current.CancellationToken);
                    await Task.Delay(300, TestContext.Current.CancellationToken);
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    var presenter = (ContentControl)window.FindName("CurrentPagePresenter");
                    Assert.Same(root.Home, presenter.Content);
                    Assert.Equal(1, presenter.Opacity);
                    Assert.True(presenter.IsHitTestVisible);
                    Assert.True(presenter.RenderTransform is null || presenter.RenderTransform.Value.IsIdentity);
                    var page = Assert.Single(FormControlThemeTests.Children<LanguageWorkspacePage>(presenter));
                    Assert.True(page.IsVisible && page.ActualWidth > 0 && page.ActualHeight > 0);
                    Assert.Same(root.Home, page.DataContext);
                    var overlay = provider.GetRequiredService<OverlayService>();
                    var content = (FrameworkElement)window.Content;
                    foreach (var mode in new[] { Dictation.Core.Configuration.ThemeMode.Light, Dictation.Core.Configuration.ThemeMode.Dark, Dictation.Core.Configuration.ThemeMode.System })
                    {
                        manager.SetThemeMode(mode);
                        ThemePalette.Apply(window.Resources, manager.CurrentTheme);
                        var completion = overlay.ShowMessageAsync("窗口边界验收", "日语 / English 页面与弹层必须位于外框内部。");
                        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                        content.Measure(new Size(860, 620));
                        content.Arrange(new Rect(0, 0, 860, 620));
                        content.UpdateLayout();
                        var layer = (Grid)window.FindName("OverlayLayer");
                        Assert.Equal(Visibility.Visible, layer.Visibility);
                        var bounds = layer.TransformToAncestor(content).TransformBounds(new Rect(layer.RenderSize));
                        Assert.True(bounds.Left >= 1 && bounds.Right <= 859);
                        Assert.True(bounds.Top >= 65 && bounds.Bottom <= 619);
                        var bitmap = Render(content, 1);
                        var expected = manager.CurrentTheme == ResolvedTheme.Light
                            ? Color.FromRgb(226, 232, 240) : Color.FromRgb(58, 75, 104);
                        AssertEdge(bitmap, 430, 0, expected);
                        AssertEdge(bitmap, 0, 310, expected);
                        AssertEdge(bitmap, 859, 310, expected);
                        AssertEdge(bitmap, 430, 619, expected);
                        AssertPerimeter(bitmap, expected);
                        Assert.Equal(0, Pixel(bitmap, 0, 619).A);
                        Assert.Equal(0, Pixel(bitmap, 859, 619).A);
                        Export(bitmap, $"{language}-{mode}-system{systemTheme}");
                        overlay.Dismiss();
                        await completion;
                        Assert.Same(root.Home, provider.GetRequiredService<NavigationService>().CurrentPage);
                    }
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public Task NativeFrame_CaptionAndWindowButtons_PreserveHitTesting()
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                var window = CreateWindow(provider);
                try
                {
                    window.ShowActivated = false;
                    window.Show();
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(2, HitTest(window, new Point(250, 30)));
                    Assert.Equal(1, HitTest(window, new Point(1, 310)));
                    var buttons = FormControlThemeTests.Children<Button>((DependencyObject)window.Content)
                        .Where(button => new[] { "设置", "最小化", "关闭" }.Contains(AutomationProperties.GetName(button))).ToArray();
                    Assert.Equal(3, buttons.Length);
                    foreach (var button in buttons)
                    {
                        var point = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), window);
                        Assert.Equal(1, HitTest(window, point));
                        Assert.True(button.IsHitTestVisible);
                    }
                    AssertNativeFrameVisibility(window);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        private static int HitTest(Window window, Point point)
        {
            var screen = window.PointToScreen(point);
            var packed = ((int)screen.Y << 16) | ((int)screen.X & 0xffff);
            return (int)SendMessage(new WindowInteropHelper(window).Handle, 0x0084, IntPtr.Zero, new IntPtr(packed));
        }

        private static void AssertPerimeter(BitmapSource bitmap, Color expected, double scale = 1)
        {
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            // 圆弧切点附近存在正常抗锯齿覆盖，由下面的连续路径检查负责。
            var cornerExtent = (int)(12 * scale) + 2;
            for (var x = cornerExtent; x < width - cornerExtent; x++)
            {
                AssertEdge(bitmap, x, 0, expected);
                AssertEdge(bitmap, x, height - 1, expected);
            }
            for (var y = cornerExtent; y < height - cornerExtent; y++)
            {
                AssertEdge(bitmap, 0, y, expected);
                AssertEdge(bitmap, width - 1, y, expected);
            }
            // 每个角的描边必须构成连接相邻两条直边的连续像素路径。
            foreach (var corner in new[] { new Point(0, 0), new Point(width - 1, 0), new Point(0, height - 1), new Point(width - 1, height - 1) })
            {
                var pending = new Queue<(int X, int Y)>();
                var visited = new HashSet<(int X, int Y)>();
                pending.Enqueue((cornerExtent, 0));
                while (pending.TryDequeue(out var point))
                {
                    if (point.X < 0 || point.X > cornerExtent || point.Y < 0 || point.Y > cornerExtent || !visited.Add(point))
                    {
                        continue;
                    }
                    var x = corner.X == 0 ? point.X : width - 1 - point.X;
                    var y = corner.Y == 0 ? point.Y : height - 1 - point.Y;
                    var color = Pixel(bitmap, x, y);
                    if (!IsBorder(color, expected))
                    {
                        visited.Remove(point);
                        continue;
                    }
                    foreach (var step in new[] { (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1) })
                    {
                        pending.Enqueue((point.X + step.Item1, point.Y + step.Item2));
                    }
                }
                Assert.Contains((0, cornerExtent), visited);
            }
        }

        private static bool IsBorder(Color pixel, Color expected)
        {
            // 允许最多约 6% 的抗锯齿混色；先去掉预乘透明度，避免把淡边缘误判成断线。
            const int tolerance = 16;
            return pixel.A > 0 && Math.Abs(pixel.R * 255d / pixel.A - expected.R) <= tolerance
                && Math.Abs(pixel.G * 255d / pixel.A - expected.G) <= tolerance
                && Math.Abs(pixel.B * 255d / pixel.A - expected.B) <= tolerance;
        }

        private static void AssertNativeFrameVisibility(HomeWindow window)
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            var bitmap = Render((FrameworkElement)window.Content, dpi.DpiScaleX);
            var region = CreateRectRgn(0, 0, 0, 0);
            Assert.NotEqual(IntPtr.Zero, region);
            try
            {
                Assert.NotEqual(0, GetWindowRgn(new WindowInteropHelper(window).Handle, region));
                for (var y = 0; y < bitmap.PixelHeight; y++)
                {
                    for (var x = 0; x < bitmap.PixelWidth; x++)
                    {
                        if (x > 18 * dpi.DpiScaleX && x < bitmap.PixelWidth - 18 * dpi.DpiScaleX
                            && y > 18 * dpi.DpiScaleY && y < bitmap.PixelHeight - 18 * dpi.DpiScaleY)
                        {
                            continue;
                        }
                        if (Pixel(bitmap, x, y).A < 200)
                        {
                            continue;
                        }
                        // GDI 区域使用整数像素，允许与 WPF 抗锯齿边缘相差最多一个像素。
                        Assert.True(PtInRegion(region, x, y) || PtInRegion(region, x - 1, y)
                            || PtInRegion(region, x + 1, y) || PtInRegion(region, x, y - 1)
                            || PtInRegion(region, x, y + 1), $"原生区域裁掉了窗口外框 ({x},{y})。");
                    }
                }
            }
            finally
            {
                DeleteObject(region);
            }
        }

        private static void Export(BitmapSource bitmap, string name)
        {
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }
            Directory.CreateDirectory(output);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, $"frame-{name}.png"));
            encoder.Save(stream);
        }

        private static void AssertRoundedRegion(HomeWindow window)
        {
            var region = CreateRectRgn(0, 0, 0, 0);
            Assert.NotEqual(IntPtr.Zero, region);
            try
            {
                Assert.NotEqual(0, GetWindowRgn(new WindowInteropHelper(window).Handle, region));
                var dpi = VisualTreeHelper.GetDpi(window);
                var width = (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX);
                var height = (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY);
                Assert.False(PtInRegion(region, 0, 0));
                Assert.False(PtInRegion(region, width - 1, 0));
                Assert.False(PtInRegion(region, 0, height - 1));
                Assert.False(PtInRegion(region, width - 1, height - 1));
                Assert.True(PtInRegion(region, width / 2, 0));
                Assert.True(PtInRegion(region, 0, height / 2));
                Assert.True(PtInRegion(region, width / 2, height - 1));
                Assert.True(PtInRegion(region, width - 1, height / 2));
            }
            finally
            {
                DeleteObject(region);
            }
        }

        private static RenderTargetBitmap Render(FrameworkElement content, double scale)
        {
            var bitmap = new RenderTargetBitmap((int)(860 * scale), (int)(620 * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(content);
            return bitmap;
        }

        private static Color Pixel(BitmapSource bitmap, int x, int y)
        {
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }

        private static void AssertEdge(BitmapSource bitmap, int x, int y, Color expected)
        {
            var actual = Pixel(bitmap, x, y);
            Assert.True(actual.A >= 240, $"外框 ({x},{y}) 应连续可见，实际颜色为 {actual}。");
            Assert.InRange(Math.Abs(actual.R - expected.R), 0, 10);
            Assert.InRange(Math.Abs(actual.G - expected.G), 0, 10);
            Assert.InRange(Math.Abs(actual.B - expected.B), 0, 10);
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("user32.dll")]
        private static extern int GetWindowRgn(IntPtr window, IntPtr region);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PtInRegion(IntPtr region, int x, int y);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr parameter, IntPtr coordinates);
    }
}
