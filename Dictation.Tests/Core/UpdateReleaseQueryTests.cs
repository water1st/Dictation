using System.Net;
using System.Text.Json;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>用真实容器及安装清单验证发行目录查询和开发目录隔离。</summary>
    public sealed class UpdateReleaseQueryTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("1.0.1", true)]
        [InlineData("1.0.5", false)]
        public async Task Check_UsesInstalledManifestAndNeverUpdatesDevelopmentDirectory(string? installedVersion, bool offered)
        {
            using var directory = new TemporaryDirectory();
            if (installedVersion is not null)
            {
                File.WriteAllText(Path.Combine(directory.Path, "Dictation.Presentation.exe"), "program");
                File.WriteAllText(Path.Combine(directory.Path, "package-manifest.json"), JsonSerializer.Serialize(new {
                    version = installedVersion, files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" } }));
            }
            using var handler = new ReleaseHandler();
            using var github = new GitHubUpdateProvider(new HttpClient(handler));
            var services = new ServiceCollection().AddDictationDataAccess(directory.Path).AddDictationCore();
            services.AddSingleton(github);
            using var provider = services.BuildServiceProvider();
            var result = await provider.GetRequiredService<IUpdateReleaseQuery>().CheckAsync(TestContext.Current.CancellationToken);
            Assert.Equal(offered, result is not null);
            if (offered) Assert.Equal("1.0.2", result!.Version);
            Assert.Equal(installedVersion is null ? 0 : 1, handler.RequestCount);
            Assert.False(File.Exists(Path.Combine(directory.Path, "DATA", "UserData.db")));
        }

        /// <summary>仅提供固定发行元数据；不模拟版本判断、文件读取或容器行为。</summary>
        private sealed class ReleaseHandler : HttpMessageHandler
        {
            internal int RequestCount { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequestCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
                    {"tag_name":"v1.0.2","draft":false,"prerelease":false,"assets":[
                      {"name":"Dictation-win-x64.zip","browser_download_url":"https://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip"},
                      {"name":"Dictation-win-x64.zip.sha256","browser_download_url":"https://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip.sha256"}]}
                    """) });
            }
        }
    }
}
