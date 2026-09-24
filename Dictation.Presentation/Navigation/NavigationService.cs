using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dictation.Presentation.ViewModels;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 维护单窗口页面栈、异步导航互斥与转场结束后的页面作用域释放。
    /// </summary>
    internal sealed class NavigationService :
        IHomeNavigation,
        IBackNavigation,
        IImportInboxDetailNavigation,
        INavigationHost,
        IDisposable
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly List<NavigationEntry> backStack = new();
        private readonly List<NavigationEntry> pendingDisposals = new();
        private NavigationEntry? currentEntry;
        private bool isInitialized;
        // 转场期间旧页面仍由动画使用；只有宿主确认完成后才能释放其作用域。
        private bool transitionPending;
        // 异步加载期间拒绝第二次前进或返回，避免两个完成顺序争用同一页面栈。
        private bool navigationOperationPending;
        private bool isDisposed;

        /// <summary>
        /// 维护单窗口页面栈、异步导航互斥与转场结束后的页面作用域释放。
        /// </summary>
        /// <param name="scopeFactory">为每个导航页面创建独立服务作用域的工厂。</param>
        public NavigationService(IServiceScopeFactory scopeFactory)
        {
            this.scopeFactory = scopeFactory
                ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<NavigationTransitionEventArgs>? TransitionRequested;

        public object? CurrentPage => currentEntry?.ViewModel;

        public string PageTitle => currentEntry?.Title ?? string.Empty;

        public bool CanGoBack => backStack.Count > 0
            && !transitionPending
            && !navigationOperationPending;

        public NavigationDirection LastDirection { get; private set; } = NavigationDirection.Forward;

        /// <summary>
        /// 设置首页状态；首页由应用级单例提供，不创建页面作用域。
        /// </summary>
        /// <param name="homeViewModel">应用级首页模型。</param>
        public void InitializeHome(HomeViewModel homeViewModel)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            ArgumentNullException.ThrowIfNull(homeViewModel);
            if (isInitialized)
            {
                throw new InvalidOperationException("首页已经初始化，不能重复初始化。");
            }

            currentEntry = new NavigationEntry(
                homeViewModel, "首页", null, null, homeViewModel.RefreshLearningProgressAsync);
            isInitialized = true;
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(LastDirection));
        }

        /// <inheritdoc />
        public Task OpenLexiconLookupAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryPrepareForwardNavigation())
            {
                return Task.CompletedTask;
            }

            var scope = scopeFactory.CreateScope();
            try
            {
                var viewModel = scope.ServiceProvider.GetRequiredService<LexiconLookupViewModel>();
                viewModel.Initialize(language);
                cancellationToken.ThrowIfCancellationRequested();
                NavigateForward(
                    new NavigationEntry(viewModel, viewModel.WindowTitle, language, scope, null));
            }
            catch
            {
                scope.Dispose();
                throw;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task OpenImportInboxAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryPrepareForwardNavigation())
            {
                return;
            }

            navigationOperationPending = true;
            OnPropertyChanged(nameof(CanGoBack));
            try
            {
                var scope = scopeFactory.CreateScope();
                try
                {
                    var viewModel = scope.ServiceProvider.GetRequiredService<ImportInboxListViewModel>();
                    viewModel.Initialize(language);
                    await viewModel.RefreshAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    NavigateForward(
                        new NavigationEntry(
                            viewModel,
                            viewModel.WindowTitle,
                            language,
                            scope,
                            viewModel.RefreshPreservingStateAsync));
                }
                catch
                {
                    scope.Dispose();
                    throw;
                }
            }
            finally
            {
                navigationOperationPending = false;
                OnPropertyChanged(nameof(CanGoBack));
            }
        }

        /// <inheritdoc />
        public async Task OpenLearningPlanAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryPrepareForwardNavigation())
            {
                return;
            }

            navigationOperationPending = true;
            OnPropertyChanged(nameof(CanGoBack));
            try
            {
                var scope = scopeFactory.CreateScope();
                try
                {
                    var viewModel = scope.ServiceProvider.GetRequiredService<LearningPlanViewModel>();
                    viewModel.Initialize(language);
                    await viewModel.RefreshAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    NavigateForward(new NavigationEntry(
                        viewModel,
                        viewModel.WindowTitle,
                        language,
                        scope,
                        viewModel.RefreshPreservingStateAsync));
                }
                catch
                {
                    scope.Dispose();
                    throw;
                }
            }
            finally
            {
                navigationOperationPending = false;
                OnPropertyChanged(nameof(CanGoBack));
            }
        }

        /// <inheritdoc />
        public async Task OpenLearningAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default)
        {
            ValidateLanguage(language);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryPrepareForwardNavigation())
            {
                return;
            }

            navigationOperationPending = true;
            OnPropertyChanged(nameof(CanGoBack));
            try
            {
                var scope = scopeFactory.CreateScope();
                try
                {
                    var viewModel = scope.ServiceProvider.GetRequiredService<LearningSessionViewModel>();
                    viewModel.Initialize(language);
                    cancellationToken.ThrowIfCancellationRequested();
                    NavigateForward(new NavigationEntry(
                        viewModel,
                        viewModel.WindowTitle,
                        language,
                        scope,
                        null));
                    // 首题听写可能等待语音服务；先展示页面，用户才能使用页面内的取消命令。
                    await viewModel.StartAsync(cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested
                    && ReferenceEquals(currentEntry?.Scope, scope))
                {
                    // 页面内取消只停止首题准备；页面仍可重试或返回。
                }
                catch
                {
                    if (!ReferenceEquals(currentEntry?.Scope, scope))
                    {
                        scope.Dispose();
                    }
                    throw;
                }
            }
            finally
            {
                navigationOperationPending = false;
                OnPropertyChanged(nameof(CanGoBack));
            }
        }

        /// <inheritdoc />
        public Task OpenImportInboxDetailAsync(
            ImportBatchDto batch,
            long itemId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateLanguage(batch.Language);
            if (!TryPrepareForwardNavigation())
            {
                return Task.CompletedTask;
            }

            if (currentEntry?.ViewModel is not ImportInboxListViewModel
                || currentEntry.Language != batch.Language)
            {
                throw new InvalidOperationException("导入详情只能从相同语言的收件箱列表打开。");
            }

            var scope = scopeFactory.CreateScope();
            try
            {
                var viewModel = scope.ServiceProvider.GetRequiredService<ImportInboxDetailViewModel>();
                viewModel.Initialize(batch, itemId);
                cancellationToken.ThrowIfCancellationRequested();
                NavigateForward(
                    new NavigationEntry(
                        viewModel,
                        viewModel.WindowTitle,
                        batch.Language,
                        scope,
                        null));
            }
            catch
            {
                scope.Dispose();
                throw;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task GoBackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();
            if (!CanGoBack)
            {
                return;
            }

            EnsureTransitionCompleted();
            var previousEntry = backStack[^1];
            navigationOperationPending = true;
            OnPropertyChanged(nameof(CanGoBack));
            try
            {
                if (previousEntry.ResumeAsync is not null)
                {
                    await previousEntry.ResumeAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var poppedEntry = currentEntry!;
                backStack.RemoveAt(backStack.Count - 1);
                currentEntry = previousEntry;
                pendingDisposals.Add(poppedEntry);
                LastDirection = NavigationDirection.Backward;
                transitionPending = true;
                RaiseNavigationChanged();
                TransitionRequested?.Invoke(
                    this,
                    new NavigationTransitionEventArgs(
                        poppedEntry.ViewModel,
                        poppedEntry.Title,
                        previousEntry.ViewModel,
                        previousEntry.Title,
                        LastDirection));
            }
            finally
            {
                navigationOperationPending = false;
                OnPropertyChanged(nameof(CanGoBack));
            }
        }

        /// <inheritdoc />
        public void CompleteTransition()
        {
            if (isDisposed)
            {
                return;
            }

            if (!transitionPending)
            {
                return;
            }

            transitionPending = false;
            DisposeEntries(pendingDisposals);
            pendingDisposals.Clear();
            OnPropertyChanged(nameof(CanGoBack));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            transitionPending = false;
            DisposeEntries(pendingDisposals);
            pendingDisposals.Clear();
            DisposeEntries(backStack);
            backStack.Clear();
            if (currentEntry is not null)
            {
                currentEntry.Scope?.Dispose();
                currentEntry = null;
            }
        }

        /// <summary>
        /// 将调用方已构建的目标页置为当前页，并把旧页连同作用域压入返回栈，发起前进转场。
        /// </summary>
        private void NavigateForward(NavigationEntry nextEntry)
        {
            EnsureTransitionCompleted();
            var previousEntry = currentEntry
                ?? throw new InvalidOperationException("首页尚未初始化。");
            backStack.Add(previousEntry);
            currentEntry = nextEntry;
            LastDirection = NavigationDirection.Forward;
            transitionPending = true;
            RaiseNavigationChanged();
            TransitionRequested?.Invoke(
                this,
                new NavigationTransitionEventArgs(
                    previousEntry.ViewModel,
                    previousEntry.Title,
                    nextEntry.ViewModel,
                    nextEntry.Title,
                    LastDirection));
        }

        private bool TryPrepareForwardNavigation()
        {
            EnsureInitialized();
            if (navigationOperationPending)
            {
                return false;
            }

            EnsureTransitionCompleted();
            return true;
        }

        private void EnsureInitialized()
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!isInitialized || currentEntry is null)
            {
                throw new InvalidOperationException("导航首页尚未初始化。");
            }
        }

        private void EnsureTransitionCompleted()
        {
            if (transitionPending)
            {
                throw new InvalidOperationException("上一段页面转场尚未完成。");
            }
        }

        private void RaiseNavigationChanged()
        {
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(LastDirection));
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "导航语言值无效。");
            }
        }

        private static void DisposeEntries(IEnumerable<NavigationEntry> entries)
        {
            foreach (var entry in entries)
            {
                entry.Scope?.Dispose();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed record NavigationEntry(
            object ViewModel,
            string Title,
            LearningLanguage? Language,
            IServiceScope? Scope,
            Func<CancellationToken, Task>? ResumeAsync);
    }
}
