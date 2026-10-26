using System.Windows;
using System.Windows.Controls;
using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证首页复习进度的真实三层绑定、返回刷新及异步结果隔离。</summary>
    public sealed class HomeReviewProgressTests
    {
        [Fact]
        public async Task InitializeAndRefresh_RealAnswersCountOneGroupWithoutChangingLearningLabel()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SavePlanAsync(provider, LearningLanguage.Japanese, 3);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(Token);
            Assert.Equal("复习 0/3", home.ReviewProgressLabel);
            await CompleteOneAsync(provider);
            await home.RefreshReviewProgressAsync(Token);
            Assert.Equal("复习 1/3", home.ReviewProgressLabel);
            Assert.Equal("学习 0/20", home.LearningProgressLabel);
            Assert.Null(home.ReviewProgressErrorMessage);
        }

        [Fact]
        public async Task SelectLanguage_UsesIndependentPlansAndCompletedGroups()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SavePlanAsync(provider, LearningLanguage.Japanese, 3);
            await SavePlanAsync(provider, LearningLanguage.English, 7);
            await CompleteOneAsync(provider);
            var home = provider.GetRequiredService<HomeViewModel>();
            var changed = new List<string?>();
            home.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
            await home.InitializeAsync(Token);
            Assert.Equal("复习 1/3", home.ReviewProgressLabel);
            await home.SelectLanguageAsync(LearningLanguage.English, Token);
            Assert.Equal("复习 0/7", home.ReviewProgressLabel);
            await home.SelectLanguageAsync(LearningLanguage.Japanese, Token);
            Assert.Equal("复习 1/3", home.ReviewProgressLabel);
            Assert.Contains(nameof(HomeViewModel.ReviewProgressLabel), changed);
        }

        [Fact]
        public Task ReturnHome_RefreshesActualBoundProgressAndChangedTargetInSameNavigation() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SavePlanAsync(provider, LearningLanguage.Japanese, 3);
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            await home.InitializeAsync(Token);
            navigation.InitializeWorkspace(home);
            var page = new LanguageWorkspacePage { DataContext = home };
            ThemePalette.Apply(page.Resources, ResolvedTheme.Light);
            page.Measure(new Size(900, 1100)); page.Arrange(new Rect(0, 0, 900, 1100)); page.UpdateLayout();
            var label = Assert.IsType<TextBlock>(page.FindName("ReviewDailyProgress"));
            Assert.Equal("复习 0/3", label.Text);
            await home.OpenReviewPlanAsync(Token);
            Assert.IsType<ReviewPlanViewModel>(navigation.CurrentPage);
            navigation.CompleteTransition();
            await CompleteOneAsync(provider);
            await SavePlanAsync(provider, LearningLanguage.Japanese, 5);
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            page.UpdateLayout();
            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal("复习 1/5", label.Text);
        });

        [Fact]
        public async Task Refresh_DatabaseFailureShowsInlineUnavailableInsteadOfOldOrZeroCount()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(Token);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.ExecuteAsync("DROP TABLE ReviewSessionGroups;");
            await home.RefreshReviewProgressAsync(Token);
            Assert.Equal("复习进度暂不可用", home.ReviewProgressLabel);
            Assert.Contains("读取复习进度失败", home.ReviewProgressErrorMessage);
            Assert.Equal("学习 0/20", home.LearningProgressLabel);
        }

        [Fact]
        public async Task Refresh_CancelledReadPropagatesWithoutReplacingPreviousLabel()
        {
            using var directory = new TemporaryDirectory();
            var reader = new StubReviewProgressReader();
            using var provider = await CreateAsync(directory.Path, reader);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(Token);
            reader.ReadAsync = async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("取消后不应返回进度。");
            };
            using var cancellation = new CancellationTokenSource();
            var refresh = home.RefreshReviewProgressAsync(cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
            Assert.Equal("复习 0/20", home.ReviewProgressLabel);
            Assert.Null(home.ReviewProgressErrorMessage);
        }

        [Fact]
        public async Task Refresh_OldLanguageCompletionDoesNotReplaceCurrentLanguageProgress()
        {
            using var directory = new TemporaryDirectory();
            var reader = new StubReviewProgressReader();
            using var provider = await CreateAsync(directory.Path, reader);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(Token);
            var pending = new TaskCompletionSource<ReviewDailyProgressDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.ReadAsync = (language, _) => language == LearningLanguage.Japanese ? pending.Task
                : Task.FromResult(new ReviewDailyProgressDto(LearningLanguage.English, 2, 7));
            var late = home.RefreshReviewProgressAsync(Token);
            await home.SelectLanguageAsync(LearningLanguage.English, Token);
            pending.SetResult(new ReviewDailyProgressDto(LearningLanguage.Japanese, 9, 20));
            await late;
            Assert.Equal("复习 2/7", home.ReviewProgressLabel);
            Assert.Null(home.ReviewProgressErrorMessage);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Refresh_OldSameLanguageSuccessOrFailureCannotOverwriteNewerResult(bool fail)
        {
            using var directory = new TemporaryDirectory();
            var reader = new StubReviewProgressReader();
            using var provider = await CreateAsync(directory.Path, reader);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(Token);
            var pending = new TaskCompletionSource<ReviewDailyProgressDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.ReadAsync = (_, _) => pending.Task;
            var late = home.RefreshReviewProgressAsync(Token);
            reader.ReadAsync = (language, _) => Task.FromResult(new ReviewDailyProgressDto(language, 4, 20));
            await home.RefreshReviewProgressAsync(Token);
            if (fail) pending.SetException(new IOException("过期读取失败"));
            else pending.SetResult(new ReviewDailyProgressDto(LearningLanguage.Japanese, 9, 20));
            await late;
            Assert.Equal("复习 4/20", home.ReviewProgressLabel);
            Assert.Null(home.ReviewProgressErrorMessage);
        }

        [Fact]
        public async Task Refresh_MismatchedBusinessLanguageShowsErrorInsteadOfForeignProgress()
        {
            using var directory = new TemporaryDirectory();
            var reader = new StubReviewProgressReader();
            using var provider = await CreateAsync(directory.Path, reader);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(Token);
            reader.ReadAsync = (_, _) => Task.FromResult(new ReviewDailyProgressDto(LearningLanguage.English, 9, 99));
            await home.RefreshReviewProgressAsync(Token);
            Assert.Equal("复习进度暂不可用", home.ReviewProgressLabel);
            Assert.Contains("语言", home.ReviewProgressErrorMessage);
        }

        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task Page_BindsCurrentReviewProgressAndInlineErrorInBothThemes(ResolvedTheme theme) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var reader = new StubReviewProgressReader();
            using var provider = await CreateAsync(directory.Path, reader);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(Token);
            var page = new LanguageWorkspacePage { DataContext = home };
            ThemePalette.Apply(page.Resources, theme);
            page.Measure(new Size(900, 1100)); page.Arrange(new Rect(0, 0, 900, 1100)); page.UpdateLayout();
            var label = Assert.IsType<TextBlock>(page.FindName("ReviewDailyProgress"));
            Assert.Equal("复习 0/20", label.Text);
            reader.ReadAsync = (_, _) => Task.FromException<ReviewDailyProgressDto>(new IOException("读取故障"));
            await home.RefreshReviewProgressAsync(Token);
            page.UpdateLayout();
            Assert.Equal("复习进度暂不可用", label.Text);
            var error = Assert.IsType<TextBlock>(page.FindName("ReviewProgressError"));
            Assert.Contains("读取故障", error.Text);
        });

        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static Task<ServiceProvider> CreateAsync(string root, StubReviewProgressReader? reader = null) =>
            ReviewPlanPresentationTestHost.CreateAsync(root, configure: services =>
            {
                services.AddSingleton<TimeProvider>(new FixedClock());
                if (reader is not null) services.AddSingleton<IReviewProgressReader>(reader);
            });

        private static Task<ReviewPlanDto> SavePlanAsync(ServiceProvider provider, LearningLanguage language, int target) =>
            provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new SaveReviewPlanRequestDto(language, target, 20,
                ReviewRangeMode.All, Array.Empty<string>(), new[] { language == LearningLanguage.Japanese ? "Dictionary" : "Base" },
                new[] { LearningChannel.MeaningWriting }, new[] { language == LearningLanguage.Japanese ? LearningOutputMode.Kana : LearningOutputMode.Latin },
                EnglishFormLabelMode.GrammarName, null), Token);

        /// <summary>通过已实现的客观答题服务完成真实词组，不直接伪造完成计数。</summary>
        private static async Task CompleteOneAsync(ServiceProvider provider)
        {
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            var session = Assert.IsType<ReviewSessionDto>(await provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(LearningLanguage.Japanese, Token));
            foreach (var text in new[] { "故意写错", "たべる", "たべる" })
            {
                var question = Assert.IsType<Dictation.Core.Practice.PracticeQuestionDto>(await provider.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(session.SessionId, Token));
                await provider.GetRequiredService<IReviewSessionAnswerer>().SubmitAnswerAsync(new(session.SessionId, question.QuestionId, text), Token);
            }
            Assert.Null(await provider.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(session.SessionId, Token));
        }

        /// <summary>固定当地日期，防止测试跨午夜读取不同天数。</summary>
        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }
    }
}
