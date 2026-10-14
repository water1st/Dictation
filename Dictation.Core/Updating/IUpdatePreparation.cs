namespace Dictation.Core.Updating
{
    /// <summary>提供全量下载、校验和独占 runner 准备，不关闭应用或执行安装。</summary>
    public interface IUpdatePreparation
    {
        /// <summary>准备可信更新包和不锁原安装目录的运行程序。</summary>
        /// <param name="update">正式成对资产。</param>
        /// <param name="cancellationToken">取消下载与文件准备。</param>
        /// <returns>仅在完整准备成功后返回安装参数。</returns>
        Task<PreparedUpdateDto> PrepareAsync(AvailableUpdateDto update, CancellationToken cancellationToken = default);
    }
}
