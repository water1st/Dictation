namespace Dictation.Services.Configuration
{
    public sealed class SettingsConfigurationException : Exception
    {
        public SettingsConfigurationException(string message)
            : base(message)
        {
        }
    }
}
