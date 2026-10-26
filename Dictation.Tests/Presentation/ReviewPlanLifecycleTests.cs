using Dictation.Core.Configuration;
using Dictation.Core.Lexicon;
using Dictation.Core.Review;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace Dictation.Tests.Presentation
{
    /// <summary>仅控制真实查询的完成时机，检验后台执行、取消、退场与错误恢复。</summary>
    public sealed class ReviewPlanLifecycleTests
    {
        [Fact]
        public Task Refresh_BackgroundReadKeepsDispatcherResponsiveAndMutatesCollectionOnUi() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            var query = new ControlledQuery(provider.GetRequiredService<IReviewLibraryQuery>());
            using var vm = CreateVm(provider, query);
            var uiThread = Environment.CurrentManagedThreadId;
            var collectionThread = 0;
            vm.Groups.CollectionChanged += (_, _) => collectionThread = Environment.CurrentManagedThreadId;
            var load = vm.RefreshAsync(TestContext.Current.CancellationToken);
            try
            {
                Assert.False(load.IsCompleted);
                await query.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                var responsive = false;
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { responsive = vm.IsBusy; query.Release.TrySetResult(); });
                await load;
                Assert.True(responsive);
                Assert.NotEqual(uiThread, query.WorkThread);
                Assert.Equal(uiThread, collectionThread);
                Assert.Single(vm.Groups);
            }
            finally { query.Release.TrySetResult(); await load; }
        });

        [Fact]
        public async Task Dispose_CancelsReadAndSuppressesLateCollectionAndPropertyNotifications()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            var query = new ControlledQuery(provider.GetRequiredService<IReviewLibraryQuery>());
            using var vm = CreateVm(provider, query);
            var load = vm.RefreshAsync(TestContext.Current.CancellationToken);
            try
            {
                Assert.False(load.IsCompleted);
                await query.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                vm.Dispose();
                var notifications = 0;
                vm.PropertyChanged += (_, _) => notifications++;
                vm.Groups.CollectionChanged += (_, _) => notifications++;
                query.Release.TrySetResult();
                await load;
                Assert.True(query.SeenToken.IsCancellationRequested);
                Assert.Empty(vm.Groups);
                Assert.Equal(0, notifications);
                Assert.False(vm.SaveCommand.CanExecute(null));
                await Assert.ThrowsAsync<ObjectDisposedException>(() => vm.RefreshAsync(TestContext.Current.CancellationToken));
            }
            finally { query.Release.TrySetResult(); await load; }
        }

        [Fact]
        public async Task Cancel_ReadDiscardsLateResultAndCanRetry()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            var query = new ControlledQuery(provider.GetRequiredService<IReviewLibraryQuery>());
            using var vm = CreateVm(provider, query);
            var load = vm.RefreshAsync(TestContext.Current.CancellationToken);
            try
            {
                Assert.False(load.IsCompleted);
                await query.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                Assert.False(vm.SaveCommand.CanExecute(null));
                vm.CancelCommand.Execute(null);
                query.Release.TrySetResult();
                await load;
                Assert.Empty(vm.Groups);
                Assert.Contains("取消", vm.StatusMessage);
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                Assert.Single(vm.Groups);
                Assert.Null(vm.ErrorMessage);
            }
            finally { query.Release.TrySetResult(); await load; }
        }

        [Fact]
        public async Task Refresh_ExternalCancellationPropagatesWithoutApplyingLatePlan()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            var query = new ControlledQuery(provider.GetRequiredService<IReviewLibraryQuery>());
            using var vm = CreateVm(provider, query);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var load = vm.RefreshAsync(cancellation.Token);
            try
            {
                Assert.False(load.IsCompleted);
                await query.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                cancellation.Cancel(); query.Release.TrySetResult();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
                Assert.Empty(vm.Groups);
                Assert.Equal(0, vm.DailyReviewTarget);
                Assert.False(vm.IsBusy);
            }
            finally { query.Release.TrySetResult(); try { await load; } catch (OperationCanceledException) { } }
        }

        [Fact]
        public async Task Navigation_BackCancelsLoadBeforeRetiringPageScope()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path, configure: services =>
            {
                services.AddSingleton<ReviewLibraryService>();
                services.AddSingleton<IReviewLibraryQuery>(provider => new ControlledQuery(provider.GetRequiredService<ReviewLibraryService>()));
            });
            var query = Assert.IsType<ControlledQuery>(provider.GetRequiredService<IReviewLibraryQuery>());
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            navigation.InitializeWorkspace(home);
            var open = navigation.OpenReviewPlanAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            var vm = Assert.IsType<ReviewPlanViewModel>(navigation.CurrentPage);
            try
            {
                Assert.False(open.IsCompleted);
                await query.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                navigation.CompleteTransition();
                await navigation.GoBackAsync(TestContext.Current.CancellationToken);
                Assert.True(query.SeenToken.IsCancellationRequested);
                Assert.Same(home, navigation.CurrentPage);
                navigation.CompleteTransition();
                query.Release.TrySetResult(); await open;
                Assert.Empty(vm.Groups);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => vm.SaveAsync(TestContext.Current.CancellationToken));
            }
            finally { query.Release.TrySetResult(); await open; }
        }

        [Fact]
        public async Task Refresh_RejectsWrongLanguageAndUninitializedPage()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            using var vm = CreateVm(provider, new WrongLanguageQuery(), initialize: false);
            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.RefreshAsync(TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentOutOfRangeException>(() => vm.Initialize((LearningLanguage)99));
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Contains("语言", vm.ErrorMessage);
            Assert.Empty(vm.Groups);
        }

        private static ReviewPlanViewModel CreateVm(ServiceProvider provider, IReviewLibraryQuery query, bool initialize = true)
        {
            var vm = new ReviewPlanViewModel(provider.GetRequiredService<IReviewPlanReader>(), provider.GetRequiredService<IReviewPlanEditor>(),
                query, provider.GetRequiredService<IReviewLibraryManager>(), provider.GetRequiredService<ILexiconSearch>(),
                provider.GetRequiredService<IBackNavigation>(), provider.GetRequiredService<IOverlayRequester>());
            if (initialize) vm.Initialize(LearningLanguage.Japanese);
            return vm;
        }

        /// <summary>等待测试放行后仍调用真实 Core/SQLite，模拟外部依赖忽略取消的迟到响应。</summary>
        private sealed class ControlledQuery(IReviewLibraryQuery inner) : IReviewLibraryQuery
        {
            private int calls;
            internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal CancellationToken SeenToken { get; private set; }
            internal int WorkThread { get; private set; }
            public async Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    SeenToken = cancellationToken; WorkThread = Environment.CurrentManagedThreadId;
                    Started.TrySetResult();
                    await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                }
                return await inner.LoadAsync(language, CancellationToken.None);
            }
        }

        /// <summary>仅提供违反语言合同的响应，用于验证页面拒绝串入另一门语言。</summary>
        private sealed class WrongLanguageQuery : IReviewLibraryQuery
        {
            public Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default) =>
                Task.FromResult(new ReviewLibraryDto(LearningLanguage.English, DateTimeOffset.UtcNow, Array.Empty<ReviewWordGroupDto>()));
        }
    }
}
