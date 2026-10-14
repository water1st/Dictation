namespace Dictation.Core.Updating
{
    /// <summary>提供已安装发行版的只读新版检查，不包含下载或安装。</summary>
    public interface IUpdateReleaseQuery
    {
        /// <summary>按安装清单检查正式新版本；开发目录不联网、不更新源码。</summary>
        /// <param name="cancellationToken">取消检查。</param>
        /// <returns>可用新版本或空；网络／数据错误由调用方显示状态。</returns>
        Task<AvailableUpdateDto?> CheckAsync(CancellationToken cancellationToken = default);
    }
}
