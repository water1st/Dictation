namespace Dictation.Core.Updating
{
    /// <summary>在新版启动失败时恢复此收据所属的旧程序。</summary>
    public interface IUpdateRollback
    {
        /// <summary>验证收据后恢复旧文件；恢复开始后不被取消打断。</summary>
        /// <param name="installation">尚未确认的安装收据。</param>
        /// <param name="cancellationToken">取消恢复开始前的等待。</param>
        Task RollbackAsync(UpdateInstallationDto installation, CancellationToken cancellationToken = default);
    }
}
