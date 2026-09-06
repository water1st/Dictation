using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    public sealed class LexiconLookupPageInteractionTests
    {
        [Fact]
        public void SearchAfterSelectingPreviousResultUsesNewBoundQuery()
        {
            RunOnSta(() =>
            {
                using var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                LexiconTestDatabases.CreateBoth(paths);
                var viewModel = CreateViewModel(paths);
                viewModel.Initialize(LearningLanguage.English);

                var page = new LexiconLookupPage { DataContext = viewModel };
                using var source = RenderControl(page);
                var queryBox = FindVisualChildren<ComboBox>(page).Single();
                var searchButton = FindVisualChildren<Button>(page)
                    .Single(button => string.Equals(button.Content as string, "搜索", StringComparison.Ordinal));

                queryBox.Text = "went";
                FlushBindings();
                Assert.Equal("went", viewModel.Query);
                ExecuteSearch(searchButton, viewModel, result => result.Count > 0);
                Assert.Equal("go", viewModel.Results[0].Headword);

                queryBox.Text = "take off";
                FlushBindings();
                Assert.Equal("take off", viewModel.Query);
                RaiseEnter(queryBox);
                WaitFor(() => viewModel.Results.Any(item => item.Headword == "take off"));

                var secondResult = Assert.Single(viewModel.Results);
                Assert.Equal("take off", secondResult.Headword);
                Assert.Equal("take off", viewModel.Query);
            });
        }

        private static LexiconLookupViewModel CreateViewModel(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            var lexiconService = new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
            var importService = new ImportInboxService(
                lexiconService,
                new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
            return new LexiconLookupViewModel(
                lexiconService,
                importService,
                new NavigationService(new TestScopeFactory()),
                new OverlayService());
        }

        private static void ExecuteSearch(
            Button searchButton,
            LexiconLookupViewModel viewModel,
            Func<IReadOnlyList<LexiconLookupResultDto>, bool> completed)
        {
            Assert.NotNull(searchButton.Command);
            searchButton.Command!.Execute(searchButton.CommandParameter);
            WaitFor(() => completed(viewModel.Results));
        }

        private static void RaiseEnter(ComboBox queryBox)
        {
            var keyEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(queryBox),
                Environment.TickCount,
                Key.Enter)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            queryBox.RaiseEvent(keyEvent);
        }

        private static HwndSource RenderControl(UserControl control)
        {
            var source = new HwndSource(new HwndSourceParameters("LexiconLookupPageInteractionTest")
            {
                Width = 1024,
                Height = 720
            });
            source.RootVisual = control;
            control.Width = 1024;
            control.Height = 720;
            control.ApplyTemplate();
            control.Measure(new Size(control.Width, control.Height));
            control.Arrange(new Rect(0, 0, control.Width, control.Height));
            control.UpdateLayout();
            FlushBindings();
            control.UpdateLayout();
            return source;
        }

        private static void FlushBindings()
        {
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.DataBind,
                new Action(() => { }));
        }

        private static void WaitFor(Func<bool> condition)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < timeout)
            {
                var frame = new DispatcherFrame();
                dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            Assert.True(condition(), "等待 WPF 搜索完成超时。");
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

        private sealed class TestScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
            {
                throw new NotSupportedException("该测试不执行页面创建导航。");
            }
        }
    }
}
