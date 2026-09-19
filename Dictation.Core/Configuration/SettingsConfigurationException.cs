namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 表示持久化设置包含无法映射的配置值。
    /// </summary>
    public sealed class SettingsConfigurationException : Exception
    {
        /// <summary>
        /// 记录失败说明。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        public SettingsConfigurationException(string message)
            : base(message)
        {
        }
    }
}
