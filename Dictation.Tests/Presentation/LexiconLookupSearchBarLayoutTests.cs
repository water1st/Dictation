using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dictation.Core.Configuration;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>使用完整应用资源验证查词搜索行的尺寸、字号和可用宽度。</summary>
    [Collection("DpiRendering")]
    public sealed class LexiconLookupSearchBarLayoutTests
    {
        public static IEnumerable<object[]> RenderingCases()
        {
            foreach (var language in new[] { LearningLanguage.Japanese, LearningLanguage.English })
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            foreach (var scale in new[] { 1d, 1.5d, 2d })
            foreach (var width in new[] { 620d, 1024d })
            {
                yield return new object[] { language, theme, scale, width };
            }
        }

        [Theory]
        [MemberData(nameof(RenderingCases))]
        public Task SearchBar_FullResourcesUsesAvailableWidthAndMatchesControlSizes(
            LearningLanguage language, ResolvedTheme theme, double scale, double width)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                var navigation = provider.GetRequiredService<NavigationService>();
                var landing = provider.GetRequiredService<LandingViewModel>();
                navigation.InitializeHome(landing);
                await landing.SelectLanguageAsync(language, TestContext.Current.CancellationToken);
                navigation.CompleteTransition();
                await navigation.OpenLexiconLookupAsync(language, TestContext.Current.CancellationToken);
                navigation.CompleteTransition();
                var viewModel = Assert.IsType<LexiconLookupViewModel>(navigation.CurrentPage);
                var page = new LexiconLookupPage { DataContext = viewModel };
                var window = provider.GetRequiredService<HomeWindow>();
                var resourceHost = ScrollBarThemeTests.CreateHost(new Border());
                foreach (var dictionary in resourceHost.Resources.MergedDictionaries)
                {
                    window.Resources.MergedDictionaries.Add(dictionary);
                }
                window.Width = width;
                window.Height = 760;
                window.ShowActivated = false;
                ThemePalette.Apply(window.Resources, theme);
                ((ContentControl)window.FindName("CurrentPagePresenter")).Content = page;
                try
                {
                    window.Show();
                    VisualTreeHelper.SetRootDpi(window, new DpiScale(scale, scale));
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();
                    var query = Descendants<ComboBox>(page).Single();
                    query.Text = language == LearningLanguage.Japanese ? "ねこ" : "went";
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();
                    Export(window, $"{language}-{theme}-{scale:0.0}-w{width:0}", scale);
                    var search = Descendants<Button>(page).Single(button => Equals(button.Content, "搜索"));
                    var label = Descendants<TextBlock>(page).Single(text => text.Text == "查询");
                    var buttonText = Descendants<TextBlock>(search).Single(text => text.Text == "搜索");
                    var editor = Assert.IsType<TextBox>(query.Template.FindName("PART_EditableTextBox", query));
                    var queryBounds = query.TransformToAncestor(page).TransformBounds(new Rect(query.RenderSize));
                    var buttonBounds = search.TransformToAncestor(page).TransformBounds(new Rect(search.RenderSize));
                    var searchRow = Assert.IsType<Grid>(search.Parent);

                    Assert.InRange(Math.Abs(queryBounds.Height - buttonBounds.Height), 0, 1);
                    Assert.InRange(Math.Abs(queryBounds.Top - buttonBounds.Top), 0, 1);
                    Assert.Equal(buttonText.FontSize, label.FontSize);
                    Assert.Equal(buttonText.FontSize, editor.FontSize);
                    Assert.InRange(buttonBounds.Left - queryBounds.Right, 8, 20);
                    Assert.InRange(searchRow.ActualWidth - search.TranslatePoint(new Point(search.ActualWidth, 0), searchRow).X, 0, 1);
                    var card = Ancestor<Border>(searchRow);
                    Assert.InRange(card.ActualWidth - card.Padding.Left - card.Padding.Right
                        - card.BorderThickness.Left - card.BorderThickness.Right - searchRow.ActualWidth, 0, 1);
                    var rowBounds = searchRow.TransformToAncestor(card).TransformBounds(new Rect(searchRow.RenderSize));
                    Assert.InRange(Math.Abs(rowBounds.Top - (card.ActualHeight - rowBounds.Bottom)), 0, 1);
                    Assert.True(query.ActualWidth > search.ActualWidth);
                    Assert.InRange(editor.ActualHeight, editor.FontSize, query.ActualHeight);
                    Assert.True(search.IsEnabled);
                    Assert.Equal(query.Text, viewModel.Query);

                    viewModel.Query = language == LearningLanguage.Japanese ? "食べます" : "went";
                    await viewModel.SearchAsync(TestContext.Current.CancellationToken);
                    Assert.NotEmpty(viewModel.Results);
                    viewModel.IsResultDropDownOpen = true;
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    var popup = Assert.IsType<Popup>(query.Template.FindName("PART_Popup", query));
                    Assert.True(popup.IsOpen);
                    var popupContent = Assert.IsAssignableFrom<FrameworkElement>(popup.Child);
                    Assert.True(popupContent.ActualWidth > 100);
                    var stage = Descendants<Button>(page).Single(button => Equals(button.Content, "加入导入收件箱"));
                    Assert.True(popupContent.PointToScreen(new Point(popupContent.ActualWidth, 0)).X
                        <= stage.PointToScreen(new Point()).X);
                    viewModel.IsResultDropDownOpen = false;
                }
                finally
                {
                    window.Close();
                }
            });
        }

        private static T Ancestor<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent is not T)
            {
                Assert.NotNull(parent);
                parent = VisualTreeHelper.GetParent(parent);
            }
            return (T)parent;
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item)
            {
                yield return item;
            }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }

        private static void Export(HomeWindow window, string name, double scale)
        {
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }
            Directory.CreateDirectory(output);
            var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)(content.ActualWidth * scale), (int)(content.ActualHeight * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, $"lookup-search-{name}.png"));
            encoder.Save(stream);
        }
    }
}
