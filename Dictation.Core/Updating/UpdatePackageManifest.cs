using System.Text.Json.Serialization;

namespace Dictation.Core.Updating
{
    /// <summary>记录发行包管理的文件，不包含用户运行目录或清单自身。</summary>
    /// <param name="Version">major.minor.run_number 三段版本。</param>
    /// <param name="Files">规范化为正斜杠的相对文件路径。</param>
    internal sealed record UpdatePackageManifest(
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("files")] IReadOnlyList<string> Files);
}
