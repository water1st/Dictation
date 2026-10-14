namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 表示设置文件无法原子保存。
    /// </summary>
    public sealed class SettingsPersistenceException : Exception
    {
        /// <summary>
        /// 记录失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public SettingsPersistenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
