using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Services;
using Dictation.Services.Configuration;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    public sealed class LearningPlanNavigationAndRenderingTests
    {
        [Fact]
        public async Task HomeCommand_NavigatesToLearningPlanWithCurrentLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(temporaryDirectory.Path);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            await home.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);
            var reachedPage = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigation.TransitionRequested += (_, args) => reachedPage.TrySetResult(args.CurrentPage);

            home.OpenLearningPlanCommand.Execute(null);
            var currentPage = await reachedPage.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var plan = Assert.IsType<LearningPlanViewModel>(currentPage);
            Assert.Same(plan, navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, plan.SelectedLanguage);
            Assert.Equal("英语学习计划", navigation.PageTitle);
            Assert.False(navigation.CanGoBack);
            navigation.CompleteTransition();
            Assert.True(navigation.CanGoBack);
        }

        [Fact]
        public void LearningPlanPage_IsSingleWindowUserControlWithPlanAndLibraryActions()
        {
            RunOnSta(() =>
            {
                var page = new LearningPlanPage();
                RenderControl(page);

                Assert.True(typeof(UserControl).IsAssignableFrom(typeof(LearningPlanPage)));
                Assert.Contains(FindVisualChildren<CheckBox>(page), item => Equals(item.Content, "全选"));
                Assert.Contains(FindVisualChildren<Button>(page), item => Equals(item.Content, "保存计划"));
                Assert.Contains(FindVisualChildren<Button>(page), item => Equals(item.Content, "开始本轮"));
                Assert.DoesNotContain(FindVisualChildren<Button>(page), item =>
                    Equals(item.Content, "切换日语") || Equals(item.Content, "切换英语"));
            });
        }

        private static async Task<ServiceProvider> CreateProviderAsync(string root)
        {
            var paths = new ApplicationPaths(root);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)).MigrateAsync(
                TestContext.Current.CancellationToken);
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(root);
            services.AddDictationServices();
            services.AddDictationPresentation();
            return services.BuildServiceProvider();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
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

        private static void RenderControl(UserControl control)
        {
            control.Width = 1024;
            control.Height = 720;
            control.ApplyTemplate();
            control.Measure(new Size(control.Width, control.Height));
            control.Arrange(new Rect(0, 0, control.Width, control.Height));
            control.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            control.UpdateLayout();
        }

        private static void RunOnSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
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
    }
}
