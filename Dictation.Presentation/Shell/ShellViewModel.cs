using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation.Shell
{
    /// <summary>
    /// 向窗口转发导航和弹层状态，并提供宿主命令。
    /// </summary>
    public sealed class ShellViewModel : INotifyPropertyChanged
    {
        private readonly INavigationHost navigationHost;
        private readonly IOverlayHost overlayHost;

        /// <summary>
        /// 向窗口转发导航和弹层状态，并提供宿主命令。
        /// </summary>
        /// <param name="navigationHost">提供导航状态和转场生命周期的宿主能力。</param>
        /// <param name="overlayHost">提供弹层显示状态和完成操作的宿主能力。</param>
        /// <param name="settingsNavigation">从首页或语言工作页进入设置的导航能力。</param>
        /// <param name="statisticsNavigation">从当前语言工作页进入统计的能力。</param>
        public ShellViewModel(
            INavigationHost navigationHost,
            IOverlayHost overlayHost,
            ISettingsNavigation settingsNavigation,
            IStatisticsNavigation statisticsNavigation)
        {
            this.navigationHost = navigationHost
                ?? throw new ArgumentNullException(nameof(navigationHost));
            this.overlayHost = overlayHost
                ?? throw new ArgumentNullException(nameof(overlayHost));
            ArgumentNullException.ThrowIfNull(settingsNavigation);
            ArgumentNullException.ThrowIfNull(statisticsNavigation);
            GoBackCommand = new AsyncCommand(async () =>
            {
                await this.navigationHost.GoBackAsync();
            });
            AcceptOverlayCommand = new RelayCommand(this.overlayHost.Accept);
            CancelOverlayCommand = new RelayCommand(this.overlayHost.Cancel);
            DismissOverlayCommand = new RelayCommand(this.overlayHost.Dismiss);
            OpenSettingsCommand = new AsyncCommand(() => settingsNavigation.OpenSettingsAsync());
            OpenStatisticsCommand = new AsyncCommand(() => statisticsNavigation.OpenStatisticsAsync(
                this.navigationHost.CurrentPage is HomeViewModel home ? home.SelectedLanguage : throw new InvalidOperationException("请从语言工作页进入统计。")));
            this.navigationHost.PropertyChanged += OnNavigationPropertyChanged;
            this.overlayHost.PropertyChanged += OnOverlayPropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<NavigationTransitionEventArgs>? TransitionRequested
        {
            add => navigationHost.TransitionRequested += value;
            remove => navigationHost.TransitionRequested -= value;
        }

        public object? CurrentPage => navigationHost.CurrentPage;

        public string PageTitle => navigationHost.PageTitle;

        public bool CanGoBack => navigationHost.CanGoBack;

        public bool IsBackButtonVisible => CurrentPage is not null and not LandingViewModel;
        public bool IsSettingsButtonVisible => CurrentPage is LandingViewModel or HomeViewModel;

        public OverlayRequest? CurrentOverlay => overlayHost.CurrentRequest;

        public bool IsOverlayVisible => overlayHost.IsVisible;

        public bool IsOverlayBusy => overlayHost.IsBusy;

        public bool IsOverlayConfirmation => overlayHost.IsConfirmation;

        public ICommand GoBackCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenStatisticsCommand { get; }

        public ICommand AcceptOverlayCommand { get; }

        public ICommand CancelOverlayCommand { get; }

        public ICommand DismissOverlayCommand { get; }

        /// <summary>
        /// 通知导航宿主动画结束，以释放离开的页面作用域。
        /// </summary>
        public void CompleteTransition()
        {
            navigationHost.CompleteTransition();
        }

        private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
            if (e.PropertyName == nameof(INavigationHost.CurrentPage))
            {
                OnPropertyChanged(nameof(IsBackButtonVisible));
                OnPropertyChanged(nameof(IsSettingsButtonVisible));
            }
        }

        private void OnOverlayPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IOverlayHost.CurrentRequest))
            {
                OnPropertyChanged(nameof(CurrentOverlay));
            }
            else if (e.PropertyName == nameof(IOverlayHost.IsVisible))
            {
                OnPropertyChanged(nameof(IsOverlayVisible));
            }
            else if (e.PropertyName == nameof(IOverlayHost.IsBusy))
            {
                OnPropertyChanged(nameof(IsOverlayBusy));
            }
            else if (e.PropertyName == nameof(IOverlayHost.IsConfirmation))
            {
                OnPropertyChanged(nameof(IsOverlayConfirmation));
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 适配宿主的同步操作；这些命令始终可执行，无需订阅状态变化。
        /// </summary>
        private sealed class RelayCommand : ICommand
        {
            private readonly Action execute;

            public RelayCommand(Action execute)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public void Execute(object? parameter)
            {
                execute();
            }
        }
    }
}
