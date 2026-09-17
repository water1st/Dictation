namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 表示词库文件、结构版本或查询不可用。
    /// </summary>
    public sealed class LexiconDataAccessException : DataAccessException
    {
        /// <summary>
        /// 记录失败说明及原始原因，不改变异常分类。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public LexiconDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
