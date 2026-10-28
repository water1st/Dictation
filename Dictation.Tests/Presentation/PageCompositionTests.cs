using System.Windows;
using System.Windows.Controls;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证图文组合与跨页面布局边界。</summary>
    public sealed class PageCompositionTests
    {
        [Theory]
        [InlineData(true, 640)]
        [InlineData(false, 640)]
        [InlineData(true, 860)]
        [InlineData(false, 860)]
        public Task WorkspaceCards_MixedContentAndLanguage_HaveEqualBounds(bool isJapanese, double width) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = new LanguageWorkspacePage
            {
                Height = 556,
                DataContext = new
                {
                    IsJapaneseSelected = isJapanese,
                    LearningProgressLabel = "学习 0/20",
                    ReviewProgressLabel = "复习 0/20",
                    KanaProgressLabel = "五十音 0/46"
                }
            };
            var host = FormControlThemeTests.CreateHost(page);
            host.Width = width;
            host.Measure(new Size(width, 556));
            host.Arrange(new Rect(0, 0, width, 556));
            host.UpdateLayout();
            var review = (Button)page.FindName("StartReviewButton");
            var grid = Assert.IsType<Grid>(review.Parent);
            var cards = grid.Children.OfType<Button>().Where(item => item.Visibility == Visibility.Visible).ToArray();
            Assert.Equal(isJapanese ? 7 : 6, cards.Length);
            Assert.All(cards, card =>
            {
                Assert.True(card.ActualWidth > 0);
                Assert.True(card.ActualHeight > 0);
                Assert.InRange(Math.Abs(card.ActualWidth - review.ActualWidth), 0, 0.5);
                Assert.InRange(Math.Abs(card.ActualHeight - review.ActualHeight), 0, 0.5);
                var content = Assert.IsType<StackPanel>(card.Content);
                var bottom = content.TranslatePoint(new Point(0, content.ActualHeight), card).Y;
                Assert.True(bottom <= card.ActualHeight - card.Padding.Bottom + 0.5);
            });
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData("日语")]
        [InlineData("英语")]
        public Task LanguageEntry_FlagAndText_FormOneHorizontalGroup(string language) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = new HomePage();
            var host = FormControlThemeTests.CreateHost(page);
            host.Width = 860;
            host.Measure(new Size(860, 556));
            host.Arrange(new Rect(0, 0, 860, 556));
            host.UpdateLayout();
            var button = Assert.Single(FormControlThemeTests.Children<Button>(page), item => System.Windows.Automation.AutomationProperties.GetName(item) == language);
            var flag = Assert.Single(FormControlThemeTests.Children<LanguageBadge>(button));
            var title = Assert.Single(FormControlThemeTests.Children<TextBlock>(button), item => item.Text == language);
            var description = FormControlThemeTests.Children<TextBlock>(button).Single(item => item != title);
            var top = title.TranslatePoint(new Point(), button).Y;
            var bottom = description.TranslatePoint(new Point(0, description.ActualHeight), button).Y;
            var flagPosition = flag.TranslatePoint(new Point(), button);
            Assert.InRange(flag.ActualHeight / (bottom - top), 0.9, 1.1);
            Assert.True(flagPosition.X >= title.TranslatePoint(new Point(title.ActualWidth, 0), button).X + 12);
            Assert.InRange(Math.Abs(flagPosition.Y + flag.ActualHeight / 2 - (top + bottom) / 2), 0, 1);
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(typeof(LearningPlanPage))]
        [InlineData(typeof(ReviewPlanPage))]
        [InlineData(typeof(LexiconLookupPage))]
        [InlineData(typeof(TextImportPage))]
        [InlineData(typeof(ImportInboxListPage))]
        [InlineData(typeof(ImportInboxDetailPage))]
        [InlineData(typeof(ReviewLibraryPage))]
        [InlineData(typeof(WrongAnswerListPage))]
        [InlineData(typeof(PracticeSessionPage))]
        public Task Page_ContentFrame_UsesSharedAlignmentAndSpacing(Type pageType) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            var frame = Assert.IsType<Grid>(page.Content);
            Assert.Equal(new Thickness(32, 24, 32, 24), frame.Margin);
            host.Resources["PageContentMargin"] = new Thickness(36, 28, 36, 28);
            FormControlThemeTests.Layout(host);
            Assert.Equal(new Thickness(36, 28, 36, 28), frame.Margin);
            return Task.CompletedTask;
        });
    }
}
