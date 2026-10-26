using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Review;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证复习词库的真实绑定、单窗口导航和后台查询生命周期。</summary>
    public sealed class ReviewLibraryPageTests
    {
        [Fact]
        public async Task Refresh_RealChineseGroupsAndSelectionSurviveRefresh()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var vm = CreateVm(provider);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            var group = Assert.Single(vm.Groups);
            Assert.Equal("食べる", group.HeadwordLabel);
            Assert.Contains("吃", group.MeaningLabel);
            Assert.Equal("待测", group.StateLabel);
            Assert.Equal("尚未测试", group.NextReviewLabel);
            Assert.Equal(4, group.Forms.Count);
            vm.SelectedGroup = group;
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal(group.WordGroupId, vm.SelectedGroup?.WordGroupId);
            Assert.NotSame(group, vm.SelectedGroup);
            Assert.Contains("1 组", vm.StatusMessage);
        }

        [Fact]
        public Task Page_RendersCardsMeaningFormsAndCommandsWithoutLanguageSwitch() => RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var vm = CreateVm(provider);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            vm.SelectedGroup = Assert.Single(vm.Groups);
            var page = new ReviewLibraryPage { DataContext = vm };
            Render(page);
            Assert.Contains(Children<TextBlock>(page), text => text.Text == "食べる");
            Assert.Contains(Children<TextBlock>(page), text => text.Text.Contains("吃"));
            Assert.Contains(Children<TextBlock>(page), text => text.Text.Contains("て形"));
            Assert.Contains(Children<TextBlock>(page), text => text.Text == "尚未评价");
            Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, vm.RefreshCommand));
            Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, vm.BackCommand));
            Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, vm.CancelCommand));
            Assert.Empty(Children<DataGrid>(page));
            Assert.DoesNotContain(Children<Button>(page), button => Equals(button.Content, "切换日语") || Equals(button.Content, "切换英语"));
            var list = Assert.Single(Children<ListBox>(page));
            Assert.Same(vm.SelectedGroup, list.SelectedItem);
            list.SelectedItem = null;
            Assert.Null(vm.SelectedGroup);
        });

        [Fact]
        public Task Home_ReviewEntryBindsCurrentLanguageAndReturnsSameHome() => RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            await home.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            navigation.InitializeWorkspace(home);
            var page = new LanguageWorkspacePage { DataContext = home };
            Render(page);
            Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, home.OpenReviewLibraryCommand));
            await home.OpenReviewLibraryAsync(TestContext.Current.CancellationToken);
            var review = Assert.IsType<ReviewLibraryViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, review.SelectedLanguage);
            Assert.Equal("英语复习词库", navigation.PageTitle);
            navigation.CompleteTransition();
            await navigation.GoBackAsync(TestContext.Current.CancellationToken);
            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, home.SelectedLanguage);
            navigation.CompleteTransition();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => review.RefreshAsync(TestContext.Current.CancellationToken));
        });

        [Fact]
        public Task Refresh_BackgroundQueryKeepsDispatcherResponsiveAndAppliesOnUi() => RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingQuery(provider.GetRequiredService<IReviewLibraryQuery>());
            using var vm = CreateVm(provider, blocked);
            var uiThread = Environment.CurrentManagedThreadId;
            var rowsThread = 0;
            vm.Groups.CollectionChanged += (_, _) => rowsThread = Environment.CurrentManagedThreadId;
            var load = vm.RefreshAsync(TestContext.Current.CancellationToken);
            try
            {
                Assert.False(load.IsCompleted);
                var wasBusy = false;
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { wasBusy = vm.IsBusy; blocked.Release.Set(); });
                await load;
                Assert.True(wasBusy);
                Assert.NotEqual(uiThread, blocked.WorkThread);
                Assert.Equal(uiThread, rowsThread);
                Assert.Single(vm.Groups);
            }
            finally { blocked.Release.Set(); await load; }
        });

        [Fact]
        public async Task Refresh_CancelCommandDiscardsLateResultAndAllowsRetry()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingQuery(provider.GetRequiredService<IReviewLibraryQuery>(), ignoreCancellation: true);
            using var vm = CreateVm(provider, blocked);
            var load = vm.RefreshAsync(TestContext.Current.CancellationToken);
            try
            {
                Assert.False(load.IsCompleted);
                await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                Assert.True(vm.CancelCommand.CanExecute(null));
                vm.CancelCommand.Execute(null);
                blocked.Release.Set();
                await load;
                Assert.True(blocked.SeenToken.IsCancellationRequested);
                Assert.Empty(vm.Groups);
                Assert.False(vm.IsBusy);
                Assert.Contains("取消", vm.StatusMessage);
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                Assert.Single(vm.Groups);
                Assert.Null(vm.ErrorMessage);
            }
            finally { blocked.Release.Set(); await load; }
        }

        [Fact]
        public async Task Refresh_DisposeCancelsAndSuppressesAllLateNotifications()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingQuery(provider.GetRequiredService<IReviewLibraryQuery>(), ignoreCancellation: true);
            using var vm = CreateVm(provider, blocked);
            var load = vm.RefreshAsync(TestContext.Current.CancellationToken);
            try
            {
                Assert.False(load.IsCompleted);
                await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                vm.Dispose();
                var notifications = 0;
                vm.PropertyChanged += (_, _) => notifications++;
                vm.Groups.CollectionChanged += (_, _) => notifications++;
                blocked.Release.Set();
                await load;
                Assert.True(blocked.SeenToken.IsCancellationRequested);
                Assert.Empty(vm.Groups);
                Assert.Equal(0, notifications);
                Assert.False(vm.RefreshCommand.CanExecute(null));
                await Assert.ThrowsAsync<ObjectDisposedException>(() => vm.RefreshAsync(TestContext.Current.CancellationToken));
            }
            finally { blocked.Release.Set(); await load; }
        }

        [Fact]
        public async Task Refresh_ExternalCancellationPropagatesAndDoesNotApplyResults()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingQuery(provider.GetRequiredService<IReviewLibraryQuery>(), ignoreCancellation: true);
            using var vm = CreateVm(provider, blocked);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var load = vm.RefreshAsync(cancellation.Token);
            try
            {
                Assert.False(load.IsCompleted);
                await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                cancellation.Cancel();
                blocked.Release.Set();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
                Assert.Empty(vm.Groups);
                Assert.False(vm.IsBusy);
            }
            finally { blocked.Release.Set(); try { await load; } catch (OperationCanceledException) { } }
        }

        [Fact]
        public async Task Navigation_BackDuringLoadCancelsBeforeTransitionDisposesScope()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path, services =>
            {
                services.AddSingleton<ReviewLibraryService>();
                services.AddSingleton<IReviewLibraryQuery>(provider => new BlockingQuery(provider.GetRequiredService<ReviewLibraryService>(), true));
            });
            var blocked = Assert.IsType<BlockingQuery>(provider.GetRequiredService<IReviewLibraryQuery>());
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            navigation.InitializeWorkspace(home);
            var open = navigation.OpenReviewLibraryAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            try
            {
                Assert.False(open.IsCompleted);
                await blocked.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                var review = Assert.IsType<ReviewLibraryViewModel>(navigation.CurrentPage);
                navigation.CompleteTransition();
                Assert.True(navigation.CanGoBack);
                await navigation.GoBackAsync(TestContext.Current.CancellationToken);
                Assert.True(blocked.SeenToken.IsCancellationRequested);
                Assert.Same(home, navigation.CurrentPage);
                navigation.CompleteTransition();
                blocked.Release.Set();
                await open;
                Assert.Empty(review.Groups);
            }
            finally { blocked.Release.Set(); await open; }
        }

        [Fact]
        public async Task Refresh_ReadFailureIsVisibleAndNextRefreshCanRecover()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var vm = CreateVm(provider, new FailedOnceQuery(provider.GetRequiredService<IReviewLibraryQuery>()));
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Contains("读取失败", vm.ErrorMessage);
            Assert.False(vm.IsBusy);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Single(vm.Groups);
            Assert.Null(vm.ErrorMessage);
        }

        [Fact]
        public async Task Refresh_RejectsCrossLanguageResponseAndUninitializedScope()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var vm = new ReviewLibraryViewModel(new WrongLanguageQuery(), provider.GetRequiredService<IBackNavigation>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.RefreshAsync(TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentOutOfRangeException>(() => vm.Initialize((LearningLanguage)99));
            vm.Initialize(LearningLanguage.Japanese);
            Assert.Throws<InvalidOperationException>(() => vm.Initialize(LearningLanguage.English));
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Contains("语言", vm.ErrorMessage);
            Assert.Empty(vm.Groups);
        }

        private static ReviewLibraryViewModel CreateVm(ServiceProvider provider, IReviewLibraryQuery? query = null)
        {
            var vm = new ReviewLibraryViewModel(query ?? provider.GetRequiredService<IReviewLibraryQuery>(), provider.GetRequiredService<IBackNavigation>());
            vm.Initialize(LearningLanguage.Japanese);
            return vm;
        }

        private static async Task<ServiceProvider> CreateProviderAsync(string root, Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(root).AddDictationCore().AddDictationPresentation();
            configure?.Invoke(services);
            var provider = services.BuildServiceProvider();
            var paths = provider.GetRequiredService<ApplicationPaths>();
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await provider.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(TestContext.Current.CancellationToken);
            var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                LearningLanguage.Japanese, ImportSourceKind.Manual, new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }), TestContext.Current.CancellationToken);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                staged.Batch.BatchId, new[] { Assert.Single(staged.Batch.Items).ItemId }, ImportDecision.Review), TestContext.Current.CancellationToken);
            return provider;
        }

        private static void Render(UserControl page)
        {
            page.Width = 1200;
            page.Height = 900;
            page.Measure(new Size(1200, 900));
            page.Arrange(new Rect(0, 0, 1200, 900));
            page.UpdateLayout();
        }

        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item) yield return item;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
        }

        private static Task RunStaAsync(Func<Task> action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try { await action(); completion.SetResult(); }
                    catch (Exception exception) { completion.SetException(exception); }
                    finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                }));
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return completion.Task;
        }

        /// <summary>只控制真实查询开始的时机，仍由 Core 和 SQLite 返回完整结果。</summary>
        private sealed class BlockingQuery(IReviewLibraryQuery inner, bool ignoreCancellation = false) : IReviewLibraryQuery, IDisposable
        {
            private int calls;
            public ManualResetEventSlim Release { get; } = new();
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken SeenToken { get; private set; }
            public int WorkThread { get; private set; }
            public Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    SeenToken = cancellationToken;
                    WorkThread = Environment.CurrentManagedThreadId;
                    Started.TrySetResult();
                    Release.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                }
                return inner.LoadAsync(language, ignoreCancellation ? CancellationToken.None : cancellationToken);
            }
            public void Dispose() => Release.Dispose();
        }

        /// <summary>模拟一次读取故障，不替代随后重试的真实数据库查询。</summary>
        private sealed class FailedOnceQuery(IReviewLibraryQuery inner) : IReviewLibraryQuery
        {
            private bool failed;
            public Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
            {
                if (!failed) { failed = true; throw new IOException("读取失败"); }
                return inner.LoadAsync(language, cancellationToken);
            }
        }

        /// <summary>提供错误语言的合同响应，检验页面拒绝跨语言绑定。</summary>
        private sealed class WrongLanguageQuery : IReviewLibraryQuery
        {
            public Task<ReviewLibraryDto> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default) =>
                Task.FromResult(new ReviewLibraryDto(LearningLanguage.English, DateTimeOffset.UtcNow, Array.Empty<ReviewWordGroupDto>()));
        }
    }
}
