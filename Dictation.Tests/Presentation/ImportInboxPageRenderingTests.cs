using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
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
using Dictation.Services.Configuration;
using Dictation.Services.Importing;
using Dictation.Services.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    public sealed class ImportInboxPageRenderingTests
    {
        [Fact]
        public void ListPage_IsUserControlAndStartsWithSelectAllCheckbox()
        {
            RunOnSta(() =>
            {
                using var environment = new TestEnvironment();
                var viewModel = environment.CreateListViewModel();
                viewModel.Initialize(LearningLanguage.English);
                var batch = CreateBatch();
                viewModel.Batches.Add(batch);
                viewModel.SelectedBatch = batch;

                var page = new ImportInboxListPage { DataContext = viewModel };
                RenderControl(page);

                Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ImportInboxListPage)));
                var firstCheckBox = FindVisualChildren<CheckBox>(page).First();
                Assert.Equal("全选", firstCheckBox.Content as string);
                firstCheckBox.IsChecked = true;
                var toggleProvider = Assert.IsAssignableFrom<IToggleProvider>(
                    new CheckBoxAutomationPeer(firstCheckBox).GetPattern(PatternInterface.Toggle));

                toggleProvider.Toggle();

                Assert.False(firstCheckBox.IsChecked);
            });
        }

        [Fact]
        public void ListPage_RendersMeaningCardsAndEmptyChinesePlaceholder()
        {
            RunOnSta(() =>
            {
                using var environment = new TestEnvironment();
                var viewModel = environment.CreateListViewModel();
                viewModel.Initialize(LearningLanguage.English);
                var batch = CreateBatch();
                viewModel.Batches.Add(batch);
                viewModel.SelectedBatch = batch;

                var page = new ImportInboxListPage { DataContext = viewModel };
                RenderControl(page);
                var text = FindText(page);

                Assert.Contains(text, value => value.Contains("go", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("去", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("went", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("暂无中文释义", StringComparison.Ordinal));
                Assert.DoesNotContain(text, value => value.Contains("to go", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void ListPage_ExposesTheSelectedItemDetailCommand()
        {
            RunOnSta(() =>
            {
                using var environment = new TestEnvironment();
                var viewModel = environment.CreateListViewModel();
                viewModel.Initialize(LearningLanguage.English);
                var batch = CreateBatch();
                viewModel.Batches.Add(batch);
                viewModel.SelectedBatch = batch;

                var page = new ImportInboxListPage { DataContext = viewModel };
                RenderControl(page);
                var detailButton = Assert.Single(
                    FindVisualChildren<Button>(page),
                    button => string.Equals(button.Content as string, "查看详情", StringComparison.Ordinal));

                Assert.Same(viewModel.OpenSelectedDetailCommand, detailButton.Command);
            });
        }

        [Fact]
        public void DetailPage_RendersEveryMemberFormAndEditableChineseDraft()
        {
            RunOnSta(() =>
            {
                var batch = CreateBatch();
                var viewModel = new ImportInboxDetailViewModel();
                viewModel.Initialize(batch, 1);

                var page = new ImportInboxDetailPage { DataContext = viewModel };
                RenderControl(page);
                var text = FindText(page);
                var meaningEditors = FindVisualChildren<TextBox>(page).ToArray();

                Assert.True(typeof(UserControl).IsAssignableFrom(typeof(ImportInboxDetailPage)));
                Assert.Contains(text, value => value.Contains("go", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("不规则动词", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("另一个成员", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("过去式", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("went", StringComparison.Ordinal));
                Assert.Equal(2, meaningEditors.Length);
                Assert.Contains(meaningEditors, editor => editor.Text.Contains("去", StringComparison.Ordinal));
                Assert.Contains(meaningEditors, editor => string.IsNullOrEmpty(editor.Text));
                Assert.All(meaningEditors, editor => Assert.False(editor.IsReadOnly));
            });
        }

        private static ImportBatchDto CreateBatch()
        {
            return new ImportBatchDto(
                "batch-render",
                LearningLanguage.English,
                ImportSourceKind.Manual,
                ImportBatchStatus.Pending,
                DateTimeOffset.UtcNow,
                null,
                new[]
                {
                    new ImportBatchItemDto(
                        1,
                        "go",
                        "go%2:01:00::",
                        "group-render",
                        ImportItemDecision.Pending,
                        new ImportWordGroupDto(
                            "group-render",
                            LearningLanguage.English,
                            "go%2:01:00::",
                            ImportWordGroupPlacement.Inbox,
                            DateTimeOffset.UtcNow,
                            new[]
                            {
                                new ImportWordGroupMemberDto(
                                    "English",
                                    "go%2:01:00::",
                                    "go%v",
                                    "go",
                                    null,
                                    "动词",
                                    "不规则动词",
                                    false,
                                    new[] { "去" },
                                    new[]
                                    {
                                        new ImportWordGroupFormDto(
                                            "went",
                                            null,
                                            "Past",
                                            null,
                                            null,
                                            "过去式",
                                            "do 示例",
                                            true,
                                            false)
                                    }),
                                new ImportWordGroupMemberDto(
                                    "English",
                                    "go%2:01:00::",
                                    "go%v-2",
                                    "go",
                                    null,
                                    "动词",
                                    "另一个成员",
                                    false,
                                    Array.Empty<string>(),
                                    Array.Empty<ImportWordGroupFormDto>())
                            }))
                });
        }

        private sealed class TestEnvironment : IDisposable
        {
            private readonly TemporaryDirectory temporaryDirectory = new();

            public TestEnvironment()
            {
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                var factory = new LexiconSqliteConnectionFactory(paths);
                ImportInboxService = new ImportInboxService(
                    new LexiconLookupService(
                        new JapaneseLexiconDataAccessor(factory),
                        new EnglishLexiconDataAccessor(factory)),
                    new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
                Overlay = new OverlayService();
                Navigation = new NavigationService(new TestScopeFactory());
            }

            public ImportInboxService ImportInboxService { get; }

            public OverlayService Overlay { get; }

            public NavigationService Navigation { get; }

            public ImportInboxListViewModel CreateListViewModel()
            {
                return new ImportInboxListViewModel(ImportInboxService, Navigation, Overlay);
            }

            public void Dispose()
            {
                Navigation.Dispose();
                Overlay.Dispose();
                temporaryDirectory.Dispose();
            }
        }

        private static IReadOnlyList<string> FindText(DependencyObject root)
        {
            var values = new List<string>();
            Visit(root, values);
            return values;
        }

        private static void Visit(DependencyObject current, ICollection<string> values)
        {
            if (current is TextBlock textBlock)
            {
                if (!string.IsNullOrEmpty(textBlock.Text))
                {
                    values.Add(textBlock.Text);
                }

                var inlineText = string.Concat(
                    textBlock.Inlines.OfType<Run>().Select(run => run.Text));
                if (!string.IsNullOrEmpty(inlineText))
                {
                    values.Add(inlineText);
                }
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                Visit(VisualTreeHelper.GetChild(current, index), values);
            }
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
            Dispatcher.CurrentDispatcher.Invoke(
                DispatcherPriority.DataBind,
                new Action(() => { }));
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

        private sealed class TestScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
            {
                throw new NotSupportedException("页面渲染测试不执行页面创建导航。");
            }
        }
    }
}
