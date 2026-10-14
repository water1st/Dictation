using Dictation.LexiconBuilder.WordNet;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.LexiconBuilder
{
    /// <summary>
    /// 验证ECDICT按词元与词性读取中文翻译的规则。
    /// </summary>
    public sealed class EcdictTranslationReaderTests
    {
        [Fact]
        public void Read_FiltersTranslationLinesByRequestedPartOfSpeech()
        {
            using var directory = new TemporaryDirectory();
            var path = CreateDatabase(directory.Path, new Dictionary<string, string>
            {
                ["go"] = "vi. 去；离开\nvt. 忍受\nn. 围棋\nadj. 可行的",
                ["do"] = "v. 做；干\nvi. 表现\nn. 聚会",
                ["child"] = "n. 儿童；孩子\nv. 生育",
                ["good"] = "a. 好的；优良的\nadj. 合适的\nad. 很好地\nadv. 有益地\nn. 好处"
            });
            using var reader = new EcdictTranslationReader(path);

            Assert.Equal(new[] { "去；离开", "忍受" }, reader.Read("go", EnglishPartOfSpeech.Verb));
            Assert.Equal(new[] { "做；干", "表现" }, reader.Read("do", EnglishPartOfSpeech.Verb));
            Assert.Equal(new[] { "儿童；孩子" }, reader.Read("child", EnglishPartOfSpeech.Noun));
            Assert.Equal(new[] { "好的；优良的", "合适的" }, reader.Read("good", EnglishPartOfSpeech.Adjective));
            Assert.Equal(new[] { "很好地", "有益地" }, reader.Read("good", EnglishPartOfSpeech.Adverb));
            Assert.Equal(new[] { "好的；优良的", "合适的" }, reader.Read("good", EnglishPartOfSpeech.AdjectiveSatellite));
        }

        [Fact]
        public void Read_DeduplicatesMeaningsAndIgnoresBlankOrUnrecognizedLines()
        {
            using var directory = new TemporaryDirectory();
            var path = CreateDatabase(directory.Path, new Dictionary<string, string>
            {
                ["go"] = "v. 去\nvi. 去\n\nprep. 到\nv.   "
            });
            using var reader = new EcdictTranslationReader(path);

            var result = reader.Read("go", EnglishPartOfSpeech.Verb);

            Assert.Equal(new[] { "去" }, result);
        }

        [Fact]
        public void Read_CachesDatabaseRowsByNormalizedLemma()
        {
            using var directory = new TemporaryDirectory();
            var path = CreateDatabase(directory.Path, new Dictionary<string, string>
            {
                ["go"] = "v. 去\nn. 围棋"
            });
            using var reader = new EcdictTranslationReader(path);
            Assert.Equal(new[] { "去" }, reader.Read("GO", EnglishPartOfSpeech.Verb));

            using (var connection = new SqliteConnection($"Data Source={path};Mode=ReadWrite;Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE stardict SET translation = 'v. 已修改' WHERE word = 'go';";
                command.ExecuteNonQuery();
            }

            Assert.Equal(new[] { "围棋" }, reader.Read("go", EnglishPartOfSpeech.Noun));
            Assert.Equal(new[] { "去" }, reader.Read("go", EnglishPartOfSpeech.Verb));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_RejectsBlankPath(string path)
        {
            Assert.Throws<ArgumentException>(() => new EcdictTranslationReader(path));
        }

        [Fact]
        public void Constructor_RejectsMissingDatabase()
        {
            var path = Path.Combine(Path.GetTempPath(), "Dictation.Tests", Guid.NewGuid().ToString("N"), "stardict.db");

            Assert.Throws<FileNotFoundException>(() => new EcdictTranslationReader(path));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Read_RejectsBlankLemma(string lemma)
        {
            using var directory = new TemporaryDirectory();
            var path = CreateDatabase(directory.Path, new Dictionary<string, string>());
            using var reader = new EcdictTranslationReader(path);

            Assert.Throws<ArgumentException>(() => reader.Read(lemma, EnglishPartOfSpeech.Verb));
        }

        [Fact]
        public void Read_RejectsUnknownPartOfSpeech()
        {
            using var directory = new TemporaryDirectory();
            var path = CreateDatabase(directory.Path, new Dictionary<string, string>());
            using var reader = new EcdictTranslationReader(path);

            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read("go", EnglishPartOfSpeech.Unknown));
        }

        internal static string CreateDatabase(string directory, IReadOnlyDictionary<string, string> translations)
        {
            var path = Path.Combine(directory, "stardict.db");
            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;Pooling=False");
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE stardict (word TEXT NOT NULL, translation TEXT, pos TEXT);";
                command.ExecuteNonQuery();
            }

            foreach (var pair in translations)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO stardict (word, translation, pos) VALUES ($word, $translation, $pos);";
                command.Parameters.AddWithValue("$word", pair.Key);
                command.Parameters.AddWithValue("$translation", pair.Value);
                command.Parameters.AddWithValue("$pos", "test");
                command.ExecuteNonQuery();
            }

            return path;
        }
    }
}
