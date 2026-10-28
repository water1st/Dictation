using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证操作按钮的实际渲染、焦点布局与键盘状态。</summary>
    public sealed class ActionButtonThemeTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Button_KeyboardFocus_KeepsContentBoundsStable(bool primary) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var button = new Button { Content = "保存计划 日本語 English", Width = 320, FontSize = 24 };
            var host = FormControlThemeTests.CreateHost(button);
            button.Style = (Style)host.Resources[primary ? "PrimaryActionButtonStyle" : "SecondaryActionButtonStyle"];
            using var source = new HwndSource(new HwndSourceParameters("操作按钮焦点验收") { Width = 600, Height = 600 });
            source.RootVisual = host;
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                Keyboard.ClearFocus();
                ThemePalette.Apply(host.Resources, theme);
                FormControlThemeTests.Layout(host);
                var presenter = Assert.Single(FormControlThemeTests.Children<ContentPresenter>(button));
                var before = new Rect(presenter.TranslatePoint(new Point(), button), presenter.RenderSize);
                var normal = Render(button, $"{primary}-{theme}-normal");
                Assert.Same(button, Keyboard.Focus(button));
                FormControlThemeTests.Layout(host);
                Assert.True(button.IsKeyboardFocused);
                var focused = Render(button, $"{primary}-{theme}-focused");
                Assert.False(normal.SequenceEqual(focused), "键盘焦点必须在真实按钮上可见。");
                Assert.Equal(before, new Rect(presenter.TranslatePoint(new Point(), button), presenter.RenderSize));
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Button_DefaultAction_HasVisibleIndicatorWithoutMovingContent(bool primary) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var button = new Button { Content = "确认并继续", Width = 240 };
            var host = FormControlThemeTests.CreateHost(button);
            var input = new TextBox { Text = "作答" };
            host.Children.Add(input);
            button.Style = (Style)host.Resources[primary ? "PrimaryActionButtonStyle" : "SecondaryActionButtonStyle"];
            using var source = new HwndSource(new HwndSourceParameters("默认按钮验收") { Width = 600, Height = 600 });
            source.RootVisual = host;
            Assert.Same(input, Keyboard.Focus(input));
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                button.IsDefault = false;
                ThemePalette.Apply(host.Resources, theme);
                FormControlThemeTests.Layout(host);
                var normal = Render(button, $"{primary}-{theme}-ordinary");
                button.IsDefault = true;
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Assert.True(button.IsDefaulted);
                var defaulted = Render(button, $"{primary}-{theme}-default");
                Assert.False(normal.SequenceEqual(defaulted), "默认按钮必须与普通按钮具有不同的可见反馈。");
            }
            return Task.CompletedTask;
        });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task Button_SpaceAndEnter_PreserveCommandParameterAndPressedFeedback(bool primary) => ReviewPlanPageTests.RunStaAsync(() =>
        {
            var command = new RoutedCommand();
            var parameter = new object();
            var calls = 0;
            var button = new Button { Content = "提交答案", Command = command, CommandParameter = parameter, Width = 240 };
            var host = FormControlThemeTests.CreateHost(button);
            host.CommandBindings.Add(new CommandBinding(command, (_, args) =>
            {
                Assert.Same(parameter, args.Parameter);
                calls++;
            }, (_, args) => args.CanExecute = true));
            button.CommandTarget = host;
            button.Style = (Style)host.Resources[primary ? "PrimaryActionButtonStyle" : "SecondaryActionButtonStyle"];
            using var source = new HwndSource(new HwndSourceParameters("按钮键盘验收") { Width = 600, Height = 600 });
            source.RootVisual = host;
            ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
            FormControlThemeTests.Layout(host);
            CommandManager.InvalidateRequerySuggested();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            Assert.True(command.CanExecute(parameter, host));
            Assert.True(button.IsEnabled);
            Assert.Same(button, Keyboard.Focus(button));
            var normal = Render(button, $"{primary}-keyboard-normal");
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.True(button.IsPressed);
            Assert.Equal(0, calls);
            Assert.False(normal.SequenceEqual(Render(button, $"{primary}-keyboard-pressed")));
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
            Assert.False(button.IsPressed);
            Assert.Equal(1, calls);
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
            Assert.Equal(2, calls);
            return Task.CompletedTask;
        });

        [Fact]
        public Task Button_AsyncCommandBusy_DisablesAndRestoresWithoutBlockingCancel() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            var completion = new TaskCompletionSource();
            var calls = 0;
            var command = new AsyncCommand(async () => { calls++; await completion.Task; });
            var button = new Button { Content = "保存并应用", Command = command, Width = 240 };
            var cancel = new Button { Content = "取消操作", Command = new AsyncCommand(() =>
            {
                completion.SetResult();
                return Task.CompletedTask;
            }) };
            var host = FormControlThemeTests.CreateHost(button);
            host.Children.Add(cancel);
            ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
            FormControlThemeTests.Layout(host);
            var enabled = Render(button, "busy-enabled");
            var peer = new ButtonAutomationPeer(button);
            var invoke = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke);
            invoke.Invoke();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            Assert.False(button.IsEnabled);
            Assert.True(cancel.IsEnabled);
            Assert.False(enabled.SequenceEqual(Render(button, "busy-disabled")));
            command.Execute(null);
            Assert.Equal(1, calls);
            var cancelPeer = new ButtonAutomationPeer(cancel);
            ((IInvokeProvider)cancelPeer.GetPattern(PatternInterface.Invoke)).Invoke();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(completion.Task.IsCompletedSuccessfully);
            Assert.True(button.IsEnabled);
            Assert.True(enabled.SequenceEqual(Render(button, "busy-restored")));
        });

        internal static byte[] Render(FrameworkElement element, string name)
        {
            element.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(new VisualBrush(element), null, new Rect(element.RenderSize));
            }
            bitmap.Render(visual);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            var output = Environment.GetEnvironmentVariable("DICTATION_SCREENSHOT_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"button-{name}.png"));
                encoder.Save(stream);
            }
            return pixels;
        }
    }
}
