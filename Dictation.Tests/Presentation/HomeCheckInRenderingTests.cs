using Dapper;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dictation.DataAccess.Runtime;
using Dictation.DataAccess.Statistics;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>使用真实服务、数据库和STA渲染检验首页打卡；仅显式指定目录时导出验证截图。</summary>
    public sealed class HomeCheckInRenderingTests
    {
        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task Page_RendersCompletedStatusInBothThemes(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path,
                configure: services => services.AddSingleton<TimeProvider>(new StatisticsTestClock()));
            await provider.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 0, 0), token);
            await using (var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection())
            {
                await connection.OpenAsync(token);
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO WordGroups VALUES('render','Japanese','render','Review','2026-10-01T00:00:00Z');
                    INSERT INTO LearningGroupProgress VALUES('render','Japanese','2026-10-12','2026-10-12');
                    """, cancellationToken: token));
            }
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            var page = new LanguageWorkspacePage { DataContext = home };
            ThemePalette.Apply(page.Resources, theme);
            page.Measure(new Size(900, 1100)); page.Arrange(new Rect(0, 0, 900, 1100)); page.UpdateLayout();
            Assert.Contains("已打卡", Assert.IsType<TextBlock>(page.FindName("TodayCheckInStatus")).Text);
            Assert.True(home.IsTodayCheckedIn);
            Assert.False(home.ShowCheckInCelebration);
            var bitmap = new RenderTargetBitmap(900, 1100, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page);
            var pixels = new byte[4];
            bitmap.CopyPixels(new Int32Rect(2, 2, 1, 1), pixels, 4, 0);
            Assert.Equal(255, pixels[3]);
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"2026-10-12-checkin-home-{theme.ToString().ToLowerInvariant()}.png"));
                encoder.Save(stream);
            }
        });
    }
}
