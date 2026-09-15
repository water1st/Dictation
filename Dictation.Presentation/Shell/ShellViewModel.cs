using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation.Shell
{
    public sealed class ShellViewModel : INotifyPropertyChanged
    {
        private readonly INavigationHost navigationHost;
        private readonly IOverlayHost overlayHost;

        public ShellViewModel(
            INavigationHost navigationHost,
            IOverlayHost overlayHost)
        {
            this.navigationHost = navigationHost
                ?? throw new ArgumentNullException(nameof(navigationHost));
            this.overlayHost = overlayHost
                ?? throw new ArgumentNullException(nameof(overlayHost));
            GoBackCommand = new AsyncCommand(async () =>
            {
                await this.navigationHost.GoBackAsync();
            });
            AcceptOverlayCommand = new RelayCommand(this.overlayHost.Accept);
            CancelOverlayCommand = new RelayCommand(this.overlayHost.Cancel);
            DismissOverlayCommand = new RelayCommand(this.overlayHost.Dismiss);
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

        public OverlayRequest? CurrentOverlay => overlayHost.CurrentRequest;

        public bool IsOverlayVisible => overlayHost.IsVisible;

        public bool IsOverlayBusy => overlayHost.IsBusy;

        public bool IsOverlayConfirmation => overlayHost.IsConfirmation;

        public ICommand GoBackCommand { get; }

        public ICommand AcceptOverlayCommand { get; }

        public ICommand CancelOverlayCommand { get; }

        public ICommand DismissOverlayCommand { get; }

        public void CompleteTransition()
        {
            navigationHost.CompleteTransition();
        }

        private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
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

            public event EventHandler? CanExecuteChanged;

            public void Execute(object? parameter)
            {
                execute();
            }
        }
    }
}
