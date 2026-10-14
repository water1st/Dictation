using Dictation.Core.Configuration;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 提供只读查询待处理导入批次的能力。
    /// </summary>
    public interface IImportInboxQuery
    {
        /// <summary>
        /// 读取指定学习语言尚待处理的导入批次。
        /// </summary>
        /// <param name="language">要筛选的学习语言。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>按数据访问层顺序返回的待处理批次。</returns>
        Task<IReadOnlyList<ImportBatchDto>> GetPendingBatchesAsync(
            LearningLanguage language,
            CancellationToken cancellationToken = default);
    }
}
