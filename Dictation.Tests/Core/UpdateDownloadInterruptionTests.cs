using System.Net;
using System.Text.Json;
using Dictation.Core;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证真实流下载中途失败或取消后不遗留可安装包、不修改旧程序。</summary>
    public sealed class UpdateDownloadInterruptionTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Prepare_InterruptedBodyRemovesPartialJobAndPreservesInstalledProgram(bool cancel)
        {
            using var directory = new TemporaryDirectory();
            var root = directory.Path;
            Directory.CreateDirectory(Path.Combine(root, "Updater"));
            File.WriteAllText(Path.Combine(root, "Dictation.Presentation.exe"), "old-main");
            File.WriteAllText(Path.Combine(root, "Updater", "Dictation.Updater.exe"), "old-updater");
            File.WriteAllText(Path.Combine(root, "package-manifest.json"), JsonSerializer.Serialize(new
            {
                version = "1.0.1", files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" }
            }));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            using var provider = new GitHubUpdateProvider(new HttpClient(new InterruptedHandler(cancellation, cancel)));
            var collection = new ServiceCollection().AddDictationDataAccess(root).AddDictationCore();
            collection.AddSingleton(provider);
            using var services = collection.BuildServiceProvider();
            var update = new AvailableUpdateDto("1.0.2",
                new("https://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip"),
                new("https://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip.sha256"));
            var preparation = services.GetRequiredService<IUpdatePreparation>();
            if (cancel)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparation.PrepareAsync(update, cancellation.Token));
            else
                await Assert.ThrowsAsync<IOException>(() => preparation.PrepareAsync(update, cancellation.Token));
            Assert.Equal("old-main", File.ReadAllText(Path.Combine(root, "Dictation.Presentation.exe")));
            Assert.Equal("old-updater", File.ReadAllText(Path.Combine(root, "Updater", "Dictation.Updater.exe")));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "CACHE"), "*.zip", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateDirectories(Path.Combine(root, "CACHE", "Updates")));
        }

        /// <summary>只替代网络，流写入和任务清理由真实产品执行。</summary>
        private sealed class InterruptedHandler(CancellationTokenSource cancellation, bool cancel) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
                    ? (HttpContent)new StringContent(new string('0', 64) + "  Dictation-win-x64.zip\n")
                    : new StreamContent(new InterruptedStream(cancellation, cancel));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }
        }

        /// <summary>先输出实际字节，第二次读取时模拟网络断开或用户取消。</summary>
        private sealed class InterruptedStream(CancellationTokenSource cancellation, bool cancel) : Stream
        {
            private bool emitted;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (!emitted)
                {
                    emitted = true;
                    buffer.Span[0] = 42;
                    return ValueTask.FromResult(1);
                }
                if (cancel)
                {
                    cancellation.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                throw new IOException("受控下载中断");
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
