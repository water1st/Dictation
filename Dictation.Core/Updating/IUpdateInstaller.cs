namespace Dictation.Core.Updating
{
    /// <summary>提供可回滚的已校验全量包安装能力，不包含 UI、下载或进程生命周期。</summary>
    public interface IUpdateInstaller
    {
        /// <summary>验证完整 ZIP 并替换受管理文件，失败恢复旧程序，始终保留用户文件。</summary>
        /// <param name="request">目标目录、已下载 ZIP、期望 SHA-256 与版本。</param>
        /// <param name="cancellationToken">取消校验与安装；回滚不被取消打断。</param>
        /// <returns>持有旧文件备份的收据，必须在新版就绪后确认或在启动失败时回滚。</returns>
        Task<UpdateInstallationDto> ApplyAsync(ApplyUpdateRequestDto request, CancellationToken cancellationToken = default);
    }
}
