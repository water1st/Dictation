using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Runtime
{
    public sealed class SqliteConnectionFactory
    {
        private readonly ApplicationPaths applicationPaths;

        public SqliteConnectionFactory(ApplicationPaths applicationPaths)
        {
            this.applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
        }

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
