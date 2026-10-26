using Dictation.Core.Configuration;
using Dictation.Core.Practice;
using Dictation.Core.Review;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证首题尚未准备时仍可取消、返回，迟到结果不能污染旧页或首页。</summary>
    public sealed class PracticePreparationBoundaryTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task Preparation_UserCanReturnOrCancelBeforeQuestionIsPublished(bool returnHome) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path, configure: services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                services.AddSingleton<IReviewQuestionReader>(p => new DelayedQuestionReader(
                    ActivatorUtilities.CreateInstance<ReviewQuestionService>(p), entered, gate));
            });
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeWorkspace(home);
            var opening = home.OpenReviewSessionAsync(Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            var vm = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            try
            {
                Assert.Null(vm.Question);
                Assert.True(vm.IsBusy);
                navigation.CompleteTransition();
                if (returnHome)
                {
                    Assert.True(navigation.CanGoBack);
                    await navigation.GoBackAsync(Token);
                    Assert.Same(home, navigation.CurrentPage);
                }
                else
                {
                    var page = new PracticeSessionPage { DataContext = vm };
                    page.Measure(new Size(1100, 1000)); page.Arrange(new Rect(0, 0, 1100, 1000)); page.UpdateLayout();
                    var cancel = Assert.IsType<Button>(page.FindName("CancelPracticeOperationButton"));
                    Assert.Equal(Visibility.Visible, cancel.Visibility);
                    Assert.Equal(Visibility.Collapsed, Assert.IsType<Border>(page.FindName("QuestionPanel")).Visibility);
                    Assert.False(Assert.IsType<Border>(page.FindName("QuestionPanel")).IsAncestorOf(cancel));
                    Assert.True(cancel.IsEnabled);
                    cancel.Command.Execute(null);
                }
                gate.TrySetResult();
                if (returnHome) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
                else await opening;
                Assert.Null(vm.Session);
                Assert.Null(vm.Question);
                Assert.Null(vm.ErrorMessage);
                navigation.CompleteTransition();
            }
            finally
            {
                vm.CancelOperation();
                gate.TrySetResult();
                try { await opening; } catch (OperationCanceledException) { }
            }
        });

        [Fact]
        public async Task HomeProgress_CancelledQueryLateFailurePreservesLastSuccessfulLabel()
        {
            using var directory = new TemporaryDirectory();
            var reader = new StubReviewProgressReader
            {
                ReadAsync = (language, _) => Task.FromResult(new ReviewDailyProgressDto(language, 3, 20))
            };
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path,
                configure: services => services.AddSingleton<IReviewProgressReader>(reader));
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.RefreshReviewProgressAsync(Token);
            Assert.Equal("复习 3/20", home.ReviewProgressLabel);
            var pending = new TaskCompletionSource<ReviewDailyProgressDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.ReadAsync = (_, _) => pending.Task;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            var refresh = home.RefreshReviewProgressAsync(cancellation.Token);
            cancellation.Cancel();
            pending.SetException(new IOException("取消后迟到的读取错误"));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
            Assert.Equal("复习 3/20", home.ReviewProgressLabel);
            Assert.Null(home.ReviewProgressErrorMessage);
        }

        /// <summary>仅延迟真实读题边界并故意忽略取消，保留原有持久化行为。</summary>
        private sealed class DelayedQuestionReader(IReviewQuestionReader inner, TaskCompletionSource entered, TaskCompletionSource release) : IReviewQuestionReader
        {
            /// <inheritdoc />
            public async Task<PracticeQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken = default)
            {
                entered.TrySetResult();
                await release.Task;
                return await inner.GetCurrentQuestionAsync(sessionId, CancellationToken.None);
            }
        }
    }
}
