using System.Windows.Threading;
using Dictation.Core;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Core.Tts;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Importing;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证真实 Dispatcher 在导入时仍可响应，取消及离页不会继续应用过期结果。</summary>
    public sealed class TextImportLifecycleTests
    {
        [Fact]
        public async Task Prepare_SynchronousDatabaseWorkRunsOffDispatcherAndRowsReturnOnDispatcher()
        {
            await RunOnDispatcherAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await CreateProviderAsync(directory.Path);
                using var blocked = new BlockingPreparation(provider.GetRequiredService<TextImportPreparationService>());
                using var vm = CreateVm(provider, blocked);
                var uiThread = Environment.CurrentManagedThreadId;
                var rowThread = 0;
                vm.Lines.CollectionChanged += (_, _) => rowThread = Environment.CurrentManagedThreadId;
                var load = vm.LoadTextAsync("食前", TestContext.Current.CancellationToken);
                try
                {
                    Assert.False(load.IsCompleted);
                    var respondedWhileBusy = false;
                    await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                    {
                        respondedWhileBusy = vm.IsBusy;
                        blocked.Release.Set();
                    });
                    await load;
                    Assert.True(respondedWhileBusy);
                    Assert.NotEqual(uiThread, blocked.WorkThread);
                    Assert.Equal(uiThread, rowThread);
                    Assert.Single(vm.Lines);
                }
                finally
                {
                    blocked.Release.Set();
                    await load;
                }
            });
        }

        [Fact]
        public async Task Prepare_CancelCommandStopsRunningWorkAndAllowsRetry()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingPreparation(provider.GetRequiredService<TextImportPreparationService>());
            using var vm = CreateVm(provider, blocked);
            var load = vm.LoadTextAsync("食前", TestContext.Current.CancellationToken);
            try
            {
                await blocked.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
                vm.CancelCommand.Execute(null);
                blocked.Release.Set();
                await load;
                Assert.True(blocked.SeenToken.IsCancellationRequested);
                Assert.Empty(vm.Lines);
                Assert.False(vm.IsBusy);
                Assert.Null(vm.ErrorMessage);
                Assert.Contains("取消", vm.StatusMessage);
                await vm.LoadTextAsync("食事", TestContext.Current.CancellationToken);
                Assert.Equal("食事", Assert.Single(vm.Lines).SourceText);
            }
            finally
            {
                blocked.Release.Set();
                await load;
            }
        }

        [Fact]
        public async Task Prepare_ExternalCancellationPropagatesAndDoesNotApplyRows()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingPreparation(provider.GetRequiredService<TextImportPreparationService>());
            using var vm = CreateVm(provider, blocked);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var load = vm.LoadTextAsync("食前", cancellation.Token);
            await blocked.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            cancellation.Cancel();
            blocked.Release.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
            Assert.Empty(vm.Lines);
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task Prepare_DisposeCancelsEvenUncooperativeResultAndStopsNotifications()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingPreparation(provider.GetRequiredService<TextImportPreparationService>(), ignoreCancellation: true);
            using var vm = CreateVm(provider, blocked);
            var load = vm.LoadTextAsync("食前", TestContext.Current.CancellationToken);
            await blocked.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            vm.Dispose();
            var notifications = 0;
            vm.PropertyChanged += (_, _) => notifications++;
            blocked.Release.Set();
            await load;
            Assert.True(blocked.SeenToken.IsCancellationRequested);
            Assert.Empty(vm.Lines);
            Assert.Equal(0, notifications);
            Assert.False(vm.CanEdit);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => vm.LoadTextAsync("食事", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Stage_CancelBeforeDatabaseCommitLeavesNoPartialInbox()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = new BlockingStaging(provider.GetRequiredService<IImportStaging>());
            using var vm = CreateVm(provider, provider.GetRequiredService<TextImportPreparationService>(), blocked);
            await vm.LoadTextAsync("食前\n食事", TestContext.Current.CancellationToken);
            var stage = vm.StageSelectedAsync(TestContext.Current.CancellationToken);
            await blocked.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            vm.CancelOperation();
            blocked.Release.Set();
            Assert.Null(await stage);
            Assert.True(blocked.SeenToken.IsCancellationRequested);
            Assert.Empty(await provider.GetRequiredService<IImportInboxQuery>().GetPendingBatchesAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken));
            Assert.False(vm.IsBusy);
            Assert.Null(vm.ErrorMessage);
        }

        [Fact]
        public async Task Navigation_StartupFileCanReturnWhileMatchingAndCancelsPage()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            using var blocked = (BlockingPreparation)provider.GetRequiredService<ITextImportPreparation>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeWorkspace(home);
            var path = Path.Combine(directory.Path, "words.txt");
            await File.WriteAllTextAsync(path, "食前", TestContext.Current.CancellationToken);
            var open = navigation.OpenTextImportAsync(LearningLanguage.Japanese, path, TestContext.Current.CancellationToken);
            await blocked.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            var vm = Assert.IsType<TextImportViewModel>(navigation.CurrentPage);
            navigation.CompleteTransition();
            try
            {
                Assert.True(navigation.CanGoBack);
                await navigation.GoBackAsync(TestContext.Current.CancellationToken);
                Assert.True(blocked.SeenToken.IsCancellationRequested);
                navigation.CompleteTransition();
                blocked.Release.Set();
                await open;
                Assert.Same(home, navigation.CurrentPage);
                Assert.Empty(vm.Lines);
            }
            finally
            {
                blocked.Release.Set();
                await open;
            }
        }

        private static TextImportViewModel CreateVm(ServiceProvider provider, ITextImportPreparation preparation,
            IImportStaging? staging = null)
        {
            var vm = new TextImportViewModel(preparation, staging ?? provider.GetRequiredService<IImportStaging>(),
                provider.GetRequiredService<IHomeNavigation>(), new Utf8TextFileReader());
            vm.Initialize(LearningLanguage.Japanese);
            return vm;
        }

        private static async Task<ServiceProvider> CreateProviderAsync(string root)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(root).AddDictationCore();
            services.AddSingleton<TextImportPreparationService>();
            services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
            services.AddDictationPresentation();
            // 导航测试通过真实页面作用域拥有此实例；直接 VM 测试使用内部真实服务。
            services.AddSingleton<ITextImportPreparation>(sp => new BlockingPreparation(
                sp.GetRequiredService<TextImportPreparationService>()));
            var provider = services.BuildServiceProvider();
            var paths = provider.GetRequiredService<ApplicationPaths>();
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await provider.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(TestContext.Current.CancellationToken);
            return provider;
        }

        private static Task RunOnDispatcherAsync(Func<Task> action)
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

        /// <summary>模拟 SQLite 在返回 Task 前同步工作的能力，并用闸门控制取消时机。</summary>
        private sealed class BlockingPreparation(ITextImportPreparation inner, bool ignoreCancellation = false)
            : ITextImportPreparation, IDisposable
        {
            public ManualResetEventSlim Release { get; } = new();
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken SeenToken { get; private set; }
            public int WorkThread { get; private set; }
            public Task<TextImportPreparationDto> PrepareAsync(LearningLanguage language, string text, CancellationToken cancellationToken = default)
            {
                WorkThread = Environment.CurrentManagedThreadId;
                SeenToken = cancellationToken;
                Started.TrySetResult();
                // 仅为失败实现设置安全超时，正确实现由 Dispatcher/测试主动放行。
                Release.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                return inner.PrepareAsync(language, text, ignoreCancellation ? CancellationToken.None : cancellationToken);
            }
            public void Dispose() => Release.Dispose();
        }

        /// <summary>在真实业务暂存前设置闸门，验证页面取消令牌仍传入真实数据库链路。</summary>
        private sealed class BlockingStaging(IImportStaging inner) : IImportStaging, IDisposable
        {
            public ManualResetEventSlim Release { get; } = new();
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationToken SeenToken { get; private set; }
            public Task<StageImportBatchResultDto> StageAsync(StageImportBatchRequestDto request, CancellationToken cancellationToken = default)
            {
                SeenToken = cancellationToken;
                Started.TrySetResult();
                Release.Wait(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
                return inner.StageAsync(request, cancellationToken);
            }
            public void Dispose() => Release.Dispose();
        }
    }
}
