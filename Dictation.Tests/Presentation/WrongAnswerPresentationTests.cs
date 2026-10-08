using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Mistakes;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.Core.Tts;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Practice;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dictation.Tests.Presentation
{
    /// <summary>从首页真实走通错题列表、只读详情、原练习页及同一记忆，覆盖取消与语言隔离。</summary>
    public sealed class WrongAnswerPresentationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public Task HomeList_RepracticeUsesOriginalPageAndOriginalMemory(LearningLanguage language) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SeedReviewErrorAsync(provider, language);
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            await home.SelectLanguageAsync(language, Token);
            navigation.InitializeHome(home);
            await home.OpenWrongAnswersAsync(Token);
            var list = Assert.IsType<WrongAnswerListViewModel>(navigation.CurrentPage);
            var item = Assert.Single(list.Items);
            list.SelectedItem = item;
            list.SelectAll = true;
            Assert.True(item.IsSelected);
            var page = new WrongAnswerListPage { DataContext = list, Width = 960, Height = 720 };
            Render(page);
            Assert.Same(list.Items, Assert.IsType<ListBox>(page.FindName("WrongAnswerList")).ItemsSource);
            Assert.Equal(item.Item.Meanings, Assert.IsType<TextBlock>(page.FindName("DetailMeanings")).Text);
            Assert.Equal(Visibility.Visible, Assert.IsAssignableFrom<FrameworkElement>(page.FindName("DetailPanel")).Visibility);
            var windowsBefore = Application.Current?.Windows.Count ?? 0;
            navigation.CompleteTransition();
            await list.RepracticeAsync(Token);
            var practice = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            Assert.Equal(language == LearningLanguage.Japanese ? "日语错题重练" : "英语错题重练", practice.WindowTitle);
            var originalPage = new PracticeSessionPage { DataContext = practice };
            Assert.NotNull(originalPage.FindName("QuestionPanel"));
            for (var index = 0; index < 2; index++)
            {
                practice.AnswerText = language == LearningLanguage.Japanese ? "たべる" : "take off";
                await practice.SubmitAsync(Token);
                Assert.True(practice.Feedback!.WasCorrect);
                await practice.ContinueAsync(Token);
            }
            Assert.True(practice.IsCompleted);
            Assert.Equal(2, practice.Summary!.AnsweredCount);
            Assert.Equal(1, practice.Summary.CompletedWordGroupCount);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            Assert.Equal(2, await connection.ExecuteScalarAsync<int>("SELECT ReviewCount FROM ReviewFormMemories;"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT LapseCount FROM ReviewFormMemories;"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM WordGroups;"));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            Assert.Equal(windowsBefore, Application.Current?.Windows.Count ?? 0);
            navigation.CompleteTransition();
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            Assert.Same(list, navigation.CurrentPage);
            Assert.Single(list.Items);
            await navigation.GoBackAsync(Token);
            navigation.CompleteTransition();
            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal("复习 1/20", home.ReviewProgressLabel);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => list.RefreshAsync(Token));
        });

        [Fact]
        public Task LearningError_ContinuesSamePendingQuestionWithoutMovingLibrary() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var batch = await provider.GetRequiredService<IImportStaging>().StageAsync(new(LearningLanguage.Japanese,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }), Token);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new(batch.Batch.BatchId,
                new[] { Assert.Single(batch.Batch.Items).ItemId }, ImportDecision.Learning), Token);
            await provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Dictionary" },
                new[] { LearningChannel.Dictation }, new[] { LearningOutputMode.Kana }), Token);
            var sessions = provider.GetRequiredService<LearningSessionService>();
            var session = await sessions.StartOrResumeAsync(new(LearningLanguage.Japanese), Token);
            var first = Assert.IsType<PracticeQuestionDto>(await sessions.GetCurrentQuestionAsync(session.SessionId, Token));
            var pending = Assert.IsType<PracticeQuestionDto>((await sessions.SubmitAnswerAsync(new(session.SessionId, first.QuestionId, "错误"), Token)).NextQuestion);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            navigation.InitializeHome(home);
            await home.OpenWrongAnswersAsync(Token);
            var list = Assert.IsType<WrongAnswerListViewModel>(navigation.CurrentPage);
            list.SelectedItem = Assert.Single(list.Items);
            Assert.True(list.ContinueLearningCommand.CanExecute(null));
            Assert.False(list.RepracticeCommand.CanExecute(null));
            navigation.CompleteTransition();
            await list.ContinueLearningAsync(Token);
            var vm = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            Assert.Equal(pending.QuestionId, vm.Question!.QuestionId);
            Assert.Equal(session.SessionId, vm.Session!.SessionId);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            Assert.Equal("Learning", await connection.ExecuteScalarAsync<string>("SELECT Placement FROM WordGroups;"));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ReviewFormMemories;"));
        });

        [Fact]
        public Task EmptyOtherLanguage_DisablesSelectionAndLocksPageLanguage() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SeedReviewErrorAsync(provider, LearningLanguage.Japanese);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.SelectLanguageAsync(LearningLanguage.English, Token);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeHome(home);
            await home.OpenWrongAnswersAsync(Token);
            var vm = Assert.IsType<WrongAnswerListViewModel>(navigation.CurrentPage);
            Assert.Empty(vm.Items);
            Assert.False(vm.RepracticeCommand.CanExecute(null));
            vm.SelectAll = true;
            Assert.False(vm.SelectAll);
            Assert.Throws<InvalidOperationException>(() => vm.Initialize(LearningLanguage.Japanese));
            await vm.RepracticeAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Same(vm, navigation.CurrentPage);
        });

        [Fact]
        public Task DetailSpeakerAndThemes_DoNotChangeProgressOrCreateAudioFiles() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var tts = new LearningPresentationTestEnvironment.ControlledTtsProvider();
            var audio = new LearningPresentationTestEnvironment.ControlledAudioPlayer();
            using var provider = await CreateAsync(directory.Path, tts, audio);
            await SeedReviewErrorAsync(provider, LearningLanguage.Japanese);
            var vm = provider.GetRequiredService<WrongAnswerListViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(Token);
            vm.SelectedItem = Assert.Single(vm.Items);
            await vm.PlayDetailAsync(Token);
            Assert.Equal("たべる", Assert.Single(tts.Requests).Text);
            Assert.Single(audio.Played);
            var page = new WrongAnswerListPage { DataContext = vm, Width = 960, Height = 720 };
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(page.Resources, theme);
                Render(page);
                Assert.Equal(theme == ResolvedTheme.Light ? Color.FromRgb(31, 41, 55) : Color.FromRgb(248, 250, 252),
                    Assert.IsType<SolidColorBrush>(Assert.IsType<TextBlock>(page.FindName("DetailMeanings")).Foreground).Color);
            }
            tts.Failure = new IOException("音频不可用");
            await vm.PlayDetailAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT ReviewCount FROM ReviewFormMemories;"));
            Assert.Empty(Directory.GetFiles(directory.Path, "*.wav", SearchOption.AllDirectories));
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task CancelOrDispose_LateReadFailureCannotPublishError(bool dispose) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var query = new DeferredQuery();
            using var provider = await CreateAsync(directory.Path, configure: services => services.AddSingleton<IWrongAnswerQuery>(query));
            var vm = provider.GetRequiredService<WrongAnswerListViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            var reading = vm.RefreshAsync(Token);
            await query.Entered.Task.WaitAsync(Token);
            if (dispose) vm.Dispose(); else vm.CancelOperation();
            query.Completion.SetException(new IOException("取消后的迟到故障"));
            await reading;
            Assert.Null(vm.ErrorMessage);
            Assert.Empty(vm.Items);
            if (!dispose) Assert.False(vm.IsBusy);
        });

        [Fact]
        public Task SelectAll_TriStateAndDetailMembershipAreBasedOnCurrentItems() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await SeedReviewErrorAsync(provider, LearningLanguage.Japanese);
            await SeedReviewErrorAsync(provider, LearningLanguage.Japanese, "jmdict:1:2");
            var vm = provider.GetRequiredService<WrongAnswerListViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(Token);
            Assert.Equal(2, vm.Items.Count);
            var first = vm.Items[0];
            first.IsSelected = true;
            Assert.Null(vm.SelectAll);
            vm.SelectAll = true;
            Assert.All(vm.Items, item => Assert.True(item.IsSelected));
            vm.SelectAll = false;
            Assert.All(vm.Items, item => Assert.False(item.IsSelected));
            Assert.Throws<ArgumentException>(() => vm.SelectedItem = new WrongAnswerItemViewModel(first.Item));
            var page = new WrongAnswerListPage { DataContext = vm };
            Render(page);
            Assert.Same(vm.RepracticeCommand, Assert.IsType<Button>(page.FindName("RepracticeButton")).Command);
            Assert.Same(vm.PlayDetailCommand, Assert.IsType<Button>(page.FindName("DetailSpeakerButton")).Command);
        });

        private static void Render(FrameworkElement page)
        {
            page.Measure(new Size(960, 720));
            page.Arrange(new Rect(0, 0, 960, 720));
            page.UpdateLayout();
        }

        private static Task<ServiceProvider> CreateAsync(string root,
            LearningPresentationTestEnvironment.ControlledTtsProvider? tts = null,
            LearningPresentationTestEnvironment.ControlledAudioPlayer? audio = null,
            Action<IServiceCollection>? configure = null) => ReviewPlanPresentationTestHost.CreateAsync(root, configure: services =>
            {
                services.AddSingleton<ITtsProvider>(tts ?? new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(audio ?? new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                configure?.Invoke(services);
            });

        private static async Task SeedReviewErrorAsync(ServiceProvider provider, LearningLanguage language, string sense = "jmdict:1:1")
        {
            var japanese = language == LearningLanguage.Japanese;
            await ReviewPlanPresentationTestHost.ImportAsync(provider, japanese ? "食べる" : "take off",
                japanese ? sense : "take_off%2:38:00::", language);
            await provider.GetRequiredService<IReviewPlanEditor>().SaveAsync(new(language, 20, 20, ReviewRangeMode.All,
                Array.Empty<string>(), new[] { japanese ? "Dictionary" : "Base" }, new[] { LearningChannel.Dictation },
                new[] { japanese ? LearningOutputMode.Kana : LearningOutputMode.Latin }, EnglishFormLabelMode.GrammarName, null), Token);
            var session = Assert.IsType<ReviewSessionDto>(await provider.GetRequiredService<IReviewSessionStarter>().StartOrResumeAsync(language, Token));
            var q = Assert.IsType<PracticeQuestionDto>(await provider.GetRequiredService<IReviewQuestionReader>().GetCurrentQuestionAsync(session.SessionId, Token));
            var answerer = provider.GetRequiredService<IReviewSessionAnswerer>();
            var result = await answerer.SubmitAnswerAsync(new(session.SessionId, q.QuestionId, "错误"), Token);
            while (result.NextQuestion is { } next)
                result = await answerer.SubmitAnswerAsync(new(session.SessionId, next.QuestionId, japanese ? "たべる" : "take off"), Token);
        }

        /// <summary>模拟明确忽略取消的读取边界，检验页面能否阻止迟到异常。</summary>
        private sealed class DeferredQuery : IWrongAnswerQuery
        {
            internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<IReadOnlyList<WrongAnswerItemDto>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<IReadOnlyList<WrongAnswerItemDto>> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
            {
                Entered.TrySetResult();
                return Completion.Task;
            }
        }
    }
}
