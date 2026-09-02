using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
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
    public sealed class LexiconLookupPageRenderingTests
    {
        [Fact]
        public void LexiconLookupPage_RendersChineseMeaningItemsAndOmitsEnglishDefinitionOnStaThread()
        {
            RunOnSta(() =>
            {
                using var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                var lexiconService = CreateLexiconService(paths);
                var importService = new ImportInboxService(
                    lexiconService,
                    new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
                var viewModel = new LexiconLookupViewModel(
                    lexiconService,
                    importService,
                    new NavigationService(new TestScopeFactory()),
                    new OverlayService());
                viewModel.Initialize(LearningLanguage.English);
                var result = new LexiconLookupResultDto(
                    LearningLanguage.English,
                    "render%2:30:00::",
                    "render%v",
                    "render",
                    null,
                    "动词",
                    "原形",
                    false,
                    new[] { "渲染", "描绘" },
                    new[]
                    {
                        new LexiconLookupFormDto(
                            "render",
                            null,
                            "原形",
                            "英语",
                            true,
                            true)
                    });
                viewModel.Results.Add(result);
                viewModel.SelectedResult = result;

                var page = new LexiconLookupPage { DataContext = viewModel };
                RenderControl(page);
                var text = FindText(page);

                Assert.Contains(text, value => value.Contains("渲染", StringComparison.Ordinal));
                Assert.Contains(text, value => value.Contains("描绘", StringComparison.Ordinal));
                Assert.DoesNotContain(text, value => value.Contains("to render", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void LexiconLookupPage_EmptyMeaningsShowChinesePlaceholderWithoutEnglishDefinitionOnStaThread()
        {
            RunOnSta(() =>
            {
                using var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                var lexiconService = CreateLexiconService(paths);
                var importService = new ImportInboxService(
                    lexiconService,
                    new ImportInboxDataAccessor(new SqliteConnectionFactory(paths)));
                var viewModel = new LexiconLookupViewModel(
                    lexiconService,
                    importService,
                    new NavigationService(new TestScopeFactory()),
                    new OverlayService());
                viewModel.Initialize(LearningLanguage.English);
                var result = new LexiconLookupResultDto(
                    LearningLanguage.English,
                    "missing%2:30:00::",
                    "missing%v",
                    "missing",
                    null,
                    "动词",
                    "无中文释义",
                    false,
                    Array.Empty<string>(),
                    Array.Empty<LexiconLookupFormDto>());
                viewModel.Results.Add(result);
                viewModel.SelectedResult = result;

                var page = new LexiconLookupPage { DataContext = viewModel };
                RenderControl(page);
                var text = FindText(page);

                Assert.Contains(text, value => value.Contains("暂无中文释义", StringComparison.Ordinal));
                Assert.DoesNotContain(text, value => value.Contains("to render", StringComparison.Ordinal));
            });
        }

        private static LexiconLookupService CreateLexiconService(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            return new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
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
                throw new NotSupportedException("该测试不执行页面创建导航。");
            }
        }
    }
}
