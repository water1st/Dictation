using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation.Shell
{
    public sealed class ShellViewModel : INotifyPropertyChanged
    {
        private readonly NavigationService navigationService;
        private readonly OverlayService overlayService;

        public ShellViewModel(
            NavigationService navigationService,
            OverlayService overlayService)
        {
            this.navigationService = navigationService
                ?? throw new ArgumentNullException(nameof(navigationService));
            this.overlayService = overlayService
                ?? throw new ArgumentNullException(nameof(overlayService));
            GoBackCommand = new AsyncCommand(async () =>
            {
                await this.navigationService.GoBackAsync();
            });
            AcceptOverlayCommand = new RelayCommand(this.overlayService.Accept);
            CancelOverlayCommand = new RelayCommand(this.overlayService.Cancel);
            DismissOverlayCommand = new RelayCommand(this.overlayService.Dismiss);
            this.navigationService.PropertyChanged += OnNavigationPropertyChanged;
            this.overlayService.PropertyChanged += OnOverlayPropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<NavigationTransitionEventArgs>? TransitionRequested
        {
            add => navigationService.TransitionRequested += value;
            remove => navigationService.TransitionRequested -= value;
        }

        public NavigationService NavigationService => navigationService;

        public OverlayService OverlayService => overlayService;

        public object? CurrentPage => navigationService.CurrentPage;

        public string PageTitle => navigationService.PageTitle;

        public bool CanGoBack => navigationService.CanGoBack;

        public ICommand GoBackCommand { get; }

        public ICommand AcceptOverlayCommand { get; }

        public ICommand CancelOverlayCommand { get; }

        public ICommand DismissOverlayCommand { get; }

        public void CompleteTransition()
        {
            navigationService.CompleteTransition();
        }

        private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
            if (e.PropertyName is nameof(NavigationService.CurrentPage)
                or nameof(NavigationService.PageTitle)
                or nameof(NavigationService.CanGoBack))
            {
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(CanGoBack));
            }
        }

        private void OnOverlayPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged($"OverlayService.{e.PropertyName}");
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
