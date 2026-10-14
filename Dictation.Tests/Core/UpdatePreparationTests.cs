using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>用真实磁盘和可控 HTTP 内容验证完整包校验及独占 runner 准备，不启动进程。</summary>
    public sealed class UpdatePreparationTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private static readonly Uri PackageUri = new("https://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip");
        private static readonly Uri ChecksumUri = new(PackageUri.AbsoluteUri + ".sha256");

        [Fact]
        public async Task Prepare_DownloadsVerifiedFullPackageAndCopiesIsolatedRunnerOnly()
        {
            using var fixture = new PreparationFixture();
            var prepared = await fixture.PrepareAsync();
            Assert.Equal(fixture.Root, prepared.RootDirectory);
            Assert.Equal("1.0.2", prepared.Version);
            Assert.Equal(fixture.Hash, prepared.Sha256);
            Assert.True(File.Exists(prepared.PackagePath));
            Assert.Equal(fixture.Hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(prepared.PackagePath))));
            Assert.Equal("old-runner", File.ReadAllText(prepared.RunnerPath));
            Assert.Equal("runner-dependency", File.ReadAllText(Path.Combine(Path.GetDirectoryName(prepared.RunnerPath)!, "dependency.dll")));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(prepared.RunnerPath)!, "user-note.txt")));
            Assert.StartsWith(Path.Combine(fixture.Root, "CACHE", "Updates") + Path.DirectorySeparatorChar, prepared.JobDirectory);
            Assert.Equal(new[] { ChecksumUri, PackageUri }, fixture.Handler.Requests);
            fixture.AssertUnchanged();
        }

        [Fact]
        public async Task Prepare_EachAttemptGetsAnIndependentJobWithoutOverwritingUserCache()
        {
            using var fixture = new PreparationFixture();
            var first = await fixture.PrepareAsync();
            var second = await fixture.PrepareAsync();
            Assert.NotEqual(first.JobDirectory, second.JobDirectory);
            Assert.True(File.Exists(first.PackagePath));
            Assert.True(File.Exists(second.PackagePath));
            Assert.Equal("user-cache", File.ReadAllText(Path.Combine(fixture.Root, "CACHE", "keep.bin")));
        }

        [Fact]
        public async Task Prepare_HashMismatchRemovesOwnedDownloadAndKeepsOriginalProgram()
        {
            using var fixture = new PreparationFixture();
            fixture.Handler.Checksum = new string('0', 64) + "  Dictation-win-x64.zip\n";
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PrepareAsync());
            fixture.AssertNoPreparedPackage();
            fixture.AssertUnchanged();
        }

        [Theory]
        [InlineData("not a checksum")]
        [InlineData("0000000000000000000000000000000000000000000000000000000000000000  different.zip")]
        public async Task Prepare_RejectsInvalidChecksumBeforeDownloadingZip(string checksum)
        {
            using var fixture = new PreparationFixture();
            fixture.Handler.Checksum = checksum;
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PrepareAsync());
            Assert.Equal(ChecksumUri, Assert.Single(fixture.Handler.Requests));
            fixture.AssertNoPreparedPackage();
            fixture.AssertUnchanged();
        }

        [Fact]
        public async Task Prepare_HttpFailureDoesNotCloseOrModifyInstalledProgram()
        {
            using var fixture = new PreparationFixture();
            fixture.Handler.PackageStatus = HttpStatusCode.ServiceUnavailable;
            await Assert.ThrowsAsync<HttpRequestException>(() => fixture.PrepareAsync());
            fixture.AssertNoPreparedPackage();
            fixture.AssertUnchanged();
        }

        [Fact]
        public async Task Prepare_InvalidZipWithCorrectChecksumStillDoesNotBecomeReady()
        {
            using var fixture = new PreparationFixture();
            fixture.Handler.PackageBytes = new byte[] { 1, 2, 3, 4 };
            fixture.Handler.Checksum = Convert.ToHexString(SHA256.HashData(fixture.Handler.PackageBytes)) + "  Dictation-win-x64.zip\n";
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PrepareAsync());
            fixture.AssertNoPreparedPackage();
            fixture.AssertUnchanged();
        }

        [Fact]
        public async Task Prepare_CancellationBeforeStartDoesNotSendRequestsOrCreateJob()
        {
            using var fixture = new PreparationFixture();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.PrepareAsync(canceled.Token));
            Assert.Empty(fixture.Handler.Requests);
            fixture.AssertNoPreparedPackage();
        }

        [Theory]
        [InlineData("https://example.com/Dictation-win-x64.zip")]
        [InlineData("http://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip")]
        public async Task Prepare_RejectsForgedDownloadAddressWithoutNetwork(string url)
        {
            using var fixture = new PreparationFixture();
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PrepareAsync(update: new("1.0.2", new Uri(url), ChecksumUri)));
            Assert.Empty(fixture.Handler.Requests);
            fixture.AssertNoPreparedPackage();
        }

        [Fact]
        public async Task Prepare_DevelopmentDirectoryDoesNotDownloadOrChangeSource()
        {
            using var fixture = new PreparationFixture();
            File.Delete(Path.Combine(fixture.Root, "package-manifest.json"));
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PrepareAsync());
            Assert.Empty(fixture.Handler.Requests);
            fixture.AssertUnchanged();
        }

        [Fact]
        public async Task Prepare_MissingRunnerIsRejectedBeforeNetwork()
        {
            using var fixture = new PreparationFixture();
            File.Delete(Path.Combine(fixture.Root, "Updater", "Dictation.Updater.exe"));
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PrepareAsync());
            Assert.Empty(fixture.Handler.Requests);
            fixture.AssertNoPreparedPackage();
        }

        /// <summary>组合原三层容器，磁盘保持真实，仅替换 HTTP 传输。</summary>
        private sealed class PreparationFixture : IDisposable
        {
            private readonly TemporaryDirectory directory = new();
            private readonly ServiceProvider services;
            private readonly GitHubUpdateProvider github;
            internal string Root => directory.Path;
            internal string Hash { get; }
            internal DownloadHandler Handler { get; }

            internal PreparationFixture()
            {
                Write("Dictation.Presentation.exe", "old-main");
                Write("Updater/Dictation.Updater.exe", "old-runner");
                Write("Updater/dependency.dll", "runner-dependency");
                Write("Updater/user-note.txt", "must-not-copy");
                Write("package-manifest.json", JsonSerializer.Serialize(new { version = "1.0.1",
                    files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe", "Updater/dependency.dll" } }));
                foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" }) Write(name + "/keep.bin", "user-" + name.ToLowerInvariant());
                using var memory = new MemoryStream();
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var file in new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" })
                    {
                        using var writer = new StreamWriter(zip.CreateEntry(file).Open());
                        writer.Write("new");
                    }
                    using var manifest = new StreamWriter(zip.CreateEntry("package-manifest.json").Open());
                    manifest.Write(JsonSerializer.Serialize(new { version = "1.0.2", files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" } }));
                }
                var bytes = memory.ToArray();
                Hash = Convert.ToHexString(SHA256.HashData(bytes));
                Handler = new DownloadHandler(bytes, Hash + "  Dictation-win-x64.zip\n");
                github = new GitHubUpdateProvider(new HttpClient(Handler));
                var collection = new ServiceCollection().AddDictationDataAccess(Root).AddDictationCore();
                collection.AddSingleton(github);
                services = collection.BuildServiceProvider();
            }

            internal Task<PreparedUpdateDto> PrepareAsync(CancellationToken? token = null, AvailableUpdateDto? update = null) =>
                services.GetRequiredService<IUpdatePreparation>().PrepareAsync(update ?? new("1.0.2", PackageUri, ChecksumUri), token ?? Token);
            internal void AssertUnchanged()
            {
                Assert.Equal("old-main", File.ReadAllText(Path.Combine(Root, "Dictation.Presentation.exe")));
                foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" })
                    Assert.Equal("user-" + name.ToLowerInvariant(), File.ReadAllText(Path.Combine(Root, name, "keep.bin")));
            }
            internal void AssertNoPreparedPackage() => Assert.Empty(Directory.EnumerateFiles(Path.Combine(Root, "CACHE"), "*.zip", SearchOption.AllDirectories));
            private void Write(string relative, string text)
            {
                var path = Path.Combine(Root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
            public void Dispose() { services.Dispose(); github.Dispose(); Handler.Dispose(); directory.Dispose(); }
        }

        /// <summary>提供确定的校验文件和完整包字节，不模拟磁盘、哈希或 runner 复制。</summary>
        private sealed class DownloadHandler(byte[] bytes, string checksum) : HttpMessageHandler
        {
            internal byte[] PackageBytes { get; set; } = bytes;
            internal string Checksum { get; set; } = checksum;
            internal HttpStatusCode PackageStatus { get; set; } = HttpStatusCode.OK;
            internal List<Uri> Requests { get; } = new();
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request.RequestUri!);
                return Task.FromResult(request.RequestUri == ChecksumUri
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Checksum) }
                    : new HttpResponseMessage(PackageStatus) { Content = new ByteArrayContent(PackageBytes) });
            }
        }
    }
}
