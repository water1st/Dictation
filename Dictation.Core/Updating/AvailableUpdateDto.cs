namespace Dictation.Core.Updating
{
    /// <summary>表示经过固定仓库和正式版本校验的一对完整发行资产。</summary>
    /// <param name="Version">三段正式版本，不含标签前缀。</param>
    /// <param name="PackageUri">固定 GitHub 仓库的全量 ZIP 地址。</param>
    /// <param name="ChecksumUri">同一发行标签下的 SHA-256 文件地址。</param>
    public sealed record AvailableUpdateDto(string Version, Uri PackageUri, Uri ChecksumUri);
}
