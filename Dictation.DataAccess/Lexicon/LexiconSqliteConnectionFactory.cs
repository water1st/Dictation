using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.DataAccess.Lexicon
{
    public sealed class LexiconSqliteConnectionFactory
    {
        private readonly ApplicationPaths applicationPaths;

        public LexiconSqliteConnectionFactory(ApplicationPaths applicationPaths)
        {
            this.applicationPaths = applicationPaths
                ?? throw new ArgumentNullException(nameof(applicationPaths));
        }

        public SqliteConnection CreateJapaneseConnection()
        {
            return CreateConnection(applicationPaths.JapaneseLexiconDatabase, "日语");
        }

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
