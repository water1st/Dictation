using System.Windows;
using System.Windows.Controls;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证页面正文、标题和普通操作共用一致的字号角色。</summary>
    public sealed class TypographyPageTests
    {
        [Theory]
        [InlineData(typeof(HomePage), "今天想练什么？", 30)]
        [InlineData(typeof(KanaPlanPage), "假名小练习", 30)]
        [InlineData(typeof(KanaSessionPage), "五十音听写", 30)]
        [InlineData(typeof(McpSettingsPage), "AI 导入设置", 30)]
        [InlineData(typeof(SettingsPage), "外观与主题", 24)]
        [InlineData(typeof(TextImportPage), "", 28)]
        [InlineData(typeof(LearningPlanPage), "", 28)]
        [InlineData(typeof(ReviewPlanPage), "", 28)]
        [InlineData(typeof(ReviewLibraryPage), "", 28)]
        [InlineData(typeof(ImportInboxListPage), "", 28)]
        [InlineData(typeof(ImportInboxDetailPage), "", 28)]
        [InlineData(typeof(LexiconLookupPage), "", 28)]
        [InlineData(typeof(WrongAnswerListPage), "", 28)]
        [InlineData(typeof(PracticeSessionPage), "", 28)]
        [InlineData(typeof(StatisticsPage), "", 28)]
        [InlineData(typeof(LanguageWorkspacePage), "", 23)]
        public Task Page_TextRoles_PreserveOriginalHeadingHierarchy(Type pageType, string title, double titleSize) => ReviewPlanPageTests.RunStaAsync(() =>
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
            Assert.Equal(titleSize, heading.FontSize);
            Assert.Equal(FontWeights.SemiBold, heading.FontWeight);
            foreach (var button in FormControlThemeTests.Children<Button>(page))
            {
                if (button.Content is string label)
                {
                    Assert.Equal(label == "🔊" ? 24d : 16d, button.FontSize);
                }
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(typeof(KanaPlanPage))]
        [InlineData(typeof(McpSettingsPage))]
        [InlineData(typeof(ReviewPlanPage))]
        public Task SharedControlSize_ChangesOnce_UpdatesPeerControlsWithoutFlatteningHeadings(Type pageType) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            host.Resources["ControlFontSize"] = 17d;
            FormControlThemeTests.Layout(host);
            Assert.Equal(14d, page.FontSize);
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
            Assert.Equal(20d, heading.FontSize);
            return Task.CompletedTask;
        });

        [Fact]
        public Task Navigation_TextLevels_KeepLanguageEntriesLargerThanFeatureEntries() => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var home = new HomePage();
            var workspace = new LanguageWorkspacePage();
            var host = FormControlThemeTests.CreateHost(home);
            host.Children.Add(workspace);
            FormControlThemeTests.Layout(host);
            var languageTitles = FormControlThemeTests.Children<TextBlock>(home)
                .Where(text => text.Text is "英语" or "日语").ToArray();
            Assert.Equal(2, languageTitles.Length);
            Assert.All(languageTitles, text =>
            {
                Assert.Equal(25d, text.FontSize);
                Assert.Equal(FontWeights.SemiBold, text.FontWeight);
            });
            var feature = Assert.Single(FormControlThemeTests.Children<TextBlock>(workspace), text => text.Text == "开始学习");
            Assert.Equal(18d, feature.FontSize);
            host.Resources["LanguageEntryTitleFontSize"] = 27d;
            FormControlThemeTests.Layout(host);
            Assert.All(languageTitles, text => Assert.Equal(27d, text.FontSize));
            Assert.Equal(18d, feature.FontSize);
            Assert.Equal(14d, home.FontSize);
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(typeof(KanaPlanPage))]
        [InlineData(typeof(LearningPlanPage))]
        [InlineData(typeof(ReviewPlanPage))]
        [InlineData(typeof(McpSettingsPage))]
        [InlineData(typeof(LexiconLookupPage))]
        [InlineData(typeof(ImportInboxListPage))]
        public Task Form_PeerControlTypes_UseOneReadableTextSize(Type pageType) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            var peers = FormControlThemeTests.Children<Control>(page)
                .Where(control => control is CheckBox or RadioButton or TextBox or ComboBox or Button)
                .Where(control => control.TemplatedParent is null).ToArray();
            Assert.NotEmpty(peers);
            Assert.All(peers, control => Assert.Equal(16d, control.FontSize));
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(typeof(KanaSessionPage))]
        [InlineData(typeof(PracticeSessionPage))]
        public Task Practice_AnswerInput_IsLargerThanOrdinaryFormControls(Type pageType) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            var answer = Assert.Single(FormControlThemeTests.Children<TextBox>(page), input =>
                input.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path == "AnswerText");
            Assert.Equal(28d, answer.FontSize);
            var button = FormControlThemeTests.Children<Button>(page).First(action => action.Content is string label && label != "🔊");
            Assert.Equal(16d, button.FontSize);
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(typeof(TextImportPage), "SenseTemplate", "Headword", 18)]
        [InlineData(typeof(LexiconLookupPage), "LookupResultTemplate", "Headword", 18)]
        [InlineData(typeof(ImportInboxDetailPage), "MemberTemplate", "Headword", 22)]
        public Task ItemTemplate_HeadingAndSupportingText_PreserveDistinctRoles(Type pageType, string templateKey, string bindingPath, double headingSize) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var row = (FrameworkElement)((DataTemplate)page.Resources[templateKey]).LoadContent();
            var host = FormControlThemeTests.CreateHost(row);
            FormControlThemeTests.Layout(host);
            var heading = Assert.Single(FormControlThemeTests.Children<TextBlock>(row), text =>
                text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == bindingPath);
            Assert.Equal(headingSize, heading.FontSize);
            Assert.Equal(FontWeights.SemiBold, heading.FontWeight);
            Assert.Contains(FormControlThemeTests.Children<TextBlock>(row), text => text.FontSize < heading.FontSize);
            return Task.CompletedTask;
        });

        [Fact]
        public Task BodyRole_ChangesOnce_UpdatesDescriptionsAndEmphasisWithoutChangingNavigation() => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = new LanguageWorkspacePage();
            var host = FormControlThemeTests.CreateHost(page);
            FormControlThemeTests.Layout(host);
            host.Resources["BodyFontSize"] = 15d;
            FormControlThemeTests.Layout(host);
            var description = Assert.Single(FormControlThemeTests.Children<TextBlock>(page), text => text.Text == "继续当前一轮，或开始新一轮");
            var progress = (TextBlock)page.FindName("LearningProgressText");
            Assert.Equal(15d, description.FontSize);
            Assert.Equal(15d, progress.FontSize);
            Assert.Equal(FontWeights.Normal, description.FontWeight);
            Assert.Equal(FontWeights.SemiBold, progress.FontWeight);
            Assert.Equal(18d, Assert.Single(FormControlThemeTests.Children<TextBlock>(page), text => text.Text == "开始学习").FontSize);
            return Task.CompletedTask;
        });
    }
}
