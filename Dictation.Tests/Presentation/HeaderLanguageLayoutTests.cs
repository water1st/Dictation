using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Presentation.Controls;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证页头语言标签在标题换行及文字放大时仍紧凑居中。</summary>
    public sealed class HeaderLanguageLayoutTests
    {
        [Theory]
        [InlineData(LearningLanguage.Japanese)]
        [InlineData(LearningLanguage.English)]
        public Task Header_RealViewModels_KeepLanguageSelectionAndLayoutAcrossRenderDensities(LearningLanguage language)
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
                var token = TestContext.Current.CancellationToken;
                var headword = language == LearningLanguage.Japanese ? "食べる" : "go";
                var sense = language == LearningLanguage.Japanese ? "jmdict:1:1" : "go%2:01:00::";
                var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                    language, ImportSourceKind.Manual, new[] { new ImportSelectionDto(headword, sense) }), token);
                using var scope = provider.CreateScope();
                var services = scope.ServiceProvider;
                var inbox = services.GetRequiredService<ImportInboxListViewModel>();
                inbox.Initialize(language);
                await inbox.RefreshAsync(token);
                var detail = services.GetRequiredService<ImportInboxDetailViewModel>();
                detail.Initialize(staged.Batch, Assert.Single(staged.Batch.Items).ItemId);
                var learning = services.GetRequiredService<LearningPlanViewModel>();
                learning.Initialize(language);
                await learning.RefreshAsync(token);
                foreach (var page in new UserControl[]
                {
                    new ImportInboxListPage { DataContext = inbox },
                    new ImportInboxDetailPage { DataContext = detail },
                    new LearningPlanPage { DataContext = learning }
                })
                {
                    var shell = new PageScrollHost { Content = page };
                    var host = ScrollBarThemeTests.CreateHost(shell);
                    host.Width = 858;
                    host.Height = 560;
                    foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
                    {
                        ThemePalette.Apply(host.Resources, theme);
                        ScrollBarThemeTests.Layout(host);
                        var label = Assert.Single(FormControlThemeTests.Children<TextBlock>(page), text =>
                            text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == "LanguageLabel");
                        Assert.Equal(language == LearningLanguage.Japanese ? "日语" : "英语", label.Text);
                        var badge = Assert.IsType<Border>(VisualTreeHelper.GetParent(label));
                        var textTop = label.TransformToAncestor(badge).Transform(new Point());
                        Assert.InRange(Math.Abs(textTop.Y + label.ActualHeight / 2 - badge.ActualHeight / 2), 0, 0.5);
                        Assert.InRange(badge.ActualHeight, 39.5, 40.5);
                        Assert.Equal(host.Resources["AccentBrush"], label.Foreground);
                        foreach (var scale in new[] { 1d, 1.5d, 2d })
                        {
                            ScrollBarThemeTests.Export(shell, $"integration-{page.GetType().Name}-{language}-{theme}-{scale}", scale);
                        }
                    }
                    if (page is ImportInboxListPage)
                    {
                        var selectAll = Assert.Single(FormControlThemeTests.Children<CheckBox>(page), box => Equals(box.Content, "全选"));
                        selectAll.IsChecked = true;
                        Assert.All(inbox.Items, item => Assert.True(item.IsSelected));
                        var refresh = Assert.Single(FormControlThemeTests.Children<Button>(page), button => Equals(button.Content, "刷新"));
                        Assert.Same(inbox.RefreshCommand, refresh.Command);
                    }
                    host.Children.Clear();
                }
            });
        }

        [Theory]
        [InlineData(typeof(ImportInboxListPage))]
        [InlineData(typeof(ImportInboxDetailPage))]
        [InlineData(typeof(LearningPlanPage))]
        public Task Header_LanguageAndTextScaleChanges_KeepBadgeCenteredAndContentSized(Type pageType)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var page = (UserControl)Activator.CreateInstance(pageType)!;
                var host = ScrollBarThemeTests.CreateHost(page);
                host.Width = 858;
                host.Height = 560;
                foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
                {
                    ThemePalette.Apply(host.Resources, theme);
                    foreach (var language in new[] { "日语", "英语" })
                    {
                        foreach (var textScale in new[] { 1d, 1.5d })
                        {
                            foreach (var longTitle in new[] { false, true })
                            {
                                var title = longTitle ? string.Concat(Enumerable.Repeat("较长的词组标题", 8)) : $"{language}导入收件箱";
                                page.DataContext = new { LanguageLabel = language, WindowTitle = title, SourceText = title };
                                host.Resources["BodyFontSize"] = 14 * textScale;
                                host.Resources["PageTitleFontSize"] = 28 * textScale;
                                ScrollBarThemeTests.Layout(host);
                                var label = Assert.Single(FormControlThemeTests.Children<TextBlock>(page), text =>
                                    text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == "LanguageLabel");
                                var badge = Assert.IsType<Border>(VisualTreeHelper.GetParent(label));
                                var header = Assert.IsType<Grid>(VisualTreeHelper.GetParent(badge));
                                ScrollBarThemeTests.Export(host, $"header-{pageType.Name}-{theme}-{language}-{textScale}-{longTitle}");
                                // TextBlock 可被拉高，需检查字符行的位置，不能把控件空白当作文字。
                                var characterBounds = label.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                                characterBounds.Union(label.ContentEnd.GetCharacterRect(LogicalDirection.Backward));
                                var textBounds = label.TransformToAncestor(badge).TransformBounds(characterBounds);
                                var badgeBounds = badge.TransformToAncestor(header).TransformBounds(new Rect(badge.RenderSize));
                                Assert.True(Math.Abs(textBounds.Top - (badge.ActualHeight - textBounds.Bottom)) <= 1,
                                    $"{pageType.Name} 语言文字上下留白不对称：{textBounds.Top} / {badge.ActualHeight - textBounds.Bottom}。");
                                Assert.InRange(Math.Abs(textBounds.Left - (badge.ActualWidth - textBounds.Right)), 0, 1);
                                Assert.InRange(Math.Abs(badgeBounds.Top + badgeBounds.Height / 2 - header.ActualHeight / 2), 0, 1);
                                Assert.InRange(badge.ActualHeight - label.DesiredSize.Height, 16, 24);
                                Assert.True(label.ActualHeight >= label.DesiredSize.Height - 0.5, "放大后的语言文字不得裁切。");
                                var heading = Assert.Single(FormControlThemeTests.Children<TextBlock>(header), text => text.Text == title);
                                var headingBounds = heading.TransformToAncestor(header).TransformBounds(new Rect(heading.RenderSize));
                                Assert.True(headingBounds.Right <= badgeBounds.Left, "长标题不得与语言标签重叠。");
                            }
                        }
                    }
                }
                return Task.CompletedTask;
            });
        }
    }
}
