using Dictation.Presentation;
using Dictation.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    public sealed class OverlayServiceTests
    {
        [Fact]
        public void AddDictationPresentation_OverlayCapabilitiesAliasTheOriginalSingleton()
        {
            var services = new ServiceCollection();
            services.AddDictationPresentation();
            using var provider = services.BuildServiceProvider();

            var implementation = provider.GetRequiredService<OverlayService>();

            Assert.Same(implementation, provider.GetRequiredService<IOverlayRequester>());
            Assert.Same(implementation, provider.GetRequiredService<IOverlayHost>());
        }

        [Fact]
        public async Task DisposingRealProviderAfterResolvingOverlayAliases_CompletesActiveRequestOnce()
        {
            var services = new ServiceCollection();
            services.AddDictationPresentation();
            var provider = services.BuildServiceProvider();
            var requester = provider.GetRequiredService<IOverlayRequester>();
            var host = provider.GetRequiredService<IOverlayHost>();
            var implementation = provider.GetRequiredService<OverlayService>();
            Assert.Same(implementation, requester);
            Assert.Same(implementation, host);
            var hiddenNotificationCount = 0;
            host.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(IOverlayHost.CurrentRequest)
                    && !host.IsVisible)
                {
                    hiddenNotificationCount++;
                }
            };
            var completion = requester.ConfirmAsync(
                "确认",
                "退出吗？",
                TestContext.Current.CancellationToken);

            var exception = Record.Exception(provider.Dispose);

            Assert.Null(exception);
            Assert.False(await completion);
            Assert.False(host.IsVisible);
            Assert.Equal(1, hiddenNotificationCount);
        }

        [Fact]
        public async Task ShowMessageAsync_ExposesMessageUntilDismissed()
        {
            using var service = new OverlayService();

            var completion = service.ShowMessageAsync("错误", "加载失败");

            Assert.True(service.IsVisible);
            Assert.Equal(OverlayKind.Message, service.CurrentRequest?.Kind);
            Assert.Equal("错误", service.CurrentRequest?.Title);
            Assert.Equal("加载失败", service.CurrentRequest?.Message);

            service.Dismiss();
            await completion;

            Assert.False(service.IsVisible);
            Assert.Null(service.CurrentRequest);
        }

        [Fact]
        public async Task ConfirmAsync_ResolvesTrueWhenAccepted()
        {
            using var service = new OverlayService();
            var completion = service.ConfirmAsync("删除", "确定删除吗？");

            service.Accept();

            Assert.True(await completion);
            Assert.False(service.IsVisible);
        }

        [Fact]
        public async Task ConfirmAsync_ResolvesFalseWhenCancelledOrDismissed()
        {
            using var service = new OverlayService();
            var cancelled = service.ConfirmAsync("删除", "确定删除吗？");
            service.Cancel();

            Assert.False(await cancelled);

            var dismissed = service.ConfirmAsync("删除", "确定删除吗？");
            service.Dismiss();

            Assert.False(await dismissed);
        }

        [Fact]
        public async Task ShowBusyAsync_ExposesBusyStateUntilDismissed()
        {
            using var service = new OverlayService();

            var completion = service.ShowBusyAsync("加载", "正在加载词库");

            Assert.True(service.IsVisible);
            Assert.True(service.IsBusy);
            Assert.Equal(OverlayKind.Busy, service.CurrentRequest?.Kind);
            service.Dismiss();
            await completion;
            Assert.False(service.IsBusy);
        }

        [Fact]
        public async Task ShowAsync_RejectsConcurrentRequests()
        {
            using var service = new OverlayService();
            var completion = service.ShowMessageAsync("提示", "先完成当前提示");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmAsync("确认", "不能并发显示"));

            service.Dismiss();
            await completion;
        }

        [Fact]
        public async Task Dispose_CompletesActiveRequestAndHidesOverlay()
        {
            var service = new OverlayService();
            var completion = service.ConfirmAsync("确认", "退出吗？");

            service.Dispose();

            Assert.False(await completion);
            Assert.False(service.IsVisible);
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                service.ShowMessageAsync("提示", "已释放"));
        }
    }
}
