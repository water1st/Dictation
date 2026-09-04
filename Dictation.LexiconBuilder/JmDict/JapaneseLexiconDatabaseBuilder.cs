using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

using Dictation.LexiconBuilder.JmDict;
using Microsoft.Data.Sqlite;

namespace Dictation.LexiconBuilder
{
    internal sealed class JapaneseLexiconDatabaseBuilder
    {
        internal const string SchemaVersion = "2";

        internal JapaneseLexiconBuildSummary Build(BuilderOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.SourceKind != LexiconSourceKind.JmDict)
            {
                throw new ArgumentException("日语构建器需要 JMdict 来源。", nameof(options));
            }

            var inputPath = Path.GetFullPath(options.SourcePath);
            var tomoshiPath = Path.GetFullPath(
                options.TomoshiDatabasePath
                    ?? throw new ArgumentException("日语构建器需要 Tomoshi 中文词库。", nameof(options)));
            var outputPath = Path.GetFullPath(options.OutputPath);
            ValidatePaths(inputPath, tomoshiPath, outputPath);
            var inputSha256 = ComputeSha256(inputPath);
            var tomoshiSha256 = ComputeSha256(tomoshiPath);
            var outputDirectory = Path.GetDirectoryName(outputPath)!;
            var temporaryPath = Path.Combine(
                outputDirectory,
                $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (File.Create(temporaryPath))
                {
                }

                var summary = BuildTemporaryDatabase(
                    inputPath,
                    tomoshiPath,
                    temporaryPath,
                    inputSha256,
                    tomoshiSha256,
                    outputPath);
                File.Move(temporaryPath, outputPath, overwrite: true);
                return summary;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static JapaneseLexiconBuildSummary BuildTemporaryDatabase(
            string inputPath,
            string tomoshiPath,
            string temporaryPath,
            string inputSha256,
            string tomoshiSha256,
            string outputPath)
        {
            var parser = new JmDictParser();
            var generator = new JapaneseFormGenerator();
            var chineseGlossIndex = TomoshiChineseGlossIndex.Load(tomoshiPath);
            var sourceSequences = new HashSet<int>();
            var entryCount = 0;
            var formCount = 0;
            var glossCount = 0;
            var chineseMeaningCount = 0;
            var missingChineseEntryCount = 0;

            using var connection = new SqliteConnection($"Data Source={temporaryPath};Mode=ReadWriteCreate;Pooling=False");
            connection.Open();
            ExecuteNonQuery(connection, null, "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA foreign_keys=ON;");
            using var transaction = connection.BeginTransaction();
            CreateSchema(connection, transaction);

            using (var input = File.OpenRead(inputPath))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false))
            {
                foreach (var entry in parser.Parse(gzip))
                {
                    sourceSequences.Add(entry.Sequence);
                    foreach (var sense in entry.Senses)
                    {
                        var entryId = $"jmdict:{entry.Sequence}:{sense.Index}";
                        var forms = generator.Generate(entry, sense);
                        InsertEntry(connection, transaction, entryId, entry, sense, forms);
                        InsertPartsOfSpeech(connection, transaction, entryId, sense.PartsOfSpeech);
                        foreach (var gloss in sense.Glosses.Select((value, index) => (value, index)))
                        {
                            InsertSourceGloss(connection, transaction, entryId, gloss.value, gloss.index);
                            glossCount++;
                        }

                        var chineseMeanings = chineseGlossIndex.GetMeanings(entry.Sequence, sense.Index);
                        for (var index = 0; index < chineseMeanings.Count; index++)
                        {
                            InsertChineseMeaning(
                                connection,
                                transaction,
                                entryId,
                                index,
                                chineseMeanings[index]);
                            chineseMeaningCount++;
                        }

                        if (chineseMeanings.Count == 0)
                        {
                            missingChineseEntryCount++;
                        }

                        foreach (var form in forms)
                        {
                            InsertForm(connection, transaction, entryId, form);
                            formCount++;
                        }

                        entryCount++;
                    }
                }
            }

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schema_version"] = SchemaVersion,
                ["input_sha256"] = inputSha256,
                ["jmdict_sha256"] = inputSha256,
                ["tomoshi_sha256"] = tomoshiSha256,
                ["jmdict_created"] = parser.CreatedDate,
                ["generator_version"] = typeof(JapaneseLexiconDatabaseBuilder).Assembly.GetName().Version?.ToString() ?? "unknown",
                ["source_entries"] = sourceSequences.Count.ToString(CultureInfo.InvariantCulture),
                ["entries"] = entryCount.ToString(CultureInfo.InvariantCulture),
                ["senses"] = entryCount.ToString(CultureInfo.InvariantCulture),
                ["forms"] = formCount.ToString(CultureInfo.InvariantCulture),
                ["glosses"] = glossCount.ToString(CultureInfo.InvariantCulture),
                ["source_glosses"] = glossCount.ToString(CultureInfo.InvariantCulture),
                ["chinese_meanings"] = chineseMeaningCount.ToString(CultureInfo.InvariantCulture),
                ["missing_chinese_entries"] = missingChineseEntryCount.ToString(CultureInfo.InvariantCulture)
            };
            InsertMetadata(connection, transaction, metadata);
            transaction.Commit();

            return new JapaneseLexiconBuildSummary(
                sourceSequences.Count,
                entryCount,
                entryCount,
                formCount,
                glossCount,
                chineseMeaningCount,
                missingChineseEntryCount,
                inputSha256,
                tomoshiSha256,
                parser.CreatedDate,
                outputPath);
        }

        private static void ValidatePaths(string inputPath, string tomoshiPath, string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("JMdict 文件不存在。", inputPath);
            }

            if (!File.Exists(tomoshiPath))
            {
                throw new FileNotFoundException("Tomoshi 中文词库不存在。", tomoshiPath);
            }

            if (Directory.Exists(outputPath))
            {
                throw new ArgumentException("输出路径不能是目录。", nameof(outputPath));
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                throw new DirectoryNotFoundException("输出目录不存在，不会自动创建目录。" + outputDirectory);
            }

            if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("JMdict 输入和 SQLite 输出不能是同一个文件。", nameof(outputPath));
            }

            if (string.Equals(tomoshiPath, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Tomoshi 输入和 SQLite 输出不能是同一个文件。", nameof(outputPath));
            }
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        private static void CreateSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            ExecuteNonQuery(connection, transaction, """
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
                    KanjiUsage TEXT NOT NULL CHECK (KanjiUsage IN ('None', 'Common', 'Rare')),
                    IsPhrase INTEGER NOT NULL CHECK (IsPhrase IN (0, 1)),
                    UNIQUE (SourceSequence, SenseIndex)
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
                    MeaningSource TEXT NOT NULL CHECK (MeaningSource = 'Tomoshi'),
                    MeaningPrecision TEXT NOT NULL CHECK (MeaningPrecision = 'Exact'),
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
                    IsCommon INTEGER NOT NULL CHECK (IsCommon IN (0, 1)),
                    IsPrimary INTEGER NOT NULL CHECK (IsPrimary IN (0, 1)),
                    UNIQUE (EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole),
                    FOREIGN KEY (EntryId) REFERENCES Entries (EntryId) ON DELETE CASCADE
                );
                CREATE INDEX IX_Forms_Surface ON Forms (Surface);
                """);
        }

        private static void InsertEntry(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string entryId,
            JmDictEntry entry,
            JmDictSense sense,
            IReadOnlyList<JapaneseLexiconForm> forms)
        {
            var primary = forms.FirstOrDefault(form => form.IsPrimary && form.ScriptKind == JapaneseScriptKind.Kanji)
                ?? forms.FirstOrDefault(form => form.IsPrimary)
                ?? forms.FirstOrDefault();
            var primaryWritten = primary?.Surface ?? entry.Orthographies.FirstOrDefault()?.Text ?? entry.Readings[0].Text;
            var primaryReading = primary?.Reading ?? entry.Readings[0].Text;
            var classification = JapaneseLexicalClassifier.Classify(sense.PartsOfSpeech);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Entries (
                    EntryId, SourceSequence, SenseIndex, PrimaryWritten, PrimaryReading,
                    LexicalCategory, VerbClass, Transitivity, KanjiUsage, IsPhrase)
                VALUES ($entryId, $sourceSequence, $senseIndex, $primaryWritten, $primaryReading,
                    $lexicalCategory, $verbClass, $transitivity, $kanjiUsage, $isPhrase);
                """;
            command.Parameters.AddWithValue("$entryId", entryId);
            command.Parameters.AddWithValue("$sourceSequence", entry.Sequence);
            command.Parameters.AddWithValue("$senseIndex", sense.Index);
            command.Parameters.AddWithValue("$primaryWritten", primaryWritten);
            command.Parameters.AddWithValue("$primaryReading", primaryReading);
            command.Parameters.AddWithValue("$lexicalCategory", classification.Category.ToString());
            command.Parameters.AddWithValue("$verbClass", classification.VerbClass.ToString());
            command.Parameters.AddWithValue("$transitivity", GetTransitivity(sense.PartsOfSpeech));
            command.Parameters.AddWithValue("$kanjiUsage", GetKanjiUsage(forms));
            command.Parameters.AddWithValue("$isPhrase", classification.Category == JapaneseLexicalCategory.Phrase ? 1 : 0);
            command.ExecuteNonQuery();
        }

        private static void InsertPartsOfSpeech(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string entryId,
            IReadOnlyList<string> partsOfSpeech)
        {
            for (var index = 0; index < partsOfSpeech.Count; index++)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO PartsOfSpeech (EntryId, SortIndex, Value) VALUES ($entryId, $sortIndex, $value);";
                command.Parameters.AddWithValue("$entryId", entryId);
                command.Parameters.AddWithValue("$sortIndex", index);
                command.Parameters.AddWithValue("$value", partsOfSpeech[index]);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertSourceGloss(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string entryId,
            JmDictGloss gloss,
            int glossIndex)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO SourceGlosses (EntryId, Language, GlossIndex, GlossType, Text) VALUES ($entryId, $language, $glossIndex, $glossType, $text);";
            command.Parameters.AddWithValue("$entryId", entryId);
            command.Parameters.AddWithValue("$language", gloss.Language);
            command.Parameters.AddWithValue("$glossIndex", glossIndex);
            command.Parameters.AddWithValue("$glossType", (object?)gloss.Type ?? DBNull.Value);
            command.Parameters.AddWithValue("$text", gloss.Text);
            command.ExecuteNonQuery();
        }

        private static void InsertChineseMeaning(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string entryId,
            int meaningIndex,
            string text)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO ChineseMeanings (
                    EntryId, MeaningIndex, Text, MeaningSource, MeaningPrecision)
                VALUES ($entryId, $meaningIndex, $text, 'Tomoshi', 'Exact');
                """;
            command.Parameters.AddWithValue("$entryId", entryId);
            command.Parameters.AddWithValue("$meaningIndex", meaningIndex);
            command.Parameters.AddWithValue("$text", text);
            command.ExecuteNonQuery();
        }

        private static void InsertForm(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string entryId,
            JapaneseLexiconForm form)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Forms (EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole, IsCommon, IsPrimary)
                VALUES ($entryId, $surface, $reading, $formKind, $scriptKind, $lexicalRole, $isCommon, $isPrimary);
                """;
            command.Parameters.AddWithValue("$entryId", entryId);
            command.Parameters.AddWithValue("$surface", form.Surface);
            command.Parameters.AddWithValue("$reading", form.Reading);
            command.Parameters.AddWithValue("$formKind", form.FormKind.ToString());
            command.Parameters.AddWithValue("$scriptKind", form.ScriptKind.ToString());
            command.Parameters.AddWithValue("$lexicalRole", form.LexicalRole.ToString());
            command.Parameters.AddWithValue("$isCommon", form.IsCommon ? 1 : 0);
            command.Parameters.AddWithValue("$isPrimary", form.IsPrimary ? 1 : 0);
            command.ExecuteNonQuery();
        }

        private static void InsertMetadata(
            SqliteConnection connection,
            SqliteTransaction transaction,
            IReadOnlyDictionary<string, string> metadata)
        {
            foreach (var item in metadata)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO BuildMetadata (Key, Value) VALUES ($key, $value);";
                command.Parameters.AddWithValue("$key", item.Key);
                command.Parameters.AddWithValue("$value", item.Value);
                command.ExecuteNonQuery();
            }
        }

        private static string GetTransitivity(IReadOnlyList<string> partsOfSpeech)
        {
            var transitive = partsOfSpeech.Any(part => part.Contains("transitive", StringComparison.OrdinalIgnoreCase)
                && !part.Contains("intransitive", StringComparison.OrdinalIgnoreCase));
            var intransitive = partsOfSpeech.Any(part => part.Contains("intransitive", StringComparison.OrdinalIgnoreCase));
            return (transitive, intransitive) switch
            {
                (true, true) => "Transitive;Intransitive",
                (true, false) => "Transitive",
                (false, true) => "Intransitive",
                _ => string.Empty
            };
        }

        private static string GetKanjiUsage(IReadOnlyList<JapaneseLexiconForm> forms)
        {
            var kanjiForms = forms.Where(form => form.ScriptKind == JapaneseScriptKind.Kanji).ToArray();
            if (kanjiForms.Length == 0)
            {
                return "None";
            }

            return kanjiForms.Any(form => form.IsCommon) ? "Common" : "Rare";
        }

        private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction? transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
