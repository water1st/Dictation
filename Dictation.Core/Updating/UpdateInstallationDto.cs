namespace Dictation.Core.Updating
{
    /// <summary>标识安装后尚未确认启动成功的独占恢复事务。</summary>
    /// <param name="RootDirectory">已更新的应用根目录。</param>
    /// <param name="TransactionDirectory">包含旧程序备份的独占CACHE任务。</param>
    /// <param name="Version">已安装且等待确认的三段版本。</param>
    public sealed record UpdateInstallationDto(string RootDirectory, string TransactionDirectory, string Version);
}
