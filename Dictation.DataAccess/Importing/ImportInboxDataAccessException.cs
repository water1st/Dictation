namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示收件箱持久化或事务操作失败。
    /// </summary>
    public sealed class ImportInboxDataAccessException : DataAccessException
    {
        /// <summary>
        /// 记录失败说明及原始原因，不改变异常分类。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        public ImportInboxDataAccessException(string message)
            : this(message, new InvalidOperationException(message))
        {
        }

        /// <summary>
        /// 记录失败说明及原始原因，不改变异常分类。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public ImportInboxDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
