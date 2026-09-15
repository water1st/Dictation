using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;

namespace Dictation.Presentation
{
    public sealed partial class HomeWindow : System.Windows.Window
    {
        private static readonly System.Windows.Duration TransitionDuration =
            new(TimeSpan.FromMilliseconds(200));
        private readonly ShellViewModel shellViewModel;
        private bool isTransitioning;

        public HomeWindow(ShellViewModel shellViewModel)
        {
            this.shellViewModel = shellViewModel
                ?? throw new ArgumentNullException(nameof(shellViewModel));
            InitializeComponent();
            DataContext = this.shellViewModel;
            this.shellViewModel.TransitionRequested += OnTransitionRequested;
        }

        public ShellViewModel ViewModel => shellViewModel;

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

        private void OnTransitionRequested(
            object? sender,
            NavigationTransitionEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(() => OnTransitionRequested(sender, e)));
                return;
            }

            PreviousPagePresenter.Content = e.PreviousPage;
            CurrentPagePresenter.Content = e.CurrentPage;
            isTransitioning = true;

            var width = ActualWidth > 0 ? ActualWidth : Width;
            var oldTransform = new TranslateTransform();
            var newTransform = new TranslateTransform();
            PreviousPagePresenter.RenderTransform = oldTransform;
            CurrentPagePresenter.RenderTransform = newTransform;
            PreviousPagePresenter.Opacity = 1;
            CurrentPagePresenter.Opacity = 0;

            var oldOffset = e.Direction == NavigationDirection.Forward ? -width : width;
            var newOffset = e.Direction == NavigationDirection.Forward ? width : -width;
            var oldSlide = new DoubleAnimation(0, oldOffset, TransitionDuration);
            var newSlide = new DoubleAnimation(newOffset, 0, TransitionDuration);
            var oldFade = new DoubleAnimation(1, 0, TransitionDuration);
            var newFade = new DoubleAnimation(0, 1, TransitionDuration);
            newFade.Completed += OnTransitionCompleted;

            oldTransform.BeginAnimation(TranslateTransform.XProperty, oldSlide);
            newTransform.BeginAnimation(TranslateTransform.XProperty, newSlide);
            PreviousPagePresenter.BeginAnimation(UIElement.OpacityProperty, oldFade);
            CurrentPagePresenter.BeginAnimation(UIElement.OpacityProperty, newFade);
        }

        private void OnTransitionCompleted(object? sender, EventArgs e)
        {
            if (!isTransitioning)
            {
                return;
            }

            isTransitioning = false;
            PreviousPagePresenter.Content = null;
            PreviousPagePresenter.RenderTransform = null;
            CurrentPagePresenter.RenderTransform = null;
            PreviousPagePresenter.BeginAnimation(UIElement.OpacityProperty, null);
            CurrentPagePresenter.BeginAnimation(UIElement.OpacityProperty, null);
            PreviousPagePresenter.Opacity = 0;
            CurrentPagePresenter.Opacity = 1;
            shellViewModel.CompleteTransition();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            // 已关闭窗口不能继续持有导航转场订阅；其余 Shell 订阅与根容器单例同寿命。
            shellViewModel.TransitionRequested -= OnTransitionRequested;
        }
    }
}
