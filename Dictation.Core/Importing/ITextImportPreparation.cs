using Dictation.Core.Configuration;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 把源语言文本词表解析为需要用户确认的精确词库义项候选。
    /// </summary>
    public interface ITextImportPreparation
    {
        /// <summary>
        /// 准备一份文本词表，不写入导入收件箱或用户学习库。
        /// </summary>
        /// <param name="language">首页选定且在本次导入中固定的学习语言。</param>
        /// <param name="text">每行一词的源语言文本。</param>
        /// <param name="cancellationToken">取消解析与词库查询的令牌。</param>
        /// <returns>保留原始行号和精确候选的准备结果。</returns>
        Task<TextImportPreparationDto> PrepareAsync(
            LearningLanguage language,
            string text,
            CancellationToken cancellationToken = default);
    }
}
