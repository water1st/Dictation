namespace Dictation.DataAccess
{
    public class DataAccessException : Exception
    {
        public DataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class SchemaMigrationDataAccessException : DataAccessException
    {
        public SchemaMigrationDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
