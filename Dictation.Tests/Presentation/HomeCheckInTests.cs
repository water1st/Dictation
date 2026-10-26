using Dapper;
using System.Windows;
using System.Windows.Controls;
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
    /// <summary>验证真实打卡绑定、返回刷新、一次庆祝和异步语言结果隔离。</summary>
    public sealed class HomeCheckInTests
    {
        private static CancellationToken token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task Refresh_RealCompletionCelebratesOnlyOnceAndDeleteWithdrawsBadge()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await provider.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 0, 0), token);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            Assert.False(home.IsTodayCheckedIn);
            await CompleteAsync(provider, "first");
            await home.RefreshCheckInAsync(token);
            Assert.True(home.IsTodayCheckedIn);
            Assert.True(home.ShowCheckInCelebration);
            Assert.Contains("1/1", home.ActualTasksLabel);
            await home.RefreshCheckInAsync(token);
            Assert.False(home.ShowCheckInCelebration);
            await ExecuteAsync(provider, "DELETE FROM WordGroups WHERE WordGroupId='first';");
            await home.RefreshCheckInAsync(token);
            Assert.False(home.IsTodayCheckedIn);
            await CompleteAsync(provider, "second");
            await home.RefreshCheckInAsync(token);
            Assert.True(home.IsTodayCheckedIn);
            Assert.False(home.ShowCheckInCelebration);
        }

        [Fact]
        public async Task Initialize_AlreadyCompletedDoesNotRepeatCelebration()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await provider.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 0, 0), token);
            await CompleteAsync(provider, "g");
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            Assert.True(home.IsTodayCheckedIn);
            Assert.False(home.ShowCheckInCelebration);
        }

        [Fact]
        public async Task Refresh_EmptyDatabaseDoesNotClaimCheckIn()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            Assert.False(home.IsTodayCheckedIn);
            Assert.Equal("今日尚未开始", home.CheckInStatusLabel);
            Assert.Null(home.CheckInErrorMessage);
        }

        [Fact]
        public async Task Refresh_InlineFailureAndCancellationDoNotLeaveStaleBadge()
        {
            using var directory = new TemporaryDirectory();
            var read = new ControlledReader();
            using var provider = await CreateAsync(directory.Path, read);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            read.Read = (_, _) => Task.FromException<DailyCheckInDto>(new IOException("统计读取失败"));
            await home.RefreshCheckInAsync(token);
            Assert.False(home.IsTodayCheckedIn);
            Assert.Contains("统计读取失败", home.CheckInErrorMessage);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => home.RefreshCheckInAsync(new CancellationToken(true)));
        }

        [Fact]
        public async Task Refresh_SameLanguageOutOfOrderResultCannotReplaceLatest()
        {
            using var directory = new TemporaryDirectory();
            var read = new ControlledReader();
            using var provider = await CreateAsync(directory.Path, read);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            var old = new TaskCompletionSource<DailyCheckInDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var latest = new TaskCompletionSource<DailyCheckInDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var count = 0;
            read.Read = (_, _) => Interlocked.Increment(ref count) == 1 ? old.Task : latest.Task;
            var oldRefresh = home.RefreshCheckInAsync(token);
            var latestRefresh = home.RefreshCheckInAsync(token);
            latest.SetResult(Value(LearningLanguage.Japanese, false));
            await latestRefresh;
            old.SetResult(Value(LearningLanguage.Japanese, true));
            await oldRefresh;
            Assert.False(home.IsTodayCheckedIn);
            Assert.False(home.ShowCheckInCelebration);
        }

        [Fact]
        public async Task SwitchLanguage_DelayedJapaneseResultCannotReplaceEnglish()
        {
            using var directory = new TemporaryDirectory();
            var read = new ControlledReader();
            using var provider = await CreateAsync(directory.Path, read);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            var delayed = new TaskCompletionSource<DailyCheckInDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            read.Read = (language, _) => language == LearningLanguage.Japanese ? delayed.Task : Task.FromResult(Value(language, true));
            var old = home.RefreshCheckInAsync(token);
            await home.SelectLanguageAsync(LearningLanguage.English, token);
            delayed.SetResult(Value(LearningLanguage.Japanese, false));
            await old;
            Assert.True(home.IsTodayCheckedIn);
            Assert.Equal(LearningLanguage.English, home.SelectedLanguage);
        }

        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task Page_RealBindingAndReturnRefreshRemainInOneWindow(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await provider.GetRequiredService<DailyCheckInDataAccessor>().InsertIfAbsentAsync(new("Japanese", StatisticsTestHost.Today, 1, 0, 0), token);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeWorkspace(home);
            var page = new LanguageWorkspacePage { DataContext = home };
            ThemePalette.Apply(page.Resources, theme);
            page.Measure(new Size(900, 1100)); page.Arrange(new Rect(0, 0, 900, 1100)); page.UpdateLayout();
            var status = Assert.IsType<TextBlock>(page.FindName("TodayCheckInStatus"));
            Assert.Equal(home.CheckInStatusLabel, status.Text);
            await provider.GetRequiredService<IHomeNavigation>().OpenReviewPlanAsync(LearningLanguage.Japanese, token);
            navigation.CompleteTransition();
            await CompleteAsync(provider, "g");
            await provider.GetRequiredService<IBackNavigation>().GoBackAsync(token);
            navigation.CompleteTransition();
            page.UpdateLayout();
            Assert.Same(home, navigation.CurrentPage);
            Assert.True(home.IsTodayCheckedIn);
            Assert.Equal(home.CheckInStatusLabel, status.Text);
            Assert.Null(Window.GetWindow(page));
        });

        private static Task<ServiceProvider> CreateAsync(string root, ControlledReader? reader = null) =>
            ReviewPlanPresentationTestHost.CreateAsync(root, configure: services =>
            {
                services.AddSingleton<TimeProvider>(new StatisticsTestClock());
                if (reader is not null) services.AddSingleton<IDailyCheckInReader>(reader);
            });

        private static Task CompleteAsync(ServiceProvider provider, string id) => ExecuteAsync(provider, """
            INSERT INTO WordGroups VALUES(@id,'Japanese',@id,'Review','2026-10-01T00:00:00Z');
            INSERT INTO LearningGroupProgress VALUES(@id,'Japanese','2026-10-12','2026-10-12');
            """, new { id });

        private static async Task ExecuteAsync(ServiceProvider provider, string sql, object? parameters = null)
        {
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(token);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: token));
        }

        private static DailyCheckInDto Value(LearningLanguage language, bool completed) =>
            new(language, new DateOnly(2026, 10, 12), completed ? 1 : 0, 1, 0, 0, 0, 0, true, completed);

        /// <summary>只控制异步查询边界；断言始终针对真实首页状态与语言隔离。</summary>
        private sealed class ControlledReader : IDailyCheckInReader
        {
            internal Func<LearningLanguage, CancellationToken, Task<DailyCheckInDto>> Read { get; set; } =
                (language, _) => Task.FromResult(Value(language, false));
            public Task<DailyCheckInDto> GetTodayAsync(LearningLanguage language, CancellationToken cancellationToken = default) =>
                Read(language, cancellationToken);
        }
    }
}
