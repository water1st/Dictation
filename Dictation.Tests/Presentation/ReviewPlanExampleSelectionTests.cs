using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dictation.Core.Configuration;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>检验英语例词候选的中文义项和选择绑定在深色模式中仍然可用。</summary>
    public sealed class ReviewPlanExampleSelectionTests
    {
        [Fact]
        public Task Candidates_ShowChineseMeaningsAndSelectInThemedList() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            vm.Initialize(LearningLanguage.English);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            vm.ExampleQuery = "go";
            await vm.SearchExamplesAsync(TestContext.Current.CancellationToken);
            var page = new ReviewPlanPage { DataContext = vm, Width = 900, Height = 1500 };
            var host = FormControlThemeTests.CreateHost(page);
            host.Width = 900;
            ThemePalette.Apply(page.Resources, ResolvedTheme.Dark);
            host.Measure(new Size(900, 1500));
            host.Arrange(new Rect(0, 0, 900, 1500));
            host.UpdateLayout();
            var candidates = Assert.Single(Children<ListBox>(page), list => ReferenceEquals(list.ItemsSource, vm.ExampleCandidates));
            Assert.NotEmpty(candidates.Items);
            candidates.SetCurrentValue(ListBox.SelectedItemProperty, candidates.Items[0]);
            Assert.Same(candidates.Items[0], vm.SelectedExample);
            foreach (var meaning in vm.SelectedExample!.Meanings)
            {
                var label = Assert.Single(Children<TextBlock>(candidates), text => text.Text == meaning);
                Assert.Equal(Assert.IsType<SolidColorBrush>(page.Resources["SecondaryForegroundBrush"]).Color,
                    Assert.IsType<SolidColorBrush>(label.Foreground).Color);
            }
            Assert.Equal(Assert.IsType<SolidColorBrush>(page.Resources["ForegroundBrush"]).Color,
                Assert.IsType<SolidColorBrush>(candidates.Foreground).Color);
        });

        /// <summary>枚举模板展开后的视觉节点。</summary>
        /// <param name="root">当前视觉节点。</param>
        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item)
            {
                yield return item;
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }
    }
}
