using System.ComponentModel;
using System.Windows.Threading;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;

namespace Dictation.Presentation
{
    /// <summary>
    /// 将首页主题选择与 WPF 应用资源连接，并在退出时解除事件订阅。
    /// </summary>
    public sealed partial class App : System.Windows.Application
    {
        private readonly ThemeManager themeManager;
        private readonly HomeViewModel viewModel;

        /// <summary>
        /// 将首页主题选择与 WPF 应用资源连接，并在退出时解除事件订阅。
        /// </summary>
        /// <param name="themeManager">解析并通知应用主题的管理器。</param>
        /// <param name="viewModel">提供首页主题选择状态的 ViewModel。</param>
        public App(ThemeManager themeManager, HomeViewModel viewModel)
        {
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
            this.themeManager.ThemeChanged += OnThemeChanged;
            Exit += OnExit;
            this.themeManager.SetThemeMode(this.viewModel.SelectedThemeMode);
            ApplyTheme(this.themeManager.CurrentTheme);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HomeViewModel.SelectedThemeMode))
            {
                themeManager.SetThemeMode(viewModel.SelectedThemeMode);
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyTheme(themeManager.CurrentTheme);
                return;
            }

            Dispatcher.BeginInvoke(
                DispatcherPriority.DataBind,
                new Action(() => ApplyTheme(themeManager.CurrentTheme)));
        }

        private void ApplyTheme(ResolvedTheme theme)
        {
            ThemePalette.Apply(Resources, theme);
        }

        private void OnExit(object? sender, System.Windows.ExitEventArgs e)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            themeManager.ThemeChanged -= OnThemeChanged;
            Exit -= OnExit;
        }
    }
}
