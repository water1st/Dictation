using Dictation.DataAccess.Runtime;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>
    /// 在测试临时目录创建最小日英语SQLite词库夹具。
    /// </summary>
    internal static class LexiconTestDatabases
    {
        public static void CreateBoth(ApplicationPaths paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            CreateJapanese(paths);
            CreateEnglish(paths);
        }

        public static void CreateJapanese(ApplicationPaths paths, string schemaVersion = "2")
        {
            ArgumentNullException.ThrowIfNull(paths);
            Directory.CreateDirectory(paths.Lexicons);
            using var connection = OpenForWrite(paths.JapaneseLexiconDatabase);
            Execute(connection, """
                CREATE TABLE BuildMetadata (
                    Key TEXT NOT NULL PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                CREATE TABLE Entries (
                    EntryId TEXT NOT NULL PRIMARY KEY,
                    SourceSequence INTEGER NOT NULL,
                    SenseIndex INTEGER NOT NULL,
                    PrimaryWritten TEXT NOT NULL,
                    PrimaryReading TEXT NOT NULL,
                    LexicalCategory TEXT NOT NULL,
                    VerbClass TEXT NOT NULL,
                    Transitivity TEXT NOT NULL,
                    KanjiUsage TEXT NOT NULL,
                    IsPhrase INTEGER NOT NULL
                );
                CREATE TABLE PartsOfSpeech (
                    EntryId TEXT NOT NULL,
                    SortIndex INTEGER NOT NULL,
                    Value TEXT NOT NULL,
                    PRIMARY KEY (EntryId, SortIndex),
                    FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE
                );
                CREATE TABLE SourceGlosses (
                    EntryId TEXT NOT NULL,
                    Language TEXT NOT NULL,
                    GlossIndex INTEGER NOT NULL,
                    GlossType TEXT NULL,
                    Text TEXT NOT NULL,
                    PRIMARY KEY (EntryId, Language, GlossIndex),
                    FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE
                );
                CREATE TABLE ChineseMeanings (
                    EntryId TEXT NOT NULL,
                    MeaningIndex INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    MeaningSource TEXT NOT NULL,
                    MeaningPrecision TEXT NOT NULL,
                    PRIMARY KEY (EntryId, MeaningIndex),
                    FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE
                );
                CREATE TABLE Forms (
                    FormId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    EntryId TEXT NOT NULL,
                    Surface TEXT NOT NULL,
                    Reading TEXT NOT NULL,
                    FormKind TEXT NOT NULL,
                    ScriptKind TEXT NOT NULL,
                    LexicalRole TEXT NOT NULL,
                    IsCommon INTEGER NOT NULL,
                    IsPrimary INTEGER NOT NULL,
                    UNIQUE (EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole),
                    FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE
                );
                CREATE INDEX IX_Forms_Surface ON Forms (Surface);
                """);
            Execute(connection,
                "INSERT INTO BuildMetadata (Key, Value) VALUES ('schema_version', $schemaVersion);",
                ("$schemaVersion", schemaVersion));

            var verbForms = new[]
            {
                ("食べる", "たべる", "Dictionary", "Kanji", "Verb", true, true),
                ("たべる", "たべる", "Dictionary", "Kana", "Verb", true, false),
                ("食べます", "たべます", "Polite", "Kanji", "Verb", true, true),
                ("たべます", "たべます", "Polite", "Kana", "Verb", true, false),
                ("食べて", "たべて", "Te", "Kanji", "Verb", true, true),
                ("たべて", "たべて", "Te", "Kana", "Verb", true, false),
                ("食べた", "たべた", "Ta", "Kanji", "Verb", true, true),
                ("たべた", "たべた", "Ta", "Kana", "Verb", true, false)
            };
            AddJapaneseEntry(
                connection,
                "jmdict:1:1",
                1,
                1,
                "食べる",
                "たべる",
                "Verb",
                "Ichidan",
                "Transitive",
                "Common",
                false,
                new[] { "Ichidan verb", "transitive verb" },
                new[] { ("eng", "to eat"), ("eng", "to consume food"), ("deu", "essen") },
                verbForms,
                new[] { "吃", "吃饭" });
            AddJapaneseEntry(
                connection,
                "jmdict:1:2",
                1,
                2,
                "食べる",
                "たべる",
                "Verb",
                "Ichidan",
                "Transitive",
                "Common",
                false,
                new[] { "Ichidan verb", "transitive verb" },
                new[] { ("eng", "to have a meal") },
                verbForms,
                new[] { "用餐" });

            AddJapaneseEntry(
                connection,
                "jmdict:2:1",
                2,
                1,
                "食前",
                "しょくぜん",
                "Noun",
                "None",
                string.Empty,
                "Common",
                false,
                new[] { "noun" },
                new[] { ("eng", "before a meal") },
                new[] { ("食前", "しょくぜん", "Dictionary", "Kanji", "Noun", true, true), ("しょくぜん", "しょくぜん", "Dictionary", "Kana", "Noun", true, false) },
                new[] { "饭前" });

            AddJapaneseEntry(
                connection,
                "jmdict:10:1",
                10,
                1,
                "食事",
                "しょくじ",
                "Noun",
                "None",
                string.Empty,
                "Common",
                false,
                new[] { "noun" },
                new[] { ("eng", "meal") },
                new[] { ("食事", "しょくじ", "Dictionary", "Kanji", "Noun", true, true), ("しょくじ", "しょくじ", "Dictionary", "Kana", "Noun", true, false) },
                new[] { "吃饭" });

            AddJapaneseEntry(
                connection,
                "jmdict:3:1",
                3,
                1,
                "%",
                "%",
                "Other",
                "None",
                string.Empty,
                "None",
                false,
                Array.Empty<string>(),
                Array.Empty<(string Language, string Text)>(),
                new[]
                {
                    ("%", "%", "Dictionary", "Kana", "Other", false, true),
                    ("_", "_", "Dictionary", "Kana", "Other", false, false),
                    ("\\", "\\", "Dictionary", "Kana", "Other", false, false),
                    ("ガ", "ガ", "Dictionary", "Kana", "Other", false, false)
                },
                Array.Empty<string>());

            AddJapaneseEntry(
                connection,
                "jmdict:4:1",
                4,
                1,
                "書く",
                "かく",
                "Verb",
                "GodanK",
                "Transitive",
                "Common",
                false,
                new[] { "Godan verb with 'ku' ending", "transitive verb" },
                new[] { ("eng", "to write") },
                new[]
                {
                    ("書く", "かく", "Dictionary", "Kanji", "Verb", true, true),
                    ("かく", "かく", "Dictionary", "Kana", "Verb", true, false)
                },
                new[] { "写" });

            AddJapaneseEntry(
                connection,
                "jmdict:6:1",
                6,
                1,
                "跪く",
                "ひざまずく",
                "Verb",
                "GodanK",
                "Intransitive",
                "Rare",
                false,
                new[] { "Godan verb with 'ku' ending", "intransitive verb" },
                new[] { ("eng", "to kneel") },
                new[]
                {
                    ("跪く", "ひざまずく", "Dictionary", "Kanji", "Verb", true, true),
                    ("ひざまずく", "ひざまずく", "Dictionary", "Kana", "Verb", true, false)
                },
                new[] { "跪下" });

            AddJapaneseEntry(
                connection,
                "jmdict:5:1",
                5,
                1,
                "勉強",
                "べんきょう",
                "NounVerb",
                "Suru",
                "Transitive",
                "Common",
                false,
                new[] { "noun", "suru verb - irregular" },
                new[] { ("eng", "study") },
                new[]
                {
                    ("勉強", "べんきょう", "Dictionary", "Kanji", "Noun", true, true),
                    ("べんきょう", "べんきょう", "Dictionary", "Kana", "Noun", true, false),
                    ("勉強する", "べんきょうする", "Dictionary", "Kanji", "Verb", true, true),
                    ("べんきょうする", "べんきょうする", "Dictionary", "Kana", "Verb", true, false)
                },
                new[] { "学习" });
        }

        public static void CreateEnglish(ApplicationPaths paths, string schemaVersion = "2")
        {
            ArgumentNullException.ThrowIfNull(paths);
            Directory.CreateDirectory(paths.Lexicons);
            using var connection = OpenForWrite(paths.EnglishLexiconDatabase);
            Execute(connection, """
                CREATE TABLE BuildMetadata (
                    Key TEXT NOT NULL PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                CREATE TABLE Lexemes (
                    LexemeId TEXT NOT NULL PRIMARY KEY,
                    Lemma TEXT NOT NULL,
                    SourceEntryKey TEXT NOT NULL,
                    PartOfSpeech TEXT NOT NULL,
                    IsPhrase INTEGER NOT NULL
                );
                CREATE TABLE Synsets (
                    SynsetId TEXT NOT NULL PRIMARY KEY,
                    PartOfSpeech TEXT NOT NULL,
                    Ili TEXT NULL
                );
                CREATE TABLE Senses (
                    SenseId TEXT NOT NULL PRIMARY KEY,
                    LexemeId TEXT NOT NULL,
                    SynsetId TEXT NOT NULL,
                    SenseOrder INTEGER NOT NULL,
                    FOREIGN KEY (LexemeId) REFERENCES Lexemes (LexemeId),
                    FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId)
                );
                CREATE TABLE Definitions (
                    SynsetId TEXT NOT NULL,
                    DefinitionIndex INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    PRIMARY KEY (SynsetId, DefinitionIndex),
                    FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId)
                );
                CREATE TABLE Examples (
                    SynsetId TEXT NOT NULL,
                    ExampleIndex INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    PRIMARY KEY (SynsetId, ExampleIndex),
                    FOREIGN KEY (SynsetId) REFERENCES Synsets (SynsetId)
                );
                CREATE TABLE Forms (
                    FormId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    LexemeId TEXT NOT NULL,
                    Surface TEXT NOT NULL,
                    FormKind TEXT NOT NULL,
                    FormSourceKind TEXT NOT NULL,
                    UNIQUE (LexemeId, Surface, FormKind),
                    FOREIGN KEY (LexemeId) REFERENCES Lexemes (LexemeId)
                );
                CREATE TABLE Derivations (
                    SourceSenseId TEXT NOT NULL,
                    TargetSenseId TEXT NOT NULL,
                    PRIMARY KEY (SourceSenseId, TargetSenseId),
                    FOREIGN KEY (SourceSenseId) REFERENCES Senses (SenseId),
                    FOREIGN KEY (TargetSenseId) REFERENCES Senses (SenseId)
                );
                CREATE TABLE LearningSenses (
                    LearningSenseId TEXT NOT NULL PRIMARY KEY,
                    SourceLexemeId TEXT NOT NULL,
                    Headword TEXT NOT NULL,
                    PartOfSpeech TEXT NOT NULL,
                    IsPhrase INTEGER NOT NULL,
                    MeaningSource TEXT NOT NULL,
                    MeaningPrecision TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL
                );
                CREATE TABLE LearningSenseSources (
                    LearningSenseId TEXT NOT NULL,
                    SenseId TEXT NOT NULL,
                    PRIMARY KEY (LearningSenseId, SenseId),
                    FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId),
                    FOREIGN KEY (SenseId) REFERENCES Senses (SenseId)
                );
                CREATE TABLE LearningMeanings (
                    LearningSenseId TEXT NOT NULL,
                    MeaningIndex INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    PRIMARY KEY (LearningSenseId, MeaningIndex),
                    FOREIGN KEY (LearningSenseId) REFERENCES LearningSenses (LearningSenseId)
                );
                CREATE INDEX IX_Forms_Surface ON Forms (Surface);
                """);
            Execute(connection,
                "INSERT INTO BuildMetadata (Key, Value) VALUES ('schema_version', $schemaVersion);",
                ("$schemaVersion", schemaVersion));

            AddEnglishLexeme(connection, "go%v", "go", "v", "v", false);
            AddEnglishLexeme(connection, "convert%v", "convert", "v", "v", false);
            AddEnglishLexeme(connection, "conversion%n", "conversion", "n", "n", false);
            AddEnglishLexeme(connection, "take off%v", "take off", "v", "v", true);
            AddEnglishLexeme(connection, "symbols%v", "symbols", "v", "v", false);

            AddEnglishSynset(connection, "go-synset-1", "v", "to move from one place to another", "go quickly");
            AddEnglishSynset(connection, "go-synset-2", "v", "to operate or function", null);
            AddEnglishSynset(connection, "go-synset-3", "v", "to happen", null);
            AddEnglishSynset(connection, "convert-synset", "v", "to change into another form", null);
            AddEnglishSynset(connection, "conversion-synset", "n", "the act of converting", null);
            AddEnglishSynset(connection, "take-off-synset", "v", "to leave the ground", null);
            AddEnglishSynset(connection, "symbols-synset", "v", "a definition containing percent", null);

            AddEnglishSense(connection, "go%2:01:00::", "go%v", "go-synset-1", 1);
            AddEnglishSense(connection, "go%2:02:00::", "go%v", "go-synset-2", 2);
            AddEnglishSense(connection, "go%2:03:00::", "go%v", "go-synset-3", 3);
            AddEnglishSense(connection, "convert%2:30:00::", "convert%v", "convert-synset", 1);
            AddEnglishSense(connection, "conversion%1:04:00::", "conversion%n", "conversion-synset", 1);
            AddEnglishSense(connection, "take_off%2:38:00::", "take off%v", "take-off-synset", 1);
            AddEnglishSense(connection, "symbols%2:30:00::", "symbols%v", "symbols-synset", 1);

            AddEnglishForm(connection, "go%v", "go", "Base", "Rule");
            AddEnglishForm(connection, "go%v", "goes", "ThirdPersonSingular", "Rule");
            AddEnglishForm(connection, "go%v", "went", "Past", "Source");
            AddEnglishForm(connection, "go%v", "gone", "PastParticiple", "Source");
            AddEnglishForm(connection, "go%v", "going", "PresentParticiple", "Rule");
            AddEnglishForm(connection, "convert%v", "convert", "Base", "Rule");
            AddEnglishForm(connection, "convert%v", "converts", "ThirdPersonSingular", "Rule");
            AddEnglishForm(connection, "convert%v", "converted", "Past", "Rule");
            AddEnglishForm(connection, "convert%v", "converted", "PastParticiple", "Rule");
            AddEnglishForm(connection, "convert%v", "converting", "PresentParticiple", "Rule");
            AddEnglishForm(connection, "conversion%n", "conversion", "Base", "Rule");
            AddEnglishForm(connection, "take off%v", "take off", "Base", "Rule");
            AddEnglishForm(connection, "symbols%v", "%", "Base", "Source");
            AddEnglishForm(connection, "symbols%v", "_", "ThirdPersonSingular", "Source");
            AddEnglishForm(connection, "symbols%v", "\\", "Past", "Source");

            AddEnglishDerivation(connection, "go%2:01:00::", "convert%2:30:00::");
            AddEnglishDerivation(connection, "convert%2:30:00::", "conversion%1:04:00::");
            AddEnglishDerivation(connection, "go%2:01:00::", "take_off%2:38:00::");

            AddEnglishLearningSense(connection, "go%2:01:00::", "go%v", "go", "v", false, "ChineseOpenWordNet", "Exact", 0, new[] { "go%2:01:00::" }, new[] { "去", "行走" });
            AddEnglishLearningSense(connection, "ecdict:go:v", "oewn:go:v", "go", "v", false, "Ecdict", "Broad", 1, new[] { "go%2:02:00::", "go%2:03:00::" }, new[] { "运行", "运转" });
            AddEnglishLearningSense(connection, "convert%2:30:00::", "convert%v", "convert", "v", false, "ChineseOpenWordNet", "Exact", 2, new[] { "convert%2:30:00::" }, new[] { "转换" });
            AddEnglishLearningSense(connection, "missing:conversion:n", "oewn:conversion:n", "conversion", "n", false, "Missing", "Missing", 3, new[] { "conversion%1:04:00::" }, Array.Empty<string>());
            AddEnglishLearningSense(connection, "take_off%2:38:00::", "take off%v", "take off", "v", true, "ChineseOpenWordNet", "Exact", 4, new[] { "take_off%2:38:00::" }, new[] { "起飞" });
            AddEnglishLearningSense(connection, "missing:symbols:v", "oewn:symbols:v", "symbols", "v", false, "Missing", "Missing", 5, new[] { "symbols%2:30:00::" }, Array.Empty<string>());
        }

        private static SqliteConnection OpenForWrite(string path)
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                ForeignKeys = true
            }.ToString());
            connection.Open();
            return connection;
        }

        private static void AddJapaneseEntry(
            SqliteConnection connection,
            string entryId,
            int sourceSequence,
            int senseIndex,
            string primaryWritten,
            string primaryReading,
            string lexicalCategory,
            string verbClass,
            string transitivity,
            string kanjiUsage,
            bool isPhrase,
            IReadOnlyList<string> partsOfSpeech,
            IReadOnlyList<(string Language, string Text)> glosses,
            IReadOnlyList<(string Surface, string Reading, string FormKind, string ScriptKind, string LexicalRole, bool IsCommon, bool IsPrimary)> forms,
            IReadOnlyList<string> chineseMeanings)
        {
            Execute(connection, """
                INSERT INTO Entries (
                    EntryId, SourceSequence, SenseIndex, PrimaryWritten, PrimaryReading,
                    LexicalCategory, VerbClass, Transitivity, KanjiUsage, IsPhrase)
                VALUES ($entryId, $sourceSequence, $senseIndex, $primaryWritten, $primaryReading,
                    $lexicalCategory, $verbClass, $transitivity, $kanjiUsage, $isPhrase);
                """,
                ("$entryId", entryId),
                ("$sourceSequence", sourceSequence),
                ("$senseIndex", senseIndex),
                ("$primaryWritten", primaryWritten),
                ("$primaryReading", primaryReading),
                ("$lexicalCategory", lexicalCategory),
                ("$verbClass", verbClass),
                ("$transitivity", transitivity),
                ("$kanjiUsage", kanjiUsage),
                ("$isPhrase", isPhrase ? 1 : 0));

            for (var index = 0; index < partsOfSpeech.Count; index++)
            {
                Execute(connection,
                    "INSERT INTO PartsOfSpeech (EntryId, SortIndex, Value) VALUES ($entryId, $sortIndex, $value);",
                    ("$entryId", entryId),
                    ("$sortIndex", index),
                    ("$value", partsOfSpeech[index]));
            }

            for (var index = 0; index < glosses.Count; index++)
            {
                Execute(connection,
                    "INSERT INTO SourceGlosses (EntryId, Language, GlossIndex, GlossType, Text) VALUES ($entryId, $language, $glossIndex, NULL, $text);",
                    ("$entryId", entryId),
                    ("$language", glosses[index].Language),
                    ("$glossIndex", index),
                    ("$text", glosses[index].Text));
            }

            for (var index = 0; index < chineseMeanings.Count; index++)
            {
                Execute(connection,
                    "INSERT INTO ChineseMeanings (EntryId, MeaningIndex, Text, MeaningSource, MeaningPrecision) VALUES ($entryId, $meaningIndex, $text, 'Tomoshi', 'Exact');",
                    ("$entryId", entryId),
                    ("$meaningIndex", index),
                    ("$text", chineseMeanings[index]));
            }

            foreach (var form in forms)
            {
                Execute(connection, """
                    INSERT INTO Forms (
                        EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole, IsCommon, IsPrimary)
                    VALUES ($entryId, $surface, $reading, $formKind, $scriptKind, $lexicalRole, $isCommon, $isPrimary);
                    """,
                    ("$entryId", entryId),
                    ("$surface", form.Surface),
                    ("$reading", form.Reading),
                    ("$formKind", form.FormKind),
                    ("$scriptKind", form.ScriptKind),
                    ("$lexicalRole", form.LexicalRole),
                    ("$isCommon", form.IsCommon ? 1 : 0),
                    ("$isPrimary", form.IsPrimary ? 1 : 0));
            }
        }

        private static void AddEnglishLexeme(
            SqliteConnection connection,
            string lexemeId,
            string lemma,
            string sourceEntryKey,
            string partOfSpeech,
            bool isPhrase)
        {
            Execute(connection,
                "INSERT INTO Lexemes (LexemeId, Lemma, SourceEntryKey, PartOfSpeech, IsPhrase) VALUES ($lexemeId, $lemma, $sourceEntryKey, $partOfSpeech, $isPhrase);",
                ("$lexemeId", lexemeId),
                ("$lemma", lemma),
                ("$sourceEntryKey", sourceEntryKey),
                ("$partOfSpeech", partOfSpeech),
                ("$isPhrase", isPhrase ? 1 : 0));
        }

        private static void AddEnglishSynset(
            SqliteConnection connection,
            string synsetId,
            string partOfSpeech,
            string definition,
            string? example)
        {
            Execute(connection,
                "INSERT INTO Synsets (SynsetId, PartOfSpeech) VALUES ($synsetId, $partOfSpeech);",
                ("$synsetId", synsetId),
                ("$partOfSpeech", partOfSpeech));
            Execute(connection,
                "INSERT INTO Definitions (SynsetId, DefinitionIndex, Text) VALUES ($synsetId, 0, $text);",
                ("$synsetId", synsetId),
                ("$text", definition));
            if (example is not null)
            {
                Execute(connection,
                    "INSERT INTO Examples (SynsetId, ExampleIndex, Text) VALUES ($synsetId, 0, $text);",
                    ("$synsetId", synsetId),
                    ("$text", example));
            }
        }

        private static void AddEnglishSense(
            SqliteConnection connection,
            string senseId,
            string lexemeId,
            string synsetId,
            int senseOrder)
        {
            Execute(connection,
                "INSERT INTO Senses (SenseId, LexemeId, SynsetId, SenseOrder) VALUES ($senseId, $lexemeId, $synsetId, $senseOrder);",
                ("$senseId", senseId),
                ("$lexemeId", lexemeId),
                ("$synsetId", synsetId),
                ("$senseOrder", senseOrder));
        }

        private static void AddEnglishForm(
            SqliteConnection connection,
            string lexemeId,
            string surface,
            string formKind,
            string formSourceKind)
        {
            Execute(connection,
                "INSERT INTO Forms (LexemeId, Surface, FormKind, FormSourceKind) VALUES ($lexemeId, $surface, $formKind, $formSourceKind);",
                ("$lexemeId", lexemeId),
                ("$surface", surface),
                ("$formKind", formKind),
                ("$formSourceKind", formSourceKind));
        }

        private static void AddEnglishDerivation(
            SqliteConnection connection,
            string sourceSenseId,
            string targetSenseId)
        {
            Execute(connection,
                "INSERT INTO Derivations (SourceSenseId, TargetSenseId) VALUES ($sourceSenseId, $targetSenseId);",
                ("$sourceSenseId", sourceSenseId),
                ("$targetSenseId", targetSenseId));
        }

        private static void AddEnglishLearningSense(
            SqliteConnection connection,
            string learningSenseId,
            string sourceLexemeId,
            string headword,
            string partOfSpeech,
            bool isPhrase,
            string meaningSource,
            string meaningPrecision,
            int sortOrder,
            IReadOnlyList<string> sourceSenseIds,
            IReadOnlyList<string> meanings)
        {
            Execute(connection, """
                INSERT INTO LearningSenses (
                    LearningSenseId, SourceLexemeId, Headword, PartOfSpeech, IsPhrase,
                    MeaningSource, MeaningPrecision, SortOrder)
                VALUES ($learningSenseId, $sourceLexemeId, $headword, $partOfSpeech, $isPhrase,
                    $meaningSource, $meaningPrecision, $sortOrder);
                """,
                ("$learningSenseId", learningSenseId),
                ("$sourceLexemeId", sourceLexemeId),
                ("$headword", headword),
                ("$partOfSpeech", partOfSpeech),
                ("$isPhrase", isPhrase ? 1 : 0),
                ("$meaningSource", meaningSource),
                ("$meaningPrecision", meaningPrecision),
                ("$sortOrder", sortOrder));

            foreach (var sourceSenseId in sourceSenseIds)
            {
                Execute(connection,
                    "INSERT INTO LearningSenseSources (LearningSenseId, SenseId) VALUES ($learningSenseId, $senseId);",
                    ("$learningSenseId", learningSenseId),
                    ("$senseId", sourceSenseId));
            }

            for (var index = 0; index < meanings.Count; index++)
            {
                Execute(connection,
                    "INSERT INTO LearningMeanings (LearningSenseId, MeaningIndex, Text) VALUES ($learningSenseId, $meaningIndex, $text);",
                    ("$learningSenseId", learningSenseId),
                    ("$meaningIndex", index),
                    ("$text", meanings[index]));
            }
        }

        private static void Execute(
            SqliteConnection connection,
            string sql,
            params (string Name, object? Value)[] parameters)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
            }

            command.ExecuteNonQuery();
        }
    }
}
