using System.Net;
using System.Text;
using System.Text.Json;
using Dictation.Core.Updating;

namespace Dictation.Tests.Core
{
    /// <summary>验证真实 HTTP 边界与正式发行元数据解析，不联网或下载实际大包。</summary>
    public sealed class GitHubUpdateProviderTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;
        private const string PackageName = "Dictation-win-x64.zip";
        private const string Base = "https://github.com/water1st/Dictation/releases/download/v1.0.2/";

        [Fact]
        public async Task Check_NewOfficialVersionOffersOnlyPairedAssetsWithoutDownloading()
        {
            using var handler = new ControlledHandler(Json());
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            var result = await provider.CheckAsync(new Version(1, 0, 1), Token);
            Assert.NotNull(result);
            Assert.Equal("1.0.2", result.Version);
            Assert.Equal(new Uri(Base + PackageName), result.PackageUri);
            Assert.Equal(new Uri(Base + PackageName + ".sha256"), result.ChecksumUri);
            Assert.Equal(new Uri("https://api.github.com/repos/water1st/Dictation/releases/latest"), Assert.Single(handler.Requests));
            Assert.Contains("Dictation", handler.UserAgent);
            Assert.Null(handler.Authorization);
        }

        [Theory]
        [InlineData("v1.0.1")]
        [InlineData("v1.0.0")]
        [InlineData("master-123")]
        public async Task Check_SameOlderOrLegacyTagDoesNotOfferUpdate(string tag)
        {
            using var handler = new ControlledHandler(Json(tag: tag));
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            Assert.Null(await provider.CheckAsync(new Version(1, 0, 1), Token));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task Check_DraftOrPrereleaseIsNotOffered(bool draft, bool prerelease)
        {
            using var handler = new ControlledHandler(Json(draft: draft, prerelease: prerelease));
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            Assert.Null(await provider.CheckAsync(new Version(1, 0, 1), Token));
        }

        [Fact]
        public async Task Check_NoReleaseIsNormalButServerErrorPropagates()
        {
            using var handler = new ControlledHandler(Json()) { Status = HttpStatusCode.NotFound };
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            Assert.Null(await provider.CheckAsync(new Version(1, 0, 1), Token));
            handler.Status = HttpStatusCode.InternalServerError;
            await Assert.ThrowsAsync<HttpRequestException>(() => provider.CheckAsync(new Version(1, 0, 1), Token));
        }

        [Theory]
        [InlineData("null")]
        [InlineData("not json")]
        [InlineData("{}")]
        public async Task Check_BadMetadataIsNotPretendedToBeNoUpdate(string json)
        {
            using var handler = new ControlledHandler(json);
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            await Assert.ThrowsAsync<InvalidDataException>(() => provider.CheckAsync(new Version(1, 0, 1), Token));
        }

        [Fact]
        public async Task Check_MissingChecksumDoesNotOfferUnverifiedPackage()
        {
            using var handler = new ControlledHandler(Json(omitChecksum: true));
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            await Assert.ThrowsAsync<InvalidDataException>(() => provider.CheckAsync(new Version(1, 0, 1), Token));
        }

        [Theory]
        [InlineData("https://example.com/Dictation-win-x64.zip")]
        [InlineData("http://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip")]
        [InlineData("https://github.com/other/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip")]
        public async Task Check_UntrustedAssetCannotBecomeDownloadTarget(string packageUrl)
        {
            using var handler = new ControlledHandler(Json(packageUrl: packageUrl));
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            await Assert.ThrowsAsync<InvalidDataException>(() => provider.CheckAsync(new Version(1, 0, 1), Token));
        }

        [Fact]
        public async Task Check_CancellationStopsPendingHttpRequest()
        {
            using var handler = new ControlledHandler(Json()) { Delay = true };
            using var provider = new GitHubUpdateProvider(new HttpClient(handler));
            using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Token);
            canceled.CancelAfter(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CheckAsync(new Version(1, 0, 1), canceled.Token));
        }

        private static string Json(string tag = "v1.0.2", bool draft = false, bool prerelease = false, bool omitChecksum = false, string? packageUrl = null)
        {
            var assets = new List<object> { new { name = PackageName, browser_download_url = packageUrl ?? Base + PackageName } };
            if (!omitChecksum) assets.Add(new { name = PackageName + ".sha256", browser_download_url = Base + PackageName + ".sha256" });
            return JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease, assets });
        }

        /// <summary>只替换网络传输，元数据处理、版本判断与信任边界仍走真实产品代码。</summary>
        private sealed class ControlledHandler(string json) : HttpMessageHandler
        {
            internal HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
            internal bool Delay { get; set; }
            internal List<Uri> Requests { get; } = new();
            internal string? UserAgent { get; private set; }
            internal string? Authorization { get; private set; }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri!);
                UserAgent = request.Headers.UserAgent.ToString();
                Authorization = request.Headers.Authorization?.ToString();
                if (Delay) await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(Status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }
        }
    }
}
