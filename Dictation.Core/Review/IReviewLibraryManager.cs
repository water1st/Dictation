using Dictation.Core.Configuration;

namespace Dictation.Core.Review
{
    /// <summary>提供当前语言复习库的整组物理删除能力。</summary>
    public interface IReviewLibraryManager
    {
        /// <summary>删除完整词组、关联记忆、进度与历史，而非归档。</summary>
        /// <param name="language">复习库所属语言。</param>
        /// <param name="wordGroupId">要删除的词组稳定标识。</param>
        /// <param name="cancellationToken">取消删除的令牌。</param>
        Task DeleteWordGroupAsync(LearningLanguage language, string wordGroupId, CancellationToken cancellationToken = default);
    }
}
