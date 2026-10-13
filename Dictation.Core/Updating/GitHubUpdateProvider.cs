using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dictation.Core.Updating
{
    /// <summary>适配固定 GitHub 仓库的正式发行元数据，不在检查时下载更新包。</summary>
    internal sealed class GitHubUpdateProvider : IDisposable
    {
        private readonly HttpClient client;

        /// <summary>接管独立 HTTP 客户端的生命周期。</summary>
        /// <param name="client">仅供本提供器使用的成熟 HTTP 传输。</param>
        public GitHubUpdateProvider(HttpClient client) => this.client = client ?? throw new ArgumentNullException(nameof(client));

        /// <summary>查询比当前版本新的正式发行，返回可校验的成对资产。</summary>
        /// <param name="current">已安装的三段版本。</param>
        /// <param name="cancellationToken">取消查询。</param>
        /// <returns>新版本资产，或不存在可用新版本时为空。</returns>
        internal async Task<AvailableUpdateDto?> CheckAsync(Version current, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(current);
            cancellationToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/water1st/Dictation/releases/latest");
            request.Headers.UserAgent.ParseAdd("Dictation-Updater/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            GitHubReleaseDocument? release;
            try { release = await JsonSerializer.DeserializeAsync<GitHubReleaseDocument>(stream, cancellationToken: timeout.Token); }
            catch (JsonException exception) { throw new InvalidDataException("发行元数据不是有效 JSON。", exception); }
            if (release is null || string.IsNullOrWhiteSpace(release.TagName)) throw new InvalidDataException("发行元数据缺少标签。");
            if (release.Draft || release.Prerelease) return null;
            var tag = release.TagName;
            if (tag.StartsWith('v')) tag = tag[1..];
            // 旧工作流的master-序号标签不能冒充新流程的正式数字版本。
            if (!Regex.IsMatch(tag, "\\A[0-9]+\\.[0-9]+\\.[0-9]+\\z") || !Version.TryParse(tag, out var version) || version <= current) return null;
            timeout.Token.ThrowIfCancellationRequested();
            return GitHubUpdateMapper.ToDto(release, version.ToString(3));
        }

        /// <summary>释放本提供器持有的 HTTP 资源。</summary>
        public void Dispose() => client.Dispose();
    }
}
