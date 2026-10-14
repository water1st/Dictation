using Dictation.Core.Configuration;

namespace Dictation.Core.Learning
{
    /// <summary>
    /// 提供学习库分页读取和整组删除能力。
    /// </summary>
    public interface ILearningLibraryManager
    {
        /// <summary>
        /// 分页读取指定语言的学习库整组词条。
        /// </summary>
        /// <param name="language">学习库所属语言。</param>
        /// <param name="pageNumber">从 1 开始的页码。</param>
        /// <param name="pageSize">每页数量，取值范围为 1–500。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>包含请求页码、页大小、总数和当前页完整词组的分页结果。</returns>
        Task<LearningLibraryPageDto> GetLibraryPageAsync(
            LearningLanguage language,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除指定语言学习库中的完整词组及其学习进度。
        /// </summary>
        /// <param name="language">词组所属学习语言。</param>
        /// <param name="wordGroupId">要整组删除的稳定标识；不能指向其他语言或非学习库位置。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        Task DeleteWordGroupAsync(
            LearningLanguage language,
            string wordGroupId,
            CancellationToken cancellationToken = default);
    }
}
