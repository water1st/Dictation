using System.Text.Json.Serialization;

namespace Dictation.Core.Updating
{
    /// <summary>承接官方 Release JSON 元数据，不暴露到 UI。</summary>
    /// <param name="TagName">发行标签。</param>
    /// <param name="Draft">是否草稿。</param>
    /// <param name="Prerelease">是否预发行。</param>
    /// <param name="Assets">成品资产列表。</param>
    internal sealed record GitHubReleaseDocument(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubReleaseAssetDocument>? Assets);
}
