using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Dictation.Presentation.Theme;
using Dictation.Presentation.Views;
using Dictation.Presentation.ViewModels;
using Path = System.Windows.Shapes.Path;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证评审要求的全量图文、粉色体系及尺寸比例。</summary>
    public sealed class ActionButtonReviewTests
    {
        public static IEnumerable<object[]> Pages()
        {
            return typeof(HomePage).Assembly.GetTypes()
                .Where(type => type.Namespace == typeof(HomePage).Namespace && type.Name.EndsWith("Page")
                    && typeof(UserControl).IsAssignableFrom(type))
                .Select(type => new object[] { type });
        }

        [Theory]
        [MemberData(nameof(Pages))]
        public Task Actions_EveryPage_DeclareVectorAndReadableLabel(Type pageType)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var page = (UserControl)Activator.CreateInstance(pageType)!;
                var roots = new List<DependencyObject> { page };
                foreach (DictionaryEntry entry in page.Resources)
                {
                    if (entry.Value is DataTemplate template && template.LoadContent() is DependencyObject content)
                    {
                        roots.Add(content);
                    }
                }
                foreach (var button in roots.SelectMany(LogicalChildren).OfType<Button>())
                {
                    if (LogicalChildren(button).OfType<LanguageBadge>().Any()) continue;
                    Assert.True(button.Tag is Geometry, $"{pageType.Name} 的 {button.Content} 缺少矢量图标。");
                    var labels = button.Content is string text ? new[] { text }
                        : LogicalChildren(button).OfType<TextBlock>().Select(label => label.Text).ToArray();
                    Assert.Contains(labels, label => !string.IsNullOrWhiteSpace(label) && label is not "🔊" and not "‹" and not "›");
                }
                return Task.CompletedTask;
            });
        }

        [Theory]
        [MemberData(nameof(Pages))]
        public Task Actions_PageOverrides_PreservePinkContainers(Type pageType)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var page = (UserControl)Activator.CreateInstance(pageType)!;
                var host = FormControlThemeTests.CreateHost(page);
                foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
                {
                    ThemePalette.Apply(host.Resources, theme);
                    FormControlThemeTests.Layout(host);
                    foreach (var button in LogicalChildren(page).OfType<Button>())
                    {
                        var color = Assert.IsType<SolidColorBrush>(button.Background).Color;
                        var cardStyle = page.TryFindResource("NavigationCardButtonStyle") as Style;
                        var isCard = false;
                        for (var style = button.Style; style is not null; style = style.BasedOn)
                        {
                            isCard |= ReferenceEquals(style, cardStyle);
                        }
                        if (isCard)
                        {
                            Assert.Equal(((SolidColorBrush)host.Resources["CardBackgroundBrush"]).Color, color);
                            if (page is HomePage) Assert.Contains(LogicalChildren(button), child => child is LanguageBadge);
                            continue;
                        }
                        Assert.True(color.A == 255 && color.R > color.B && color.B > color.G,
                            $"{pageType.Name} 的 {button.Content} 在 {theme} 下未使用粉色背景：{color}。");
                    }
                }
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Settings_SelectionChanges_KeepSelectedChoicesDistinct()
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                var page = new SettingsPage();
                var host = FormControlThemeTests.CreateHost(page);
                foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
                {
                    ThemePalette.Apply(host.Resources, theme);
                    page.DataContext = new { SelectedSection = SettingsSection.Appearance, IsJapaneseTarget = true, IsEnglishTarget = false,
                        Home = new { IsSystemThemeSelected = true, IsLightThemeSelected = false, IsDarkThemeSelected = false } };
                    FormControlThemeTests.Layout(host);
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    var buttons = LogicalChildren(page).OfType<Button>().Where(button => button.Content is string label
                        && new[] { "外观与主题", "词表导入", "跟随系统", "深色", "日语", "英语" }.Contains(label)).ToArray();
                    Assert.Equal(6, buttons.Length);
                    var colors = buttons.Select(button => ((SolidColorBrush)button.Background).Color).ToArray();
                    page.DataContext = new { SelectedSection = SettingsSection.Import, IsJapaneseTarget = false, IsEnglishTarget = true,
                        Home = new { IsSystemThemeSelected = false, IsLightThemeSelected = false, IsDarkThemeSelected = true } };
                    FormControlThemeTests.Layout(host);
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    for (var index = 0; index < buttons.Length; index++)
                    {
                        Assert.NotEqual(colors[index], ((SolidColorBrush)buttons[index].Background).Color);
                    }
                }
            });
        }

        [Theory]
        [InlineData(ResolvedTheme.Light)]
        [InlineData(ResolvedTheme.Dark)]
        public Task Actions_AllRoles_UsePinkContainers(ResolvedTheme theme)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                foreach (var key in new[] { "SecondaryActionButtonStyle", "PrimaryActionButtonStyle", "TertiaryActionButtonStyle", "DestructiveActionButtonStyle" })
                {
                    var button = new Button { Content = "操作" };
                    var host = FormControlThemeTests.CreateHost(button);
                    button.Style = (Style)host.Resources[key];
                    ThemePalette.Apply(host.Resources, theme);
                    FormControlThemeTests.Layout(host);
                    var color = Assert.IsType<SolidColorBrush>(button.Background).Color;
                    Assert.True(color.A == 255 && color.R > color.B && color.B > color.G,
                        $"{theme} 的 {key} 应使用粉色背景，实际为 {color}。");
                }
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Actions_LargerText_EnlargesVectorAndKeepsContentHeight()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var small = new Button { Content = "浏览本轮", FontSize = 16 };
                var large = new Button { Content = "浏览本轮", FontSize = 28 };
                var host = FormControlThemeTests.CreateHost(small);
                host.Children.Add(large);
                foreach (var button in new[] { small, large })
                {
                    button.Tag = host.Resources["SearchIconGeometry"];
                    button.Style = (Style)host.Resources["SecondaryActionButtonStyle"];
                }
                FormControlThemeTests.Layout(host);
                var smallIcon = Assert.Single(FormControlThemeTests.Children<Path>(small), path => path.Data is not null);
                var largeIcon = Assert.Single(FormControlThemeTests.Children<Path>(large), path => path.Data is not null);
                Assert.True(largeIcon.ActualHeight >= smallIcon.ActualHeight * 1.5);
                Assert.True(large.ActualHeight > small.ActualHeight);
                Assert.Equal(VerticalAlignment.Center, small.VerticalAlignment);
                Assert.Equal(VerticalAlignment.Center, large.VerticalAlignment);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public Task Actions_PressAnimation_CompletesAndRestoresScale()
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                var button = new Button { Content = "保存" };
                var host = FormControlThemeTests.CreateHost(button);
                using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("按钮动画验证") { Width = 600, Height = 600 });
                source.RootVisual = host;
                button.Style = (Style)host.Resources["SecondaryActionButtonStyle"];
                FormControlThemeTests.Layout(host);
                var trigger = Assert.Single(button.Template.Triggers.OfType<MultiDataTrigger>(), candidate =>
                    candidate.Conditions.Any(condition => condition.Binding is System.Windows.Data.Binding binding && binding.Path?.Path == "IsPressed"));
                Assert.Contains(trigger.Conditions, condition => condition.Binding is System.Windows.Data.Binding binding
                    && binding.Source is bool preference && preference == SystemParameters.ClientAreaAnimation
                    && Equals(condition.Value, true));
                var enter = Assert.Single(trigger.EnterActions.OfType<BeginStoryboard>()).Storyboard;
                var exit = Assert.Single(trigger.ExitActions.OfType<BeginStoryboard>()).Storyboard;
                var surface = Assert.IsType<Grid>(button.Template.FindName("ButtonSurface", button));
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                enter.Begin(button, button.Template, true);
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                enter.SeekAlignedToLastTick(button, TimeSpan.FromSeconds(1), TimeSeekOrigin.BeginTime);
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.InRange(Assert.IsType<ScaleTransform>(surface.RenderTransform).ScaleX, 0.95, 0.99);
                exit.Begin(button, button.Template, true);
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                exit.SeekAlignedToLastTick(button, TimeSpan.FromSeconds(1), TimeSeekOrigin.BeginTime);
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(1d, Assert.IsType<ScaleTransform>(surface.RenderTransform).ScaleX, 3);
                Assert.Equal(1d, Assert.IsType<ScaleTransform>(surface.RenderTransform).ScaleY, 3);
            });
        }

        [Fact]
        public Task Practice_BrowseAction_DoesNotStretchToHeaderHeight()
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var page = new PracticeSessionPage();
                var host = FormControlThemeTests.CreateHost(page);
                FormControlThemeTests.Layout(host);
                var button = Assert.Single(FormControlThemeTests.Children<Button>(page), candidate => candidate.Content as string == "浏览本轮");
                Assert.Equal(VerticalAlignment.Center, button.VerticalAlignment);
                Assert.InRange(button.ActualHeight, 36, 60);
                return Task.CompletedTask;
            });
        }

        private static IEnumerable<DependencyObject> LogicalChildren(DependencyObject root)
        {
            yield return root;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                foreach (var descendant in LogicalChildren(child)) yield return descendant;
            }
        }
    }
}
