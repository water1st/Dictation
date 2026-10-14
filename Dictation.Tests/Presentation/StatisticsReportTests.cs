using Dapper;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Dictation.Core.Configuration;
using Dictation.Core.Statistics;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证报表页面真实绑定、语言隔离、迟到请求与不同主题尺寸的原生图表。</summary>
    public sealed class StatisticsReportTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);

        [Fact]
        public Task Refresh_UsesRealCountsAndKeepsPageLanguage() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            await connection.ExecuteAsync("""
                INSERT INTO WordGroups VALUES('one','English','one','Review','2026-10-01');
                INSERT INTO LearningGroupProgress VALUES('one','English','2026-10-12','2026-10-12');
                """);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.English);
            await vm.RefreshAsync(Token);
            Assert.NotNull(vm.Report);
            Assert.Equal(1, vm.Report.LearnedGroupCount);
            Assert.Equal(1, vm.Report.TodayLearnedGroupCount);
            Assert.Equal(LearningLanguage.English, vm.Report.Language);
            Assert.Throws<InvalidOperationException>(() => vm.Initialize(LearningLanguage.Japanese));
        });

        [Fact]
        public Task Refresh_FailureIsInlineAndRetryLoadsReport() => WithVm(async (vm, reader) =>
        {
            reader.Read = (_, _) => Task.FromException<LanguageStatisticsDto>(new IOException("测试读取失败"));
            await vm.RefreshReportAsync(Token);
            Assert.Null(vm.Report);
            Assert.Contains("测试读取失败", vm.ReportErrorMessage);
            reader.Read = (language, _) => Task.FromResult(Data(language, 7));
            await vm.RefreshReportAsync(Token);
            Assert.Null(vm.ReportErrorMessage);
            Assert.NotNull(vm.Report);
            Assert.Equal(7, vm.Report.LearnedGroupCount);
        });

        [Fact]
        public Task Refresh_RejectsWrongLanguageWithoutPublishingIt() => WithVm(async (vm, reader) =>
        {
            reader.Read = (_, _) => Task.FromResult(Data(LearningLanguage.Japanese, 7));
            await vm.RefreshReportAsync(Token);
            Assert.Null(vm.Report);
            Assert.NotNull(vm.ReportErrorMessage);
        });

        [Fact]
        public Task Refresh_CancellationDoesNotBecomeZeroSuccess() => WithVm(async (vm, _) =>
        {
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vm.RefreshReportAsync(canceled.Token));
            Assert.Null(vm.Report);
            Assert.Null(vm.ReportErrorMessage);
        });

        [Fact]
        public Task Refresh_LateResultCannotOverwriteNewReport() => WithVm(async (vm, reader) =>
        {
            var slow = new TaskCompletionSource<LanguageStatisticsDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.Read = (_, _) => slow.Task;
            var old = vm.RefreshReportAsync(Token);
            reader.Read = (language, _) => Task.FromResult(Data(language, 9));
            await vm.RefreshReportAsync(Token);
            slow.SetResult(Data(LearningLanguage.English, 7));
            await old;
            Assert.NotNull(vm.Report);
            Assert.Equal(9, vm.Report.LearnedGroupCount);
        });

        [Fact]
        public Task Refresh_DisposedPageDoesNotPublishLateResult() => WithVm(async (vm, reader) =>
        {
            var slow = new TaskCompletionSource<LanguageStatisticsDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.Read = (_, _) => slow.Task;
            var pending = vm.RefreshReportAsync(Token);
            vm.Dispose();
            slow.SetResult(Data(LearningLanguage.English, 7));
            await pending;
            Assert.Null(vm.Report);
        });

        [Fact]
        public Task Refresh_EmptyMemoryUsesExplicitEmptyStateNotOneHundredPercent() => WithVm(async (vm, reader) =>
        {
            reader.Read = (language, _) => Task.FromResult(new LanguageStatisticsDto(language, Now, 0, 0, 0, 0, 0,
                Array.Empty<FrequentMistakeDto>(), Array.Empty<StatisticsTrendPointDto>(), Array.Empty<StatisticsTrendPointDto>()));
            await vm.RefreshReportAsync(Token);
            Assert.NotNull(vm.Report);
            Assert.Empty(vm.Report.RetentionPoints);
            Assert.Contains("尚无", vm.Report.RetentionDescription);
            Assert.DoesNotContain("100%", vm.Report.RetentionDescription);
            Assert.Empty(vm.Report.TopMistakes);
        });

        [Theory]
        [InlineData(ResolvedTheme.Light, 960)]
        [InlineData(ResolvedTheme.Dark, 960)]
        [InlineData(ResolvedTheme.Dark, 760)]
        public Task Page_BindsCardsAndNativeChartsAcrossThemesAndWidths(ResolvedTheme theme, int width) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path, new ControlledReportReader());
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.English);
            await vm.RefreshAsync(Token);
            var page = new StatisticsPage { DataContext = vm };
            ThemePalette.Apply(page.Resources, theme);
            page.Measure(new Size(width, 1900)); page.Arrange(new Rect(0, 0, width, 1900)); page.UpdateLayout();
            Assert.Equal("7", Assert.IsType<TextBlock>(page.FindName("LearnedCount")).Text);
            Assert.Equal("2", Assert.IsType<TextBlock>(page.FindName("TodayLearnedCount")).Text);
            Assert.Equal("3", Assert.IsType<TextBlock>(page.FindName("TodayReviewedCount")).Text);
            Assert.Single(Assert.IsType<ItemsControl>(page.FindName("FrequentMistakes")).Items.Cast<object>());
            var line = Assert.IsType<Polyline>(page.FindName("RetentionLine"));
            Assert.Equal(30, line.Points.Count);
            Assert.All(line.Points, point => { Assert.InRange(point.X, 0, 600); Assert.InRange(point.Y, 0, 140); });
            Assert.Equal(14, Assert.IsType<ItemsControl>(page.FindName("DueBars")).Items.Count);
            Assert.Contains("预计", Assert.IsType<TextBlock>(page.FindName("RetentionCaption")).Text);
            Assert.Contains("不再复习", Assert.IsType<TextBlock>(page.FindName("RetentionCaption")).Text);
            Assert.True(line.ActualWidth <= page.ActualWidth);
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                var image = new RenderTargetBitmap(width, 1900, 96, 96, PixelFormats.Pbgra32);
                image.Render(page);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(System.IO.Path.Combine(output, $"2026-10-13-report-{theme.ToString().ToLowerInvariant()}-{width}.png"));
                encoder.Save(stream);
            }
        });

        private static Task WithVm(Func<StatisticsViewModel, ControlledReportReader, Task> action) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var reader = new ControlledReportReader();
            using var provider = await CreateAsync(directory.Path, reader);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.English);
            await action(vm, reader);
        });

        private static Task<ServiceProvider> CreateAsync(string root, ControlledReportReader? reader = null) =>
            ReviewPlanPresentationTestHost.CreateAsync(root, configure: services =>
            {
                services.AddSingleton<TimeProvider>(new StatisticsTestClock());
                if (reader is not null) services.AddSingleton<ILanguageStatisticsReader>(reader);
            });

        private static LanguageStatisticsDto Data(LearningLanguage language, long total) => new(language, Now, total, 2, 3, 4, 1,
            new[] { new FrequentMistakeDto("one", "go", "移动", 5) },
            Enumerable.Range(0, 30).Select(i => new StatisticsTrendPointDto(new DateOnly(2026, 10, 12).AddDays(i), 0.95 - i * 0.01)).ToArray(),
            Enumerable.Range(0, 14).Select(i => new StatisticsTrendPointDto(new DateOnly(2026, 10, 12).AddDays(i), i == 2 ? 3 : 0)).ToArray());

        /// <summary>只控制报表查询的返回时序，页面、绑定与作用域保持真实。</summary>
        private sealed class ControlledReportReader : ILanguageStatisticsReader
        {
            internal Func<LearningLanguage, CancellationToken, Task<LanguageStatisticsDto>> Read { get; set; } = (language, _) => Task.FromResult(Data(language, 7));
            public Task<LanguageStatisticsDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Read(language, cancellationToken);
        }
    }
}
