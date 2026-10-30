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
                bar.IsEnabled = false;
                Layout(host);
                Assert.False(thumb.IsEnabled);
                Assert.Contains(FormControlThemeTests.Children<UIElement>(bar), element => element.Opacity < 1);
                Assert.Equal(size, bar.RenderSize);
                bar.IsEnabled = true;
                Assert.Equal(25, bar.Value);
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
