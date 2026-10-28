using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Dictation.Presentation.Theme;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>检查各页面局部样式不会使表单回落到系统模板。</summary>
    public sealed class FormControlPageTests
    {
        [Theory]
        [InlineData(typeof(KanaPlanPage))]
        [InlineData(typeof(McpSettingsPage))]
        [InlineData(typeof(ReviewPlanPage))]
        [InlineData(typeof(LearningPlanPage))]
        [InlineData(typeof(ImportInboxListPage))]
        [InlineData(typeof(ImportInboxDetailPage))]
        [InlineData(typeof(TextImportPage))]
        [InlineData(typeof(LexiconLookupPage))]
        [InlineData(typeof(WrongAnswerListPage))]
        [InlineData(typeof(KanaSessionPage))]
        [InlineData(typeof(PracticeSessionPage))]
        public Task Page_SharedResources_KeepActualFormTemplatesConsistent(Type pageType) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var page = (UserControl)Activator.CreateInstance(pageType)!;
            var host = FormControlThemeTests.CreateHost(page);
            if (page is ImportInboxDetailPage)
            {
                host.Children.Add((FrameworkElement)((DataTemplate)page.Resources["MemberTemplate"]).LoadContent());
            }
            if (page is LearningPlanPage)
            {
                var option = (CheckBox)((DataTemplate)page.Resources["OptionTemplate"]).LoadContent();
                host.Children.Add(option);
            }
            ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
            FormControlThemeTests.Layout(host);
            var controls = FormControlThemeTests.Children<Control>(host)
                .Where(control => control is CheckBox or RadioButton or TextBox or ComboBox).ToArray();
            Assert.NotEmpty(controls);
            foreach (var control in controls)
            {
                control.ApplyTemplate();
                if (control is ToggleButton)
                {
                    Assert.NotNull(control.Template.FindName("CheckedMark", control));
                    Assert.Equal(16d, control.FontSize);
                }
                else
                {
                    Assert.Contains(FormControlThemeTests.Children<Border>(control), border => border.CornerRadius.TopLeft > 0);
                }
            }
            return Task.CompletedTask;
        });
    }
}
