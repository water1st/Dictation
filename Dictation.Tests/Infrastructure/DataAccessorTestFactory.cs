using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Runtime;

namespace Dictation.Tests.Infrastructure
{
    internal static class DataAccessorTestFactory
    {
        internal static ImportInboxDataAccessor CreateImportInbox(SqliteConnectionFactory connectionFactory)
        {
            return new ImportInboxDataAccessor(connectionFactory, new WordGroupMemberSnapshotReader());
        }

        internal static LearningPlanDataAccessor CreateLearningPlan(SqliteConnectionFactory connectionFactory)
        {
            return new LearningPlanDataAccessor(connectionFactory, new WordGroupMemberSnapshotReader());
        }

        internal static LearningSessionDataAccessor CreateLearningSession(SqliteConnectionFactory connectionFactory)
        {
            return new LearningSessionDataAccessor(connectionFactory, new WordGroupMemberSnapshotReader());
        }
    }
}
