using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 按语言定位不可变词库并创建只读、无连接池的 SQLite 连接。
    /// </summary>
    public sealed class LexiconSqliteConnectionFactory
    {
        private readonly ApplicationPaths applicationPaths;

        /// <summary>
        /// 按语言定位不可变词库并创建只读、无连接池的 SQLite 连接。
        /// </summary>
        /// <param name="applicationPaths">资源、运行数据和备份路径。</param>
        public LexiconSqliteConnectionFactory(ApplicationPaths applicationPaths)
        {
            this.applicationPaths = applicationPaths
                ?? throw new ArgumentNullException(nameof(applicationPaths));
        }

        /// <summary>
        /// 创建尚未打开的日语只读词库连接，由调用方打开并释放。
        /// </summary>
        /// <returns>未打开的日语只读连接。</returns>
        public SqliteConnection CreateJapaneseConnection()
        {
            return CreateConnection(applicationPaths.JapaneseLexiconDatabase, "日语");
        }

        /// <summary>
        /// 创建尚未打开的英语只读词库连接，由调用方打开并释放。
        /// </summary>
        /// <returns>未打开的英语只读连接。</returns>
        public SqliteConnection CreateEnglishConnection()
        {
            return CreateConnection(applicationPaths.EnglishLexiconDatabase, "英语");
        }

        private static SqliteConnection CreateConnection(string path, string languageName)
        {
            if (!File.Exists(path))
            {
                var fileNotFound = new FileNotFoundException($"{languageName}词库文件不存在。", path);
                throw new LexiconDataAccessException($"{languageName}词库不可用：文件不存在。", fileNotFound);
            }

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                ForeignKeys = true
            };
            return new SqliteConnection(connectionString.ToString());
        }
    }
}
