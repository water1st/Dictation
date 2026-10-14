namespace Dictation.Core.Updating
{
    /// <summary>向独立更新进程传递已下载全量包的安装条件。</summary>
    /// <param name="RootDirectory">现有应用完全限定根目录。</param>
    /// <param name="PackagePath">已下载 ZIP 的完全限定路径。</param>
    /// <param name="Sha256">正式资产提供的 64 位十六进制 SHA-256。</param>
    /// <param name="Version">期望安装的 major.minor.run_number 三段版本。</param>
    public sealed record ApplyUpdateRequestDto(string RootDirectory, string PackagePath, string Sha256, string Version);
}
