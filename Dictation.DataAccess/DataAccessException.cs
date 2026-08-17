namespace Dictation.DataAccess
{
    public class DataAccessException : Exception
    {
        public DataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class SettingsDataAccessException : DataAccessException
    {
        public SettingsDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
