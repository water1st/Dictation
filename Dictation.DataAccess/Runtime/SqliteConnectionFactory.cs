using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Runtime
{
    /// <summary>
    /// 为用户数据库创建启用外键、无连接池的 SQLite 连接。
    /// </summary>
    public sealed class SqliteConnectionFactory
    {
        private readonly ApplicationPaths applicationPaths;

        /// <summary>
        /// 为用户数据库创建启用外键、无连接池的 SQLite 连接。
        /// </summary>
        /// <param name="applicationPaths">资源、运行数据和备份路径。</param>
        public SqliteConnectionFactory(ApplicationPaths applicationPaths)
        {
            this.applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
        }

        /// <summary>
        /// 创建尚未打开的用户数据库连接，由调用方打开并释放。
        /// </summary>
        /// <returns>未打开的新连接，使用完毕必须释放。</returns>
        public SqliteConnection CreateConnection()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = applicationPaths.UserDataDatabase,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                ForeignKeys = true
            };
            return new SqliteConnection(connectionString.ToString());
        }
    }
}
