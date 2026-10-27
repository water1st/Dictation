using System.Windows;
using System.Windows.Controls;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证页面正文、标题和普通操作共用一致的字号角色。</summary>
    public sealed class TypographyPageTests
    {
        [Theory]
        [InlineData(typeof(HomePage), "今天想练什么？")]
        [InlineData(typeof(KanaPlanPage), "假名小练习")]
        [InlineData(typeof(KanaSessionPage), "五十音听写")]
        [InlineData(typeof(McpSettingsPage), "AI 导入设置")]
        [InlineData(typeof(SettingsPage), "外观与主题")]
        [InlineData(typeof(TextImportPage), "")]
        [InlineData(typeof(LearningPlanPage), "")]
        [InlineData(typeof(ReviewPlanPage), "")]
        [InlineData(typeof(ReviewLibraryPage), "")]
        [InlineData(typeof(ImportInboxListPage), "")]
        [InlineData(typeof(ImportInboxDetailPage), "")]
        [InlineData(typeof(LexiconLookupPage), "")]
        [InlineData(typeof(WrongAnswerListPage), "")]
        [InlineData(typeof(PracticeSessionPage), "")]
        [InlineData(typeof(StatisticsPage), "")]
        [InlineData(typeof(LanguageWorkspacePage), "")]
        public Task Page_TextRoles_UseSharedBodyTitleAndButtonSizes(Type pageType, string title) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            Assert.Equal(14d, page.FontSize);
            var heading = Assert.Single(FormControlThemeTests.Children<TextBlock>(page), text =>
                title.Length > 0
                    ? text.Text == title && text.TemplatedParent is not ContentPresenter
                    : text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path ==
                        (pageType == typeof(ImportInboxDetailPage) ? "SourceText" :
                            pageType == typeof(LanguageWorkspacePage) ? "CurrentLanguageLabel" : "WindowTitle"));
            Assert.Equal(28d, heading.FontSize);
            foreach (var button in FormControlThemeTests.Children<Button>(page))
            {
                if (button.Content is string label)
                {
                    Assert.Equal(label == "🔊" ? 24d : 14d, button.FontSize);
                }
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(typeof(KanaPlanPage))]
        [InlineData(typeof(McpSettingsPage))]
        [InlineData(typeof(ReviewPlanPage))]
        public Task SharedBodySize_ChangesOnce_UpdatesLocalPageAndControlStyles(Type pageType) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            host.Resources["BodyFontSize"] = 17d;
            FormControlThemeTests.Layout(host);
            Assert.Equal(17d, page.FontSize);
            var choices = FormControlThemeTests.Children<Control>(page)
                .Where(control => control is CheckBox or RadioButton).ToArray();
            Assert.NotEmpty(choices);
            Assert.All(choices, control => Assert.Equal(17d, control.FontSize));
            Assert.All(FormControlThemeTests.Children<Button>(page), button => Assert.Equal(17d, button.FontSize));
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(typeof(KanaPlanPage))]
        [InlineData(typeof(KanaSessionPage))]
        [InlineData(typeof(McpSettingsPage))]
        [InlineData(typeof(LearningPlanPage))]
        [InlineData(typeof(ReviewPlanPage))]
        [InlineData(typeof(ReviewLibraryPage))]
        [InlineData(typeof(PracticeSessionPage))]
        [InlineData(typeof(TextImportPage))]
        [InlineData(typeof(LexiconLookupPage))]
        [InlineData(typeof(WrongAnswerListPage))]
        [InlineData(typeof(StatisticsPage))]
        public Task Page_OrdinaryActions_ReuseSharedRoundedTemplate(Type pageType) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            var buttons = FormControlThemeTests.Children<Button>(page).ToArray();
            Assert.NotEmpty(buttons);
            foreach (var button in buttons)
            {
                button.ApplyTemplate();
                var border = FormControlThemeTests.Children<Border>(button).FirstOrDefault();
                Assert.NotNull(border);
                Assert.Equal(new CornerRadius(14), border.CornerRadius);
            }
            return Task.CompletedTask;
        });

        [Fact]
        public Task IconAction_FixedButtonSize_PreservesRoomForGlyph() => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var icon = new Button { Width = 40, Height = 40, Content = "🔊" };
            var host = FormControlThemeTests.CreateHost(icon);
            icon.Style = (Style)host.Resources["IconActionButtonStyle"];
            FormControlThemeTests.Layout(host);
            var presenter = Assert.Single(FormControlThemeTests.Children<ContentPresenter>(icon));
            Assert.True(presenter.ActualWidth >= 24, $"图标可用宽度仅 {presenter.ActualWidth} DIP。");
            Assert.True(presenter.ActualHeight >= 24, $"图标可用高度仅 {presenter.ActualHeight} DIP。");
            return Task.CompletedTask;
        });

        [Fact]
        public Task ReviewPlan_GroupTemplate_UsesSharedListHeadingSize() => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = new ReviewPlanPage();
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            var groups = Assert.Single(FormControlThemeTests.Children<ListBox>(page),
                list => list.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.ParentBinding.Path?.Path == "Groups");
            var row = (FrameworkElement)groups.ItemTemplate.LoadContent();
            host.Children.Add(row);
            FormControlThemeTests.Layout(host);
            var heading = Assert.Single(FormControlThemeTests.Children<TextBlock>(row),
                text => text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == "Details.HeadwordLabel");
            Assert.Equal(18d, heading.FontSize);
            return Task.CompletedTask;
        });
    }
}
