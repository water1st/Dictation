using System.Text.Json.Serialization;

namespace Dictation.Core.Updating
{
    /// <summary>承接正式成品名称与官方浏览器下载地址。</summary>
    /// <param name="Name">完整资产文件名。</param>
    /// <param name="BrowserDownloadUrl">该资产的下载地址。</param>
    internal sealed record GitHubReleaseAssetDocument(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);
}
