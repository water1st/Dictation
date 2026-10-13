namespace Dictation.Core.Updating
{
    /// <summary>描述已校验完整包和复制出的独占 updater，不代表安装已经完成。</summary>
    /// <param name="JobDirectory">本次 CACHE 独占任务目录。</param>
    /// <param name="RunnerPath">独立运行目录内的更新 EXE。</param>
    /// <param name="PackagePath">下载并校验的 ZIP 路径。</param>
    /// <param name="Sha256">已核对的正式包哈希。</param>
    /// <param name="Version">正式三段版本。</param>
    /// <param name="RootDirectory">必须保留用户数据的原应用根目录。</param>
    public sealed record PreparedUpdateDto(string JobDirectory, string RunnerPath, string PackagePath,
        string Sha256, string Version, string RootDirectory);
}
