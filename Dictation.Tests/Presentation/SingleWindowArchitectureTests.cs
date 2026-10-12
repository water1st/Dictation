using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Behaviors;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Core.Configuration;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证主窗口页面宿主和导航弹层的WPF结构合同。
    /// </summary>
    public sealed class SingleWindowArchitectureTests
    {
        [Fact]
        public void HomePage_IsAUserControl()
        {
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(HomePage)));
        }

        [Fact]
        public void PresentationUsesOnlyHomeWindowAndFeatureUserControls()
        {
            var windows = typeof(HomeWindow).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && typeof(Window).IsAssignableFrom(type))
                .ToArray();

            Assert.Equal(typeof(HomeWindow), Assert.Single(windows));
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(HomePage)));
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(LexiconLookupPage)));
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ImportInboxListPage)));
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ImportInboxDetailPage)));
            Assert.True(typeof(UserControl).IsAssignableFrom(typeof(LearningPlanPage)));
        }

        [Fact]
        public void PresentationAssemblyContainsNoLegacyWinFormsTypes()
        {
            var services = new ServiceCollection();

            services.AddDictationPresentation();

            Assert.DoesNotContain(
                services,
                descriptor => InheritsFrom(descriptor.ServiceType, "System.Windows.Forms.Form"));
            Assert.DoesNotContain(
                services,
                descriptor => descriptor.ServiceType.FullName == "Dictation.Presentation.WindowFactory");
            var types = typeof(HomeWindow).Assembly.GetTypes();
            Assert.DoesNotContain(types, type => InheritsFrom(type, "System.Windows.Forms.Form"));
            Assert.DoesNotContain(types, type => type.FullName == "Dictation.Presentation.WindowFactory");
        }

        private static bool InheritsFrom(Type type, string fullName)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [Fact]
        public void HomeWindow_ContainsNamedCurrentPagePresenter()
        {
            RunOnSta(() =>
            {
                using var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                using var provider = new ServiceCollection().BuildServiceProvider();
                using var overlay = new OverlayService();
                var navigation = new NavigationService(
                    provider.GetRequiredService<IServiceScopeFactory>());
                var homeViewModel = new HomeViewModel(
                    new SettingsService(new ConfigurationBuilder().Build(), paths),
                    navigation,
                    new StubLearningProgressReader(),
                    new StubLearningPlanReader(),
                    new StubLearningSessionReader(),
                    new StubReviewProgressReader(), new EmptyKanaProgressReader(), new EmptyDailyCheckInReader());
                navigation.InitializeHome(homeViewModel);
                var shell = new ShellViewModel(navigation, overlay, navigation);
                var homeWindow = new HomeWindow(shell);

                homeWindow.ApplyTemplate();
                var content = (UIElement)homeWindow.Content;
                content.Measure(new Size(homeWindow.Width, homeWindow.Height));
                content.Arrange(new Rect(0, 0, homeWindow.Width, homeWindow.Height));
                content.UpdateLayout();

                Assert.Contains(
                    FindVisualChildren<ContentControl>(homeWindow),
                    control => control.Name == "CurrentPagePresenter");
                Assert.Contains(
                    FindVisualChildren<ContentControl>(homeWindow),
                    control => control.Name == "PreviousPagePresenter");
                homeWindow.Close();
                navigation.Dispose();
            });
        }

        [Fact]
        public void HomeViewModel_HasNoWpfWindowOrUserControlDependency()
        {
            var members = typeof(HomeViewModel)
                .GetMembers(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Static)
                .Select(member => member switch
                {
                    System.Reflection.FieldInfo field => field.FieldType,
                    System.Reflection.PropertyInfo property => property.PropertyType,
                    System.Reflection.MethodInfo method => method.ReturnType,
                    System.Reflection.EventInfo eventInfo => eventInfo.EventHandlerType,
                    _ => null
                })
                .Where(type => type is not null)
                .Cast<Type>()
                .ToArray();

            Assert.DoesNotContain(members, type =>
                typeof(Window).IsAssignableFrom(type)
                || typeof(UserControl).IsAssignableFrom(type));
            Assert.DoesNotContain(typeof(Window),
                typeof(HomeViewModel).GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => parameter.ParameterType));
            Assert.DoesNotContain(typeof(UserControl),
                typeof(HomeViewModel).GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => parameter.ParameterType));
        }

        [Fact]
        public void ScrollViewerOffsetBehavior_TransfersOffsetInBothDirections()
        {
            RunOnSta(() =>
            {
                var state = new ScrollState();
                var viewer = new ScrollViewer
                {
                    Content = new Border { Height = 1000, Width = 100 }
                };
                viewer.SetBinding(
                    ScrollViewerOffsetBehavior.VerticalOffsetProperty,
                    new System.Windows.Data.Binding(nameof(ScrollState.Offset))
                    {
                        Source = state,
                        Mode = System.Windows.Data.BindingMode.TwoWay
                    });
                ScrollViewerOffsetBehavior.SetIsAttached(viewer, true);
                var window = new Window
                {
                    Width = 100,
                    Height = 100,
                    Content = viewer,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                try
                {
                    window.Show();
                    viewer.UpdateLayout();

                    viewer.ScrollToVerticalOffset(120);
                    viewer.UpdateLayout();
                    FlushDispatcher();
                    Assert.Equal(120, state.Offset, precision: 3);

                    state.Offset = 40;
                    viewer.UpdateLayout();
                    FlushDispatcher();
                    Assert.Equal(40, viewer.VerticalOffset, precision: 3);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void OverlayRendering_ExposesOnlyActionsForCurrentRequest()
        {
            RunOnSta(() =>
            {
                using var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                using var provider = new ServiceCollection().BuildServiceProvider();
                using var overlay = new OverlayService();
                using var navigation = new NavigationService(
                    provider.GetRequiredService<IServiceScopeFactory>());
                var homeViewModel = new HomeViewModel(
                    new SettingsService(new ConfigurationBuilder().Build(), paths),
                    navigation,
                    new StubLearningProgressReader(),
                    new StubLearningPlanReader(),
                    new StubLearningSessionReader(),
                    new StubReviewProgressReader(), new EmptyKanaProgressReader(), new EmptyDailyCheckInReader());
                navigation.InitializeHome(homeViewModel);
                var shell = new ShellViewModel(navigation, overlay, navigation);
                var homeWindow = new HomeWindow(shell);

                try
                {
                    homeWindow.Show();
                    homeWindow.ApplyTemplate();
                    homeWindow.UpdateLayout();

                    var messageCompletion = overlay.ShowMessageAsync("提示", "已完成");
                    FlushDispatcher();
                    homeWindow.UpdateLayout();
                    var messageDismissButton = FindButton(homeWindow, "知道了");
                    Assert.Equal(Visibility.Visible, messageDismissButton.Visibility);
                    Assert.True(messageDismissButton.IsEnabled);
                    Assert.Same(shell.DismissOverlayCommand, messageDismissButton.Command);
                    Assert.True(messageDismissButton.Command.CanExecute(null));
                    messageDismissButton.Command.Execute(null);
                    Assert.False(overlay.IsVisible);
                    messageCompletion.GetAwaiter().GetResult();

                    var confirmationCompletion = overlay.ConfirmAsync("确认", "继续吗？");
                    FlushDispatcher();
                    homeWindow.UpdateLayout();
                    Assert.Equal(Visibility.Visible, FindButton(homeWindow, "取消").Visibility);
                    var acceptButton = FindButton(homeWindow, "确定");
                    Assert.Equal(Visibility.Visible, acceptButton.Visibility);
                    Assert.Equal(Visibility.Collapsed, FindButton(homeWindow, "知道了").Visibility);
                    Assert.Same(shell.AcceptOverlayCommand, acceptButton.Command);
                    Assert.True(acceptButton.Command.CanExecute(null));
                    acceptButton.Command.Execute(null);
                    Assert.False(overlay.IsVisible);
                    Assert.True(confirmationCompletion.GetAwaiter().GetResult());

                    var busyCompletion = overlay.ShowBusyAsync("加载", "正在加载");
                    FlushDispatcher();
                    homeWindow.UpdateLayout();
                    Assert.Equal(Visibility.Collapsed, FindButton(homeWindow, "取消").Visibility);
                    Assert.Equal(Visibility.Collapsed, FindButton(homeWindow, "确定").Visibility);
                    Assert.Equal(Visibility.Collapsed, FindButton(homeWindow, "知道了").Visibility);
                    overlay.Dismiss();
                    busyCompletion.GetAwaiter().GetResult();
                }
                finally
                {
                    homeWindow.Close();
                }
            });
        }

        private static Button FindButton(HomeWindow homeWindow, string content)
        {
            return Assert.Single(
                FindVisualChildren<Button>(homeWindow),
                button => Equals(button.Content, content));
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root is Window window && window.Content is DependencyObject content)
            {
                root = content;
            }

            if (root is T match)
            {
                yield return match;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in FindVisualChildren<T>(VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }

        private static void FlushDispatcher()
        {
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.DataBind,
                new Action(() => { }));
        }

        private static void RunOnSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    action();
                }
                catch (Exception caught)
                {
                    exception = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception is not null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private sealed class ScrollState : INotifyPropertyChanged
        {
            private double offset;

            public event PropertyChangedEventHandler? PropertyChanged;

            public double Offset
            {
                get => offset;
                set
                {
                    if (Math.Abs(offset - value) < 0.001)
                    {
                        return;
                    }

                    offset = value;
                    PropertyChanged?.Invoke(
                        this,
                        new PropertyChangedEventArgs(nameof(Offset)));
                }
            }
        }
    }
}
