namespace Dictation.Services.Configuration
{
    public sealed class SettingsPersistenceException : Exception
    {
        public SettingsPersistenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
