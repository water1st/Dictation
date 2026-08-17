using Dictation.Presentation.ViewModels;
using System.Windows.Threading;

namespace Dictation.Presentation
{
    public sealed partial class HomeWindow : System.Windows.Window
    {
        private readonly HomeViewModel viewModel;

        public HomeWindow(HomeViewModel viewModel)
        {
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            DataContext = this.viewModel;
        }

        public HomeViewModel ViewModel => viewModel;

        public void RestoreAndActivate()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                {
                    return;
                }

                try
                {
                    Dispatcher.BeginInvoke(
                        DispatcherPriority.Normal,
                        new Action(RestoreAndActivate));
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            if (WindowState == System.Windows.WindowState.Minimized)
            {
                WindowState = System.Windows.WindowState.Normal;
            }

            if (!IsVisible)
            {
                Show();
            }

            var originalTopmost = Topmost;
            try
            {
                Topmost = true;
                Activate();
                Focus();
            }
            finally
            {
                Topmost = originalTopmost;
            }
        }
    }
}
