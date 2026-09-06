namespace Dictation.DataAccess.Learning
{
    public sealed class LearningSessionDataAccessException : DataAccessException
    {
        public LearningSessionDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
