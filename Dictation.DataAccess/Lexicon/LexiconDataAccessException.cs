namespace Dictation.DataAccess.Lexicon
{
    public sealed class LexiconDataAccessException : DataAccessException
    {
        public LexiconDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
