using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dictation.Presentation.ViewModels;
using Dictation.Services.Configuration;
using Dictation.Services.Importing;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation.Navigation
{
    public sealed class NavigationService : INotifyPropertyChanged, IDisposable
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly List<NavigationEntry> backStack = new();
        private readonly List<NavigationEntry> pendingDisposals = new();
        private NavigationEntry? currentEntry;
        private bool isInitialized;
        private bool transitionPending;
        private bool navigationOperationPending;
        private bool isDisposed;

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

        public void InitializeHome(HomeViewModel homeViewModel)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            ArgumentNullException.ThrowIfNull(homeViewModel);
            if (isInitialized)
            {
                throw new InvalidOperationException("首页已经初始化，不能重复初始化。");
            }

            currentEntry = new NavigationEntry(homeViewModel, "首页", null, null);
            isInitialized = true;
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(LastDirection));
        }

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
                    new NavigationEntry(viewModel, viewModel.WindowTitle, language, scope));
            }
            catch
            {
                scope.Dispose();
                throw;
            }

            return Task.CompletedTask;
        }

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
                        new NavigationEntry(viewModel, viewModel.WindowTitle, language, scope));
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

        public Task OpenImportInboxDetailAsync(
            ImportBatchDto batch,
            long itemId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemId);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();
            throw new NotSupportedException("导入收件箱详情页将在收件箱页面化检查点实现。");
        }

        public Task GoBackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();
            if (!CanGoBack)
            {
                return Task.CompletedTask;
            }

            EnsureTransitionCompleted();
            var poppedEntry = currentEntry!;
            var previousEntry = backStack[^1];
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
            return Task.CompletedTask;
        }

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
            IServiceScope? Scope);
    }
}
