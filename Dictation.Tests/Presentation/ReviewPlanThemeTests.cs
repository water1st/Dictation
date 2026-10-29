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
    /// <summary>检验复习计划选项的实际控件文字跟随浅深主题，而非只检验页面标题。</summary>
    public sealed class ReviewPlanThemeTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public Task Options_KeepReadableForegroundWhenThemeChanges(LearningLanguage language) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            vm.Initialize(language);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            var page = new ReviewPlanPage { DataContext = vm, Width = 900, Height = 1200 };
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(page.Resources, theme);
                page.Measure(new Size(900, 1200));
                page.Arrange(new Rect(0, 0, 900, 1200));
                page.UpdateLayout();
                var expected = Assert.IsType<SolidColorBrush>(page.Resources["ForegroundBrush"]).Color;
                var choices = Children<Control>(page).Where(control => control is CheckBox or RadioButton).ToArray();
                Assert.NotEmpty(choices);
                Assert.All(choices, control => Assert.Equal(expected, Assert.IsType<SolidColorBrush>(control.Foreground).Color));
                foreach (var choice in choices)
                {
                    var texts = FormControlThemeTests.Children<TextBlock>(choice).Where(text => !string.IsNullOrEmpty(text.Text)).ToArray();
                    Assert.NotEmpty(texts);
                    Assert.All(texts, text => Assert.Equal(expected, Assert.IsType<SolidColorBrush>(text.Foreground).Color));
                }
            }
        });

        /// <summary>枚举真实视觉树中的控件及其模板子元素。</summary>
        /// <param name="root">正在检查的控件根节点。</param>
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
