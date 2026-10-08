using System.Runtime.ExceptionServices;
using Dapper;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证首页学习入口、会话恢复、播放器注册和单页四态渲染。
    /// </summary>
    public sealed class LearningSessionNavigationAndRenderingTests
    {
        [Fact]
        public async Task HomeLearningCommand_UsesCurrentLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var navigation = new RecordingHomeNavigation();
            var home = new HomeViewModel(
                new SettingsService(new ConfigurationBuilder().Build(),
                    new ApplicationPaths(temporaryDirectory.Path)),
                navigation,
                new StubLearningProgressReader(),
                new StubLearningPlanReader(),
                new StubLearningSessionReader(),
                new StubReviewProgressReader());
            await home.SelectLanguageAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);

            home.OpenLearningCommand.Execute(null);
            var language = await navigation.OpenedLearning.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.English, language);
        }

        [Fact]
        public async Task LearningNavigation_ReturnsHomeAndReopensTheSameActiveSession()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(
                LearningLanguage.English);
            using var provider = CreateProvider(environment);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.SelectLanguageAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);

            await home.OpenLearningAsync(TestContext.Current.CancellationToken);

            var first = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, first.SelectedLanguage);
            var sessionId = first.Session!.SessionId;
            var questionId = first.Question!.QuestionId;
            navigation.CompleteTransition();
            await navigation.GoBackAsync(TestContext.Current.CancellationToken);
            Assert.Same(home, navigation.CurrentPage);
            navigation.CompleteTransition();

            await home.OpenLearningAsync(TestContext.Current.CancellationToken);

            var resumed = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            Assert.NotSame(first, resumed);
            Assert.Equal(LearningLanguage.English, resumed.SelectedLanguage);
            Assert.Equal(sessionId, resumed.Session!.SessionId);
            Assert.Equal(questionId, resumed.Question!.QuestionId);
        }

        [Fact]
        public async Task ReturningFromLearningSession_RefreshesCompletedGroupCount()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync();
            using var provider = CreateProvider(environment);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);
            Assert.Equal("学习 0/20", home.LearningProgressLabel);
            await home.OpenLearningAsync(TestContext.Current.CancellationToken);
            navigation.CompleteTransition();
            await using var connection = new SqliteConnectionFactory(environment.Paths).CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                "UPDATE LearningGroupProgress SET CompletedLocalDate = @Date;",
                new { Date = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });

            await navigation.GoBackAsync(TestContext.Current.CancellationToken);

            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal("学习 1/20", home.LearningProgressLabel);
            navigation.CompleteTransition();
        }

        [Fact]
        public async Task DictationStartup_WhenSpeechWaits_ShowsCancellablePageAndReleasesScopeOnBack()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(dictation: true);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            environment.Tts.BeforeReturnAsync = async token =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            };
            using var provider = CreateProvider(environment);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            navigation.InitializeHome(home);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            var opening = home.OpenLearningAsync(cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            try
            {
                var page = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
                Assert.True(page.CancelCommand.CanExecute(null));
                page.CancelCommand.Execute(null);
                await opening;
                Assert.False(page.IsBusy);
                navigation.CompleteTransition();
                await navigation.GoBackAsync(TestContext.Current.CancellationToken);
                Assert.Same(home, navigation.CurrentPage);
                navigation.CompleteTransition();
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    _ = page.ReplayQuestionAsync(TestContext.Current.CancellationToken);
                });
            }
            finally
            {
                cancellation.Cancel();
                try
                {
                    await opening;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [Fact]
        public async Task DictationStartup_WhenCallerCancels_PropagatesCancellation()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(dictation: true);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            environment.Tts.BeforeReturnAsync = async token =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            };
            using var provider = CreateProvider(environment);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            navigation.InitializeHome(home);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            var opening = home.OpenLearningAsync(cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
            Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
        }

        [Fact]
        public void PresentationRegistration_ContainsOneMemoryPlayerAndScopedSessionViewModel()
        {
            var services = new ServiceCollection();

            services.AddDictationPresentation();

            var audio = Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IAudioPlayer));
            Assert.Equal(typeof(MemoryWavAudioPlayer), audio.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, audio.Lifetime);
            var session = Assert.Single(services, descriptor =>
                !descriptor.IsKeyedService && descriptor.ServiceType == typeof(PracticeSessionViewModel));
            Assert.Equal(ServiceLifetime.Scoped, session.Lifetime);
        }

        [Fact]
        public void LearningPage_RendersQuestionFeedbackBrowseAndSummaryWithoutLeakingAnswer()
        {
            RunOnSta(() =>
            {
                using var environment = LearningPresentationTestEnvironment.CreateAsync()
                    .GetAwaiter().GetResult();
                using var viewModel = environment.CreateViewModel();
                viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken)
                    .GetAwaiter().GetResult();
                var page = new PracticeSessionPage { DataContext = viewModel };
                RenderControl(page);

                Assert.True(typeof(UserControl).IsAssignableFrom(typeof(PracticeSessionPage)));
                var answer = Assert.IsType<TextBox>(page.FindName("AnswerTextBox"));
                Assert.False(SpellCheck.GetIsEnabled(answer));
                var replay = Assert.IsType<Button>(page.FindName("ReplayQuestionButton"));
                Assert.Equal("播放题目发音", AutomationProperties.GetName(replay));
                Assert.IsType<TextBlock>(replay.Content);
                Assert.Equal(Visibility.Collapsed, replay.Visibility);
                Assert.Equal(Visibility.Visible, FindPanel(page, "QuestionPanel").Visibility);
                Assert.Equal(Visibility.Collapsed, FindPanel(page, "BrowsePanel").Visibility);
                Assert.Equal(Visibility.Collapsed, FindPanel(page, "SummaryPanel").Visibility);
                Assert.DoesNotContain(FindVisualChildren<TextBlock>(page), text =>
                    text.Text.Contains("たべます", StringComparison.Ordinal));
                Assert.DoesNotContain(FindVisualChildren<Button>(page), button =>
                    Equals(button.Content, "切换日语") || Equals(button.Content, "切换英语"));

                viewModel.AnswerText = "忘记了";
                viewModel.SubmitAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                RenderControl(page);
                Assert.Equal(Visibility.Visible, FindPanel(page, "FeedbackPanel").Visibility);
                Assert.Equal(Visibility.Visible, replay.Visibility);
                Assert.Contains(FindVisualChildren<TextBlock>(page), text =>
                    text.Text.Contains("たべます", StringComparison.Ordinal));

                viewModel.OpenBrowseCommand.Execute(null);
                RenderControl(page);
                Assert.Equal(Visibility.Visible, FindPanel(page, "BrowsePanel").Visibility);
                Assert.Equal(Visibility.Collapsed, FindPanel(page, "QuestionPanel").Visibility);
                Assert.Contains(FindVisualChildren<TextBlock>(page), text => text.Text == "食べる");
                Assert.Contains(FindVisualChildren<TextBlock>(page), text =>
                    text.Text.Contains("吃", StringComparison.Ordinal));
                Assert.Contains(FindVisualChildren<TextBlock>(page), text =>
                    text.Text == viewModel.SelectedBrowseMember!.PartOfSpeechLabel);
                Assert.Contains(FindVisualChildren<TextBlock>(page), text =>
                    text.Text == viewModel.SelectedBrowseMember!.AttributeLabel);
                Assert.Equal("Polite", viewModel.SelectedBrowseForm!.FormCode);
                Assert.Contains(FindVisualChildren<TextBlock>(page), text =>
                    text.Text == viewModel.SelectedBrowseForm.Surface);
                var browseReplay = Assert.IsType<Button>(page.FindName("ReplayBrowseButton"));
                Assert.Equal("播放所选词形", AutomationProperties.GetName(browseReplay));
                Assert.IsType<TextBlock>(browseReplay.Content);

                viewModel.CloseBrowseCommand.Execute(null);
                for (var index = 0; index < 2; index++)
                {
                    viewModel.ContinueAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                    viewModel.AnswerText = "たべます";
                    viewModel.SubmitAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                }
                viewModel.ContinueAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                RenderControl(page);

                Assert.Equal(Visibility.Visible, FindPanel(page, "SummaryPanel").Visibility);
                Assert.Equal(Visibility.Collapsed, FindPanel(page, "QuestionPanel").Visibility);
                Assert.Contains(FindVisualChildren<TextBlock>(page), text =>
                    text.Text.Contains("本轮完成", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void LearningPage_StartupFailure_OffersWorkingRetryButton()
        {
            RunOnSta(() =>
            {
                using var environment = LearningPresentationTestEnvironment.CreateAsync()
                    .GetAwaiter().GetResult();
                using var viewModel = environment.CreateViewModel(
                    new FailOncePlanReader(environment.PlanReader));
                viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken)
                    .GetAwaiter().GetResult();
                Assert.NotNull(viewModel.ErrorMessage);
                Assert.Null(viewModel.Question);
                var page = new PracticeSessionPage { DataContext = viewModel };
                RenderControl(page);

                var retry = Assert.IsType<Button>(page.FindName("RetryStartButton"));
                Assert.Equal(Visibility.Visible, retry.Visibility);
                Assert.Same(viewModel.RetryStartCommand, retry.Command);
                Assert.True(retry.Command.CanExecute(null));
                retry.Command.Execute(null);
                Assert.True(SpinWait.SpinUntil(
                    () => viewModel.Question is not null && !viewModel.IsBusy,
                    TimeSpan.FromSeconds(5)));
                Assert.Null(viewModel.ErrorMessage);
            });
        }

        private sealed class FailOncePlanReader : ILearningPlanReader
        {
            private readonly ILearningPlanReader inner;
            private bool failed;

            public FailOncePlanReader(ILearningPlanReader inner)
            {
                this.inner = inner;
            }

            public Task<LearningPlanDto> LoadAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                if (!failed)
                {
                    failed = true;
                    return Task.FromException<LearningPlanDto>(
                        new InvalidOperationException("计划暂不可用"));
                }
                return inner.LoadAsync(language, cancellationToken);
            }
        }

        private static Border FindPanel(FrameworkElement page, string name)
        {
            return Assert.IsType<Border>(page.FindName(name));
        }

        private static ServiceProvider CreateProvider(
            LearningPresentationTestEnvironment environment)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(environment.Paths.Root).AddDictationCore();
            services.AddSingleton<ITtsProvider>(environment.Tts);
            services.AddDictationPresentation();
            services.AddSingleton<IAudioPlayer>(environment.Audio);
            return services.BuildServiceProvider();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root is T match)
            {
                yield return match;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in FindVisualChildren<T>(
                    VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }

        private static void RenderControl(UserControl control)
        {
            control.Width = 1024;
            control.Height = 720;
            control.ApplyTemplate();
            control.Measure(new Size(control.Width, control.Height));
            control.Arrange(new Rect(0, 0, control.Width, control.Height));
            control.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.DataBind,
                new Action(() => { }));
            control.UpdateLayout();
        }

        private static void RunOnSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception caught)
                {
                    exception = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception is not null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private sealed class RecordingHomeNavigation : IHomeNavigation
        {
            public Task OpenReviewSessionAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task OpenReviewPlanAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task OpenReviewLibraryAsync(LearningLanguage language, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task OpenTextImportAsync(LearningLanguage language, string? initialFilePath = null,
                CancellationToken cancellationToken = default) => Task.CompletedTask;

            public TaskCompletionSource<LearningLanguage> OpenedLearning { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public Task OpenLexiconLookupAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task OpenImportInboxAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task OpenLearningPlanAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task OpenLearningAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                OpenedLearning.TrySetResult(language);
                return Task.CompletedTask;
            }
        }
    }
}
