using Dapper;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dictation.Core.Configuration;
using Dictation.Core.Statistics;
using Dictation.DataAccess.Runtime;
using Dictation.DataAccess.Statistics;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证真实月历单窗口导航、固定语言、异步月份隔离及圆形徽章渲染。</summary>
    public sealed class StatisticsCalendarTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;

        [Fact]
        public Task Navigate_OpensFixedLanguageAndDisposesScopeOnlyAfterReturnTransition() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeWorkspace(home);
            var capability = provider.GetRequiredService<IStatisticsNavigation>();
            await capability.OpenStatisticsAsync(LearningLanguage.Japanese, token);
            var page = Assert.IsType<StatisticsViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.Japanese, page.Language);
            Assert.Equal(42, page.Cells.Count);
            navigation.CompleteTransition();
            await Assert.ThrowsAsync<InvalidOperationException>(() => capability.OpenStatisticsAsync(LearningLanguage.English, token));
            await provider.GetRequiredService<IBackNavigation>().GoBackAsync(token);
            Assert.Same(home, navigation.CurrentPage);
            await page.RefreshAsync(token);
            navigation.CompleteTransition();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => page.RefreshAsync(token));
        });

        [Fact]
        public async Task Initialize_RejectsLanguageSwitchAndInvalidInputs()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            Assert.Throws<ArgumentOutOfRangeException>(() => vm.Initialize((LearningLanguage)99));
            vm.Initialize(LearningLanguage.English);
            Assert.Throws<InvalidOperationException>(() => vm.Initialize(LearningLanguage.Japanese));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => vm.ShowMonthAsync(0, 1, token));
        }

        [Fact]
        public async Task MonthNavigation_DisablesOverflowAndKeepsSixWeekGrid()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.ShowMonthAsync(1, 1, token);
            Assert.False(vm.PreviousMonthCommand.CanExecute(null));
            Assert.True(vm.NextMonthCommand.CanExecute(null));
            await vm.ShowMonthAsync(9999, 12, token);
            Assert.False(vm.NextMonthCommand.CanExecute(null));
            Assert.Equal(31, vm.Cells.Count(cell => cell.Date is not null));
            Assert.Equal(42, vm.Cells.Count);
            await vm.ShowMonthAsync(2028, 2, token);
            Assert.Equal(29, vm.Cells.Count(cell => cell.Date is not null));
            Assert.Equal(new DateOnly(2028, 2, 1), vm.Cells[1].Date);
        }

        [Fact]
        public async Task Refresh_LateMonthCannotOverwriteCurrentMonth()
        {
            using var directory = new TemporaryDirectory();
            var reader = new ControlledCalendarReader();
            using var provider = await CreateAsync(directory.Path, reader);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            var old = new TaskCompletionSource<CalendarMonthDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.Read = (year, month, _) => month == 9 ? old.Task : Task.FromResult(EmptyMonth(year, month));
            var delayed = vm.ShowMonthAsync(2026, 9, token);
            await vm.ShowMonthAsync(2026, 10, token);
            old.SetResult(EmptyMonth(2026, 9));
            await delayed;
            Assert.Equal("2026年10月", vm.MonthLabel);
            Assert.Equal(31, vm.Cells.Count(cell => cell.Date is not null));
            Assert.All(vm.Cells.Where(cell => cell.Date is not null), cell => Assert.Equal(10, cell.Date!.Value.Month));
        }

        [Fact]
        public async Task Refresh_FailureIsInlineAndCancellationDoesNotBecomeSuccess()
        {
            using var directory = new TemporaryDirectory();
            var reader = new ControlledCalendarReader();
            using var provider = await CreateAsync(directory.Path, reader);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            reader.Read = (_, _, _) => Task.FromException<CalendarMonthDto>(new IOException("月历读取故障"));
            await vm.RefreshAsync(token);
            Assert.Contains("月历读取故障", vm.CalendarErrorMessage);
            Assert.Empty(vm.Cells);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vm.RefreshAsync(new CancellationToken(true)));
        }

        [Fact]
        public async Task Refresh_RejectsResponseForWrongRequestedMonth()
        {
            using var directory = new TemporaryDirectory();
            var reader = new ControlledCalendarReader { Read = (_, _, _) => Task.FromResult(EmptyMonth(2026, 8)) };
            using var provider = await CreateAsync(directory.Path, reader);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.ShowMonthAsync(2026, 10, token);
            Assert.NotNull(vm.CalendarErrorMessage);
            Assert.Empty(vm.Cells);
        }

        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task Page_RendersIndependentRoundBadgesAndBindings(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var data = provider.GetRequiredService<DailyCheckInDataAccessor>();
            await data.InsertIfAbsentAsync(new("Japanese", "2026-10-12", 1, 0, 0), token);
            await data.InsertIfAbsentAsync(new("English", "2026-10-12", 1, 0, 0), token);
            await using (var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection())
            {
                await connection.OpenAsync(token);
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO WordGroups VALUES('jp','Japanese','jp','Review','2026-10-01T00:00:00Z'),('en','English','en','Review','2026-10-01T00:00:00Z');
                    INSERT INTO LearningGroupProgress VALUES('jp','Japanese','2026-10-12','2026-10-12'),('en','English','2026-10-12','2026-10-12');
                    """, cancellationToken: token));
            }
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<StatisticsViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(token);
            var day = Assert.Single(vm.Cells, cell => cell.Date == new DateOnly(2026, 10, 12));
            Assert.True(day.JapaneseIsCheckedIn);
            Assert.True(day.EnglishIsCheckedIn);
            var page = new StatisticsPage { DataContext = vm };
            ThemePalette.Apply(page.Resources, theme);
            page.Measure(new Size(960, 820)); page.Arrange(new Rect(0, 0, 960, 820)); page.UpdateLayout();
            Assert.Equal("2026年10月", Assert.IsType<TextBlock>(page.FindName("CalendarMonthLabel")).Text);
            var grid = Assert.IsType<ItemsControl>(page.FindName("MonthGrid"));
            Assert.Equal(42, grid.Items.Count);
            var active = Children<LanguageBadge>(page).Where(badge => badge.IsActive).ToArray();
            Assert.Equal(2, active.Length);
            Assert.Contains(active, badge => badge.SourceLanguage == LearningLanguage.Japanese);
            Assert.Contains(active, badge => badge.SourceLanguage == LearningLanguage.English);
            Assert.All(active, badge => { Assert.True(badge.ActualWidth > 0); Assert.True(badge.ActualWidth <= 40); });
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                var image = new RenderTargetBitmap(960, 820, 96, 96, PixelFormats.Pbgra32);
                image.Render(page);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(Path.Combine(output, $"2026-10-12-calendar-{theme.ToString().ToLowerInvariant()}.png"));
                encoder.Save(stream);
            }
        });

        private static Task<ServiceProvider> CreateAsync(string root, ControlledCalendarReader? reader = null) =>
            ReviewPlanPresentationTestHost.CreateAsync(root, configure: services =>
            {
                services.AddSingleton<TimeProvider>(new StatisticsTestClock());
                if (reader is not null) services.AddSingleton<ICheckInCalendarReader>(reader);
            });

        private static CalendarMonthDto EmptyMonth(int year, int month) => new(year, month,
            Enumerable.Range(1, DateTime.DaysInMonth(year, month)).Select(day => new CalendarDayDto(new DateOnly(year, month, day), false, false, 0, 0, 0, 0, 0)).ToArray());

        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item) yield return item;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
        }

        /// <summary>只替换查询时序或错误，断言真实页面和请求隔离行为。</summary>
        private sealed class ControlledCalendarReader : ICheckInCalendarReader
        {
            internal Func<int, int, CancellationToken, Task<CalendarMonthDto>> Read { get; set; } = (year, month, _) => Task.FromResult(EmptyMonth(year, month));
            public Task<CalendarMonthDto> LoadMonthAsync(int year, int month, CancellationToken cancellationToken = default) => Read(year, month, cancellationToken);
        }
    }
}
