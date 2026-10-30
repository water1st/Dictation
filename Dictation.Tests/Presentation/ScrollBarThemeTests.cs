using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Dictation.Presentation.Theme;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证应用实际合并的滚动条资源及其可见行为。</summary>
    public sealed class ScrollBarThemeTests
    {
        [Theory]
        [InlineData(Orientation.Vertical, ResolvedTheme.Light)]
        [InlineData(Orientation.Vertical, ResolvedTheme.Dark)]
        [InlineData(Orientation.Horizontal, ResolvedTheme.Light)]
        [InlineData(Orientation.Horizontal, ResolvedTheme.Dark)]
        public Task Bar_ApplicationResources_RenderCompactRoundedThemedThumb(Orientation orientation, ResolvedTheme theme)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var bar = new ScrollBar { Orientation = orientation, Minimum = 0, Maximum = 100, Value = 25, ViewportSize = 25 };
                var host = CreateHost(bar);
                ThemePalette.Apply(host.Resources, theme);
                Layout(host);
                Export(host, $"bar-{orientation}-{theme}");
                var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
                Assert.NotNull(track.Thumb);
                var borders = FormControlThemeTests.Children<Border>(track.Thumb).ToArray();
                var expected = theme == ResolvedTheme.Light ? Color.FromRgb(100, 116, 139) : Color.FromRgb(172, 184, 204);
                Assert.Contains(borders, border => border.Background is SolidColorBrush brush && brush.Color == expected
                    && border.CornerRadius.TopLeft >= 3);
                var thickness = orientation == Orientation.Vertical ? bar.ActualWidth : bar.ActualHeight;
                Assert.InRange(thickness, 10, 14);
                Assert.True(track.DecreaseRepeatButton.IsHitTestVisible);
                Assert.True(track.IncreaseRepeatButton.IsHitTestVisible);
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(Orientation.Vertical)]
        [InlineData(Orientation.Horizontal)]
        public Task Bar_ThemeAndDisabledChanges_UpdateExistingThumbWithoutLayoutShift(Orientation orientation)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var bar = new ScrollBar { Orientation = orientation, Maximum = 100, Value = 25, ViewportSize = 25 };
                var host = CreateHost(bar);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
                Layout(host);
                var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
                var thumb = track.Thumb;
                var size = bar.RenderSize;
                ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
                Layout(host);
                Assert.Same(thumb, track.Thumb);
                Assert.Contains(FormControlThemeTests.Children<Border>(thumb), border =>
                    border.Background is SolidColorBrush brush && brush.Color == Color.FromRgb(172, 184, 204));
                var root = Assert.Single(FormControlThemeTests.Children<Grid>(bar), element => element.Name == "Root");
                var enabledOpacity = root.Opacity;
                bar.IsEnabled = false;
                Layout(host);
                Assert.False(thumb.IsEnabled);
                Assert.True(root.Opacity < enabledOpacity);
                Assert.Equal(size, bar.RenderSize);
                bar.IsEnabled = true;
                Assert.Equal(enabledOpacity, root.Opacity);
                Assert.Equal(25, bar.Value);
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(Orientation.Vertical, 1)]
        [InlineData(Orientation.Vertical, 1.25)]
        [InlineData(Orientation.Vertical, 1.5)]
        [InlineData(Orientation.Horizontal, 1)]
        [InlineData(Orientation.Horizontal, 1.25)]
        [InlineData(Orientation.Horizontal, 1.5)]
        public Task Bar_ScaledRendering_KeepsCompactThumbInsideTrack(Orientation orientation, double scale)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var bar = new ScrollBar { Orientation = orientation, Maximum = 100, ViewportSize = 25 };
                var host = CreateHost(bar);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
                foreach (var value in new[] { 0, 100 })
                {
                    bar.Value = value;
                    Layout(host);
                    var bitmap = new RenderTargetBitmap((int)(host.Width * scale), (int)(host.Height * scale),
                        96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    bitmap.Render(host);
                    var stride = bitmap.PixelWidth * 4;
                    var pixels = new byte[stride * bitmap.PixelHeight];
                    bitmap.CopyPixels(pixels, stride, 0);
                    var painted = new List<Point>();
                    for (var y = 0; y < bitmap.PixelHeight; y++)
                    {
                        for (var x = 0; x < bitmap.PixelWidth; x++)
                        {
                            if (pixels[y * stride + x * 4 + 3] >= 100)
                            {
                                painted.Add(new Point(x, y));
                            }
                        }
                    }
                    Assert.NotEmpty(painted);
                    var width = painted.Max(point => point.X) - painted.Min(point => point.X) + 1;
                    var height = painted.Max(point => point.Y) - painted.Min(point => point.Y) + 1;
                    Assert.InRange(orientation == Orientation.Vertical ? width : height, 3 * scale, 8 * scale);
                    Assert.True((orientation == Orientation.Vertical ? height : width) >= 18 * scale);
                    Assert.True(painted.All(point => point.X > 0 && point.X < bitmap.PixelWidth - 1
                        && point.Y > 0 && point.Y < bitmap.PixelHeight - 1));
                    Export(host, $"dpi-{orientation}-{scale}-{value}", scale);
                }
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(Orientation.Vertical, 220)]
        [InlineData(Orientation.Horizontal, 220)]
        [InlineData(Orientation.Vertical, 32)]
        [InlineData(Orientation.Horizontal, 32)]
        public Task Bar_LargeRangeAndSmallViewport_KeepsThumbWithinBothTrackEnds(Orientation orientation, double length)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var bar = new ScrollBar { Orientation = orientation, Maximum = 5000, ViewportSize = 10 };
                if (orientation == Orientation.Vertical)
                {
                    bar.Height = length;
                }
                else
                {
                    bar.Width = length;
                }
                var host = CreateHost(bar);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
                foreach (var value in new[] { 0, 2500, 5000 })
                {
                    bar.Value = value;
                    Layout(host);
                    var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
                    var bounds = track.Thumb.TransformToAncestor(track).TransformBounds(new Rect(track.Thumb.RenderSize));
                    var start = orientation == Orientation.Vertical ? bounds.Top : bounds.Left;
                    var end = orientation == Orientation.Vertical ? bounds.Bottom : bounds.Right;
                    var thumbLength = orientation == Orientation.Vertical ? bounds.Height : bounds.Width;
                    Assert.True(start >= -0.01 && end <= length + 0.01,
                        $"{orientation} 在 Value={value} 时滑块范围 [{start},{end}] 越过轨道 [0,{length}]。");
                    Assert.True(thumbLength >= 24, "长列表滑块仍需保留可辨认和拖动的长度。");
                }
                return Task.CompletedTask;
            });
        }

        internal static Grid CreateHost(UIElement content)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dictation.sln")))
            {
                directory = directory.Parent;
            }
            Assert.NotNull(directory);
            var document = XDocument.Load(Path.Combine(directory.FullName, "Dictation.Presentation", "App.xaml"));
            XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var host = new Grid { Width = 300, Height = 260 };
            foreach (var dictionary in document.Descendants(ns + "ResourceDictionary.MergedDictionaries").Elements(ns + "ResourceDictionary"))
            {
                var source = dictionary.Attribute("Source")!.Value;
                host.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/Dictation.Presentation;component/{source}", UriKind.Relative)
                });
            }
            host.Children.Add(content);
            return host;
        }

        internal static void Layout(FrameworkElement host)
        {
            host.Measure(new Size(host.Width, host.Height));
            host.Arrange(new Rect(0, 0, host.Width, host.Height));
            host.UpdateLayout();
        }

        internal static void Export(FrameworkElement element, string name, double scale = 1)
        {
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }
            Directory.CreateDirectory(output);
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale),
                (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, $"scroll-{name}.png"));
            encoder.Save(stream);
        }
    }
}
