using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
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
                    var expected = theme == ResolvedTheme.Light
                        ? Color.FromRgb(226, 232, 240)
                        : Color.FromRgb(58, 75, 104);
                    AssertEdge(bitmap, bitmap.PixelWidth / 2, 0, expected);
                    AssertEdge(bitmap, 0, bitmap.PixelHeight / 2, expected);
                    AssertEdge(bitmap, bitmap.PixelWidth - 1, bitmap.PixelHeight / 2, expected);
                    AssertEdge(bitmap, bitmap.PixelWidth / 2, bitmap.PixelHeight - 1, expected);
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
    }
}
