namespace Dictation.Core.Updating
{
    /// <summary>记录独占安装事务的文件归属，供后续进程确认或恢复使用。</summary>
    /// <param name="RootDirectory">应用根目录。</param>
    /// <param name="PreviousVersion">可恢复的旧版本。</param>
    /// <param name="Version">等待启动确认的新版。</param>
    /// <param name="PreviousFiles">实际存在并已备份的旧文件。</param>
    /// <param name="NewFiles">本次安装的文件，包含发行清单。</param>
    internal sealed record UpdateRecoveryRecord(string RootDirectory, string PreviousVersion, string Version,
        IReadOnlyList<string> PreviousFiles, IReadOnlyList<string> NewFiles);
}
