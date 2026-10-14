using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dictation.Presentation.Theme;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>检验复习卡片不被 ListBox 的系统默认前景色覆盖，主题切换后文字仍可读。</summary>
    public sealed class ReviewLibraryThemeTests
    {
        [Fact]
        public async Task CardText_FollowsLightAndDarkThemeIncludingSelection()
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    var page = new ReviewLibraryPage
                    {
                        Width = 1000,
                        Height = 800,
                        DataContext = new
                        {
                            Groups = new[] { new { HeadwordLabel = "食べる", MeaningLabel = "吃", StateLabel = "待测" } }
                        }
                    };
                    foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
                    {
                        ThemePalette.Apply(page.Resources, theme);
                        page.Measure(new Size(1000, 800));
                        page.Arrange(new Rect(0, 0, 1000, 800));
                        page.UpdateLayout();
                        var list = Assert.Single(Children<ListBox>(page));
                        var text = Assert.Single(Children<TextBlock>(list), value => value.Text == "食べる");
                        var expected = theme == ResolvedTheme.Light ? Color.FromRgb(31, 41, 55) : Color.FromRgb(248, 250, 252);
                        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
                        list.SelectedIndex = 0;
                        page.UpdateLayout();
                        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
                    }
                    completion.SetResult();
                }
                catch (Exception exception) { completion.SetException(exception); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }

        /// <summary>读取实际模板生成的控件，不检索 XAML 文本。</summary>
        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item) yield return item;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
        }
    }
}
