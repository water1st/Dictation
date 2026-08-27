namespace Dictation.DataAccess.Importing
{
    public sealed class ImportInboxDataAccessException : DataAccessException
    {
        public ImportInboxDataAccessException(string message)
            : this(message, new InvalidOperationException(message))
        {
        }

        public ImportInboxDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
