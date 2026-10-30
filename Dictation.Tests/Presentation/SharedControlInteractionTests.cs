using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.Presentation.Theme;
using Dictation.Presentation.Views;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证公共控件在空集合、交互与主题切换时的实际显示和操作合同。</summary>
    public sealed class SharedControlInteractionTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task ComboBox_EmptyCandidates_NeverShowsPopupAndPreservesEditing(bool editable)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var items = new ObservableCollection<string>();
                var combo = new ComboBox { IsEditable = editable, ItemsSource = items, Width = 220 };
                var host = FormControlThemeTests.CreateHost(combo);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
                using var source = new HwndSource(new HwndSourceParameters("空候选验收") { Width = 600, Height = 300 });
                source.RootVisual = host;
                FormControlThemeTests.Layout(host);
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                var toggle = (ToggleButton)combo.Template.FindName("DropDownToggle", combo);
                Assert.False(toggle.IsEnabled);
                combo.IsDropDownOpen = true;
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Assert.False(popup.IsOpen);
                Assert.True(combo.IsEnabled);
                if (editable)
                {
                    var input = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
                    input.Text = "食べる English";
                    Assert.Equal("食べる English", combo.Text);
                    Assert.True(input.IsEnabled);
                }
                items.Add("English");
                combo.IsDropDownOpen = false;
                FormControlThemeTests.Layout(host);
                Assert.True(toggle.IsEnabled);
                combo.IsDropDownOpen = true;
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Assert.True(popup.IsOpen);
                items.Clear();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Assert.False(popup.IsOpen);
                combo.IsDropDownOpen = false;
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(1d)]
        [InlineData(1.5d)]
        [InlineData(2d)]
        public Task Slider_ThemeAndScaleChanges_PreserveTrackCommandsAndBounds(double scale)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var slider = new Slider { Minimum = 1, Maximum = 500, Value = 20, TickFrequency = 1, IsSnapToTickEnabled = true };
                var host = FormControlThemeTests.CreateHost(slider);
                host.LayoutTransform = new ScaleTransform(scale, scale);
                foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
                {
                    ThemePalette.Apply(host.Resources, theme);
                    FormControlThemeTests.Layout(host);
                    var track = Assert.IsType<Track>(slider.Template.FindName("PART_Track", slider));
                    Assert.NotNull(track.Thumb.Template);
                    Assert.Contains(FormControlThemeTests.Children<Border>(track.Thumb), border => border.CornerRadius.TopLeft > 0);
                    Assert.True(track.Thumb.ActualWidth >= 20);
                    Assert.Equal(Slider.DecreaseLarge, track.DecreaseRepeatButton.Command);
                    Assert.Equal(Slider.IncreaseLarge, track.IncreaseRepeatButton.Command);
                    Slider.IncreaseSmall.Execute(null, slider);
                    Assert.Equal(theme == ResolvedTheme.Light ? 21 : 22, slider.Value);
                    slider.Value = 600;
                    Assert.Equal(500, slider.Value);
                    slider.Value = 0;
                    Assert.Equal(1, slider.Value);
                    slider.Value = theme == ResolvedTheme.Light ? 21 : 22;
                    slider.IsEnabled = false;
                    Assert.False(track.Thumb.IsEnabled);
                    slider.IsEnabled = true;
                }
                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(typeof(LearningPlanPage))]
        [InlineData(typeof(ReviewPlanPage))]
        [InlineData(typeof(ImportInboxListPage))]
        [InlineData(typeof(TextImportPage))]
        [InlineData(typeof(WrongAnswerListPage))]
        public Task SelectAll_EmptyPage_CannotBeInvoked(Type pageType)
        {
            return ReviewPlanPageTests.RunStaAsync(() =>
            {
                var page = (UserControl)Activator.CreateInstance(pageType)!;
                var host = FormControlThemeTests.CreateHost(page);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
                FormControlThemeTests.Layout(host);
                var selectAll = Assert.Single(FormControlThemeTests.Children<CheckBox>(page), control => control.Content is string text && text.StartsWith("全选"));
                Assert.False(selectAll.IsEnabled);
                return Task.CompletedTask;
            });
        }
    }
}
