using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Runtime;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>
    /// 为测试构造真实DAO并注入共享快照读取组件，不提供替代业务实现。
    /// </summary>
    internal static class DataAccessorTestFactory
    {
        /// <summary>构造收件箱DAO及真实快照读取器。</summary>
        /// <param name="connectionFactory">测试隔离数据库的连接工厂。</param>
        internal static ImportInboxDataAccessor CreateImportInbox(SqliteConnectionFactory connectionFactory)
        {
            return new ImportInboxDataAccessor(connectionFactory, new WordGroupMemberSnapshotReader());
        }

        /// <summary>构造学习计划DAO及真实快照读取器。</summary>
        /// <param name="connectionFactory">测试隔离数据库的连接工厂。</param>
        internal static LearningPlanDataAccessor CreateLearningPlan(SqliteConnectionFactory connectionFactory)
        {
            return new LearningPlanDataAccessor(connectionFactory, new WordGroupMemberSnapshotReader());
        }

        /// <summary>构造学习会话DAO及真实快照读取器。</summary>
        /// <param name="connectionFactory">测试隔离数据库的连接工厂。</param>
        internal static LearningSessionDataAccessor CreateLearningSession(SqliteConnectionFactory connectionFactory)
        {
            return new LearningSessionDataAccessor(connectionFactory, new WordGroupMemberSnapshotReader());
        }
    }
}
