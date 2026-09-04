using Dictation.LexiconBuilder;
using Dictation.LexiconBuilder.JmDict;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.LexiconBuilder
{
    public sealed class TomoshiChineseGlossIndexTests
    {
        [Fact]
        public void Load_ReturnsExactSenseMeaningsAndNormalizesText()
        {
            using var directory = new TemporaryDirectory();
            var databasePath = Path.Combine(directory.Path, "tomoshi.db");
            CreateDatabase(
                databasePath,
                ("1358280", "zh-CN", """
                    {"senses":{"0":{"glosses":[{"text":" 吃 "},{"text":"吃"},{"text":"e\u0301"},{"text":"   "}]},"1":{"glosses":[{"text":"靠……生活"}]}}}
                    """),
                ("1358280", "zh-TW", """
                    {"senses":{"0":{"glosses":[{"text":"不应读取"}]}}}
                    """));

            var index = TomoshiChineseGlossIndex.Load(databasePath);

            Assert.Equal(new[] { "吃", "é" }, index.GetMeanings(1358280, 1));
            Assert.Equal(new[] { "靠……生活" }, index.GetMeanings(1358280, 2));
        }

        [Fact]
        public void GetMeanings_ReturnsEmptyForAbsentEntryOrSense()
        {
            using var directory = new TemporaryDirectory();
            var databasePath = Path.Combine(directory.Path, "tomoshi.db");
            CreateDatabase(
                databasePath,
                ("1358280", "zh-CN", """
                    {"senses":{"0":{"glosses":[{"text":"吃"}]}}}
                    """));
            var index = TomoshiChineseGlossIndex.Load(databasePath);

            Assert.Empty(index.GetMeanings(9999999, 1));
            Assert.Empty(index.GetMeanings(1358280, 2));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GetMeanings_RejectsNonPositiveSenseIndex(int senseIndex)
        {
            using var directory = new TemporaryDirectory();
            var databasePath = Path.Combine(directory.Path, "tomoshi.db");
            CreateDatabase(databasePath);
            var index = TomoshiChineseGlossIndex.Load(databasePath);

            var exception = Assert.Throws<LexiconBuildException>(() =>
                index.GetMeanings(1358280, senseIndex));

            Assert.Contains("Tomoshi", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Load_RejectsMalformedJsonWithSourceContext()
        {
            using var directory = new TemporaryDirectory();
            var databasePath = Path.Combine(directory.Path, "tomoshi.db");
            CreateDatabase(databasePath, ("1358280", "zh-CN", "not-json"));

            var exception = Assert.Throws<LexiconBuildException>(() =>
                TomoshiChineseGlossIndex.Load(databasePath));

            Assert.Contains("Tomoshi", exception.Message, StringComparison.Ordinal);
            Assert.Contains("1358280", exception.Message, StringComparison.Ordinal);
        }

        private static void CreateDatabase(
            string databasePath,
            params (string EntryId, string Locale, string Data)[] rows)
        {
            using var connection = new SqliteConnection(
                $"Data Source={databasePath};Mode=ReadWriteCreate;Pooling=False");
            connection.Open();
            using (var schema = connection.CreateCommand())
            {
                schema.CommandText = """
                    CREATE TABLE zh_defs (
                        entry_id TEXT NOT NULL,
                        locale TEXT NOT NULL DEFAULT 'zh-CN',
                        data TEXT NOT NULL,
                        PRIMARY KEY (entry_id, locale)
                    );
                    """;
                schema.ExecuteNonQuery();
            }

            foreach (var row in rows)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO zh_defs(entry_id, locale, data)
                    VALUES ($entryId, $locale, $data);
                    """;
                insert.Parameters.AddWithValue("$entryId", row.EntryId);
                insert.Parameters.AddWithValue("$locale", row.Locale);
                insert.Parameters.AddWithValue("$data", row.Data);
                insert.ExecuteNonQuery();
            }
        }
    }
}
