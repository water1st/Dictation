using Dictation.Core.Importing;

namespace Dictation.Presentation.Navigation
{
    /// <summary>
    /// 提供导入收件箱列表打开当前批次详情的能力。
    /// </summary>
    public interface IImportInboxDetailNavigation
    {
        /// <summary>
        /// 打开属于当前同语言收件箱批次的指定项目详情。
        /// </summary>
        Task OpenImportInboxDetailAsync(
            ImportBatchDto batch,
            long itemId,
            CancellationToken cancellationToken = default);
    }
}
