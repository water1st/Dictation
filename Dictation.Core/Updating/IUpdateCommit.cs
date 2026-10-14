namespace Dictation.Core.Updating
{
    /// <summary>新版确认就绪后清理该安装的旧程序备份。</summary>
    public interface IUpdateCommit
    {
        /// <summary>验证当前版本和收据，仅清理对应独占恢复事务。</summary>
        /// <param name="installation">已确认启动的安装收据。</param>
        /// <param name="cancellationToken">取消清理前的检查。</param>
        Task CommitAsync(UpdateInstallationDto installation, CancellationToken cancellationToken = default);
    }
}
