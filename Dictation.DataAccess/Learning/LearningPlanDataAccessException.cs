namespace Dictation.DataAccess.Learning
{
    public sealed class LearningPlanDataAccessException : DataAccessException
    {
        public LearningPlanDataAccessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
