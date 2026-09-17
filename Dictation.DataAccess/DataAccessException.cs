namespace Dictation.DataAccess
{
    /// <summary>
    /// 表示持久化操作失败，保留底层异常供诊断。
    /// </summary>
    public class DataAccessException : Exception
    {
        /// <summary>
        /// 记录失败说明及原始原因，不改变异常分类。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public DataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
