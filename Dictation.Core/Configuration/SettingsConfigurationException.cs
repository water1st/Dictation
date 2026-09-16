namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 表示持久化设置包含无法映射的配置值。
    /// </summary>
    public sealed class SettingsConfigurationException : Exception
    {
        public SettingsConfigurationException(string message)
            : base(message)
        {
        }
    }
}
