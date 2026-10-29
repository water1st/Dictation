using System.Windows;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Core.Updating;

namespace Dictation.Presentation
{
    /// <summary>
    /// 承载单窗口页面、导航动画和弹层的 WPF 主窗口。
    /// </summary>
    public sealed partial class HomeWindow : System.Windows.Window
    {
        private static readonly System.Windows.Duration TransitionDuration =
            new(TimeSpan.FromMilliseconds(200));
        private readonly ShellViewModel shellViewModel;
        private bool isTransitioning;
        private readonly IUpdateStartupNotifier startupNotifier;

        /// <summary>
        /// 承载单窗口页面、导航动画和弹层的 WPF 主窗口。
        /// </summary>
        /// <param name="shellViewModel">提供页面、转场和弹层状态的窗口宿主模型。</param>
        /// <param name="update">独立于语言页面的更新入口状态。</param>
        /// <param name="startupNotifier">迁移和Host成功后确认窗口Loaded的能力。</param>
        public HomeWindow(ShellViewModel shellViewModel, UpdateViewModel update, IUpdateStartupNotifier startupNotifier)
        {
            this.shellViewModel = shellViewModel
                ?? throw new ArgumentNullException(nameof(shellViewModel));
            Update = update ?? throw new ArgumentNullException(nameof(update));
            this.startupNotifier = startupNotifier ?? throw new ArgumentNullException(nameof(startupNotifier));
            InitializeComponent();
            DataContext = this.shellViewModel;
            this.shellViewModel.TransitionRequested += OnTransitionRequested;
            Loaded += OnLoaded;
            Update.ShutdownRequested += OnUpdateShutdownRequested;
        }

        public ShellViewModel ViewModel => shellViewModel;

        /// <summary>工具栏的独立更新组件，不让导航和语言页面互相依赖。</summary>
        public UpdateViewModel Update { get; }

        /// <summary>将页面、转场与弹层裁切到描边内侧，避免背景覆盖圆角和边缘。</summary>
        private void OnFrameContentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var content = (FrameworkElement)sender;
            content.Clip = new RectangleGeometry(new Rect(e.NewSize), 11, 11);
        }

        /// <summary>为本次错误创建独立参数列表，不通过命令行拼接路径。</summary>
        internal static ProcessStartInfo CreateErrorLogStartInfo(string path)
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe"))
            {
                UseShellExecute = false
            };
            info.ArgumentList.Add(path);
            return info;
        }

        private void OnUpdateErrorClick(object sender, RoutedEventArgs e)
        {
            if (!Update.CanOpenErrorLog || Update.CurrentErrorLogPath is not { } path) return;
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("对应日志文件不存在。", path);
                using var process = Process.Start(CreateErrorLogStartInfo(path));
                if (process is null) throw new InvalidOperationException("记事本未能启动。");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
            {
                Update.ReportLogOpenFailure(exception);
            }
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 将主窗口恢复到可见状态并请求前台激活。
        /// </summary>
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
            CurrentPagePresenter.IsHitTestVisible = false;

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
            CurrentPagePresenter.IsHitTestVisible = true;
            PreviousPagePresenter.Content = null;
            PreviousPagePresenter.RenderTransform = null;
            CurrentPagePresenter.RenderTransform = null;
            PreviousPagePresenter.BeginAnimation(UIElement.OpacityProperty, null);
            CurrentPagePresenter.BeginAnimation(UIElement.OpacityProperty, null);
            PreviousPagePresenter.Opacity = 0;
            CurrentPagePresenter.Opacity = 1;
            shellViewModel.CompleteTransition();
        }

        /// <summary>窗口Loaded确认迁移及Host成功，随后异步检查，不弹框或自动下载。</summary>
        /// <param name="sender">已加载窗口。</param>
        /// <param name="e">加载事件。</param>
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            startupNotifier.NotifyReady();
            await Update.CheckAsync();
        }

        /// <summary>只有独占runner启动成功才按正常窗口关闭流程退出。</summary>
        /// <param name="sender">更新组件。</param>
        /// <param name="e">关闭请求。</param>
        private void OnUpdateShutdownRequested(object? sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess()) Close();
            else Dispatcher.BeginInvoke(new Action(Close));
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            // 已关闭窗口不能继续持有导航转场订阅；其余 Shell 订阅与根容器单例同寿命。
            shellViewModel.TransitionRequested -= OnTransitionRequested;
            Loaded -= OnLoaded;
            Update.ShutdownRequested -= OnUpdateShutdownRequested;
            Update.Dispose();
        }
    }
}
