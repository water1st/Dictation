namespace Dictation.DataAccess
{
    /// <summary>
    /// 表示迁移读取、备份或执行失败，阻止带不完整结构的数据库继续启动。
    /// </summary>
    public sealed class SchemaMigrationDataAccessException : DataAccessException
    {
        /// <summary>
        /// 记录迁移失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public SchemaMigrationDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
