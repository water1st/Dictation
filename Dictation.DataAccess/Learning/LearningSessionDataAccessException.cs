namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示学习会话或题目状态持久化操作失败。
    /// </summary>
    public sealed class LearningSessionDataAccessException : DataAccessException
    {
        /// <summary>
        /// 记录失败说明及原始原因，不改变异常分类。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public LearningSessionDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
