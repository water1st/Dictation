namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 表示设置文件无法原子保存。
    /// </summary>
    public sealed class SettingsPersistenceException : Exception
    {
        public SettingsPersistenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
