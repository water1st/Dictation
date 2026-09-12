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
        Task<LearningLibraryPageDto> GetLibraryPageAsync(
            LearningLanguage language,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除指定语言学习库中的完整词组及其学习进度。
        /// </summary>
        Task DeleteWordGroupAsync(
            LearningLanguage language,
            string wordGroupId,
            CancellationToken cancellationToken = default);
    }
}
