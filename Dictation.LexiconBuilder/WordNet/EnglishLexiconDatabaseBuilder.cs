using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Dictation.LexiconBuilder;
using Microsoft.Data.Sqlite;

namespace Dictation.LexiconBuilder.WordNet
{
    internal sealed class EnglishLexiconDatabaseBuilder
    {
        internal const string SchemaVersion = "1";

        internal EnglishLexiconBuildSummary Build(BuilderOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.SourceKind != LexiconSourceKind.WordNet)
            {
                throw new ArgumentException("英语构建器需要 Open English WordNet 来源。", nameof(options));
            }

            var sourceRoot = Path.GetFullPath(options.SourcePath);
            var outputPath = Path.GetFullPath(options.OutputPath);
            ValidatePaths(sourceRoot, outputPath);
            var sourceFiles = WordNetSourceFiles.Enumerate(sourceRoot);
            var sourceFingerprint = WordNetSourceFiles.ComputeFingerprint(sourceFiles);
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
                    sourceRoot,
                    sourceFiles,
                    temporaryPath,
                    sourceFingerprint,
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

        private static EnglishLexiconBuildSummary BuildTemporaryDatabase(
            string sourceRoot,
            IReadOnlyList<WordNetSourceFile> sourceFiles,
            string temporaryPath,
            string sourceFingerprint,
            string outputPath)
        {
            var lexicon = new WordNetYamlParser().ParseDirectory(sourceRoot);
            var generator = new EnglishFormGenerator();

            using var connection = new SqliteConnection($"Data Source={temporaryPath};Mode=ReadWriteCreate;Pooling=False");
            connection.Open();
            ExecuteNonQuery(connection, null, "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA foreign_keys=ON;");
            using var transaction = connection.BeginTransaction();
            CreateSchema(connection, transaction);

            foreach (var synset in lexicon.Synsets.OrderBy(item => item.SynsetId, StringComparer.Ordinal))
            {
                InsertSynset(connection, transaction, synset);
                for (var index = 0; index < synset.Definitions.Count; index++)
                {
                    InsertDefinition(connection, transaction, synset.SynsetId, index, synset.Definitions[index]);
                }

                for (var index = 0; index < synset.Examples.Count; index++)
                {
                    InsertExample(connection, transaction, synset.SynsetId, index, synset.Examples[index]);
                }
            }

            var formCount = 0;
            foreach (var lexeme in lexicon.Lexemes.OrderBy(item => item.LexemeId, StringComparer.Ordinal))
            {
                InsertLexeme(connection, transaction, lexeme);
                foreach (var sense in lexeme.Senses.OrderBy(item => item.Order))
                {
                    InsertSense(connection, transaction, sense, lexeme.LexemeId);
                }

                var forms = generator.Generate(lexeme);
                foreach (var form in forms)
                {
                    InsertForm(connection, transaction, lexeme.LexemeId, form);
                    formCount++;
                }
            }

            var derivationCount = 0;
            foreach (var lexeme in lexicon.Lexemes.OrderBy(item => item.LexemeId, StringComparer.Ordinal))
            {
                foreach (var sense in lexeme.Senses.OrderBy(item => item.Order))
                {
                    foreach (var targetSenseId in sense.DerivationSenseIds.OrderBy(value => value, StringComparer.Ordinal))
                    {
                        InsertDerivation(connection, transaction, sense.SenseId, targetSenseId);
                        derivationCount++;
                    }

                }
            }

            var senseCount = lexicon.Lexemes.Sum(lexeme => lexeme.Senses.Count);
            var definitionCount = lexicon.Synsets.Sum(synset => synset.Definitions.Count);
            var exampleCount = lexicon.Synsets.Sum(synset => synset.Examples.Count);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schema_version"] = SchemaVersion,
                ["generator_version"] = typeof(EnglishLexiconDatabaseBuilder).Assembly.GetName().Version?.ToString() ?? "unknown",
                ["source_name"] = "Open English WordNet",
                ["source_fingerprint_sha256"] = sourceFingerprint,
                ["source_files"] = sourceFiles.Count.ToString(CultureInfo.InvariantCulture),
                ["lexemes"] = lexicon.Lexemes.Count.ToString(CultureInfo.InvariantCulture),
                ["senses"] = senseCount.ToString(CultureInfo.InvariantCulture),
                ["synsets"] = lexicon.Synsets.Count.ToString(CultureInfo.InvariantCulture),
                ["definitions"] = definitionCount.ToString(CultureInfo.InvariantCulture),
                ["examples"] = exampleCount.ToString(CultureInfo.InvariantCulture),
                ["forms"] = formCount.ToString(CultureInfo.InvariantCulture),
                ["derivations"] = derivationCount.ToString(CultureInfo.InvariantCulture),
                ["unclassified_source_forms"] = "0"
            };
            InsertMetadata(connection, transaction, metadata);
            transaction.Commit();

            return new EnglishLexiconBuildSummary(
                sourceFiles.Count,
                lexicon.Lexemes.Count,
                senseCount,
                lexicon.Synsets.Count,
                definitionCount,
                exampleCount,
                formCount,
                derivationCount,
                0,
                sourceFingerprint,
                outputPath);
        }

        private static void ValidatePaths(string sourceRoot, string outputPath)
        {
            if (!Directory.Exists(sourceRoot))
            {
                throw new DirectoryNotFoundException($"WordNet 输入目录不存在：{sourceRoot}。");
            }

            if (Directory.Exists(outputPath))
            {
                throw new ArgumentException("输出路径不能是目录。", nameof(outputPath));
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                throw new DirectoryNotFoundException($"输出目录不存在，不会自动创建目录：{outputDirectory}。");
            }

            if (IsInsideDirectory(sourceRoot, outputPath))
            {
                throw new ArgumentException("输出数据库不能放在 WordNet 输入目录内。", nameof(outputPath));
            }
        }

        private static bool IsInsideDirectory(string directoryPath, string candidatePath)
        {
            var relativePath = Path.GetRelativePath(directoryPath, candidatePath);
            if (relativePath == ".")
            {
                return true;
            }

            return !Path.IsPathRooted(relativePath)
                && relativePath != ".."
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static void CreateSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            ExecuteNonQuery(connection, transaction, """
                CREATE TABLE BuildMetadata (
                    Key TEXT NOT NULL PRIMARY KEY,
                    Value TEXT NOT NULL
                );
                CREATE TABLE Lexemes (
                    LexemeId TEXT NOT NULL PRIMARY KEY,
                    Lemma TEXT NOT NULL,
                    SourceEntryKey TEXT NOT NULL,
                    PartOfSpeech TEXT NOT NULL,
                    IsPhrase INTEGER NOT NULL CHECK (IsPhrase IN (0, 1)),
                    UNIQUE (Lemma, SourceEntryKey)
                );
                CREATE TABLE Synsets (
                    SynsetId TEXT NOT NULL PRIMARY KEY,
                    PartOfSpeech TEXT NOT NULL
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
                CREATE INDEX IX_Lexemes_Lemma ON Lexemes (Lemma);
                CREATE INDEX IX_Forms_Surface ON Forms (Surface);
                CREATE INDEX IX_Senses_SynsetId ON Senses (SynsetId);
                CREATE INDEX IX_Derivations_TargetSenseId ON Derivations (TargetSenseId);
                """);
        }

        private static void InsertLexeme(
            SqliteConnection connection,
            SqliteTransaction transaction,
            EnglishLexeme lexeme)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Lexemes (LexemeId, Lemma, SourceEntryKey, PartOfSpeech, IsPhrase)
                VALUES ($lexemeId, $lemma, $sourceEntryKey, $partOfSpeech, $isPhrase);
                """;
            command.Parameters.AddWithValue("$lexemeId", lexeme.LexemeId);
            command.Parameters.AddWithValue("$lemma", lexeme.Lemma);
            command.Parameters.AddWithValue("$sourceEntryKey", lexeme.SourceEntryKey);
            command.Parameters.AddWithValue("$partOfSpeech", ToCode(lexeme.PartOfSpeech));
            command.Parameters.AddWithValue("$isPhrase", lexeme.IsPhrase ? 1 : 0);
            command.ExecuteNonQuery();
        }

        private static void InsertSense(
            SqliteConnection connection,
            SqliteTransaction transaction,
            EnglishSense sense,
            string lexemeId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Senses (SenseId, LexemeId, SynsetId, SenseOrder)
                VALUES ($senseId, $lexemeId, $synsetId, $senseOrder);
                """;
            command.Parameters.AddWithValue("$senseId", sense.SenseId);
            command.Parameters.AddWithValue("$lexemeId", lexemeId);
            command.Parameters.AddWithValue("$synsetId", sense.SynsetId);
            command.Parameters.AddWithValue("$senseOrder", sense.Order);
            command.ExecuteNonQuery();
        }

        private static void InsertSynset(
            SqliteConnection connection,
            SqliteTransaction transaction,
            EnglishSynset synset)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Synsets (SynsetId, PartOfSpeech) VALUES ($synsetId, $partOfSpeech);";
            command.Parameters.AddWithValue("$synsetId", synset.SynsetId);
            command.Parameters.AddWithValue("$partOfSpeech", ToCode(synset.PartOfSpeech));
            command.ExecuteNonQuery();
        }

        private static void InsertDefinition(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string synsetId,
            int definitionIndex,
            string text)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Definitions (SynsetId, DefinitionIndex, Text) VALUES ($synsetId, $definitionIndex, $text);";
            command.Parameters.AddWithValue("$synsetId", synsetId);
            command.Parameters.AddWithValue("$definitionIndex", definitionIndex);
            command.Parameters.AddWithValue("$text", text);
            command.ExecuteNonQuery();
        }

        private static void InsertExample(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string synsetId,
            int exampleIndex,
            string text)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Examples (SynsetId, ExampleIndex, Text) VALUES ($synsetId, $exampleIndex, $text);";
            command.Parameters.AddWithValue("$synsetId", synsetId);
            command.Parameters.AddWithValue("$exampleIndex", exampleIndex);
            command.Parameters.AddWithValue("$text", text);
            command.ExecuteNonQuery();
        }

        private static void InsertForm(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string lexemeId,
            EnglishForm form)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Forms (LexemeId, Surface, FormKind, FormSourceKind)
                VALUES ($lexemeId, $surface, $formKind, $formSourceKind);
                """;
            command.Parameters.AddWithValue("$lexemeId", lexemeId);
            command.Parameters.AddWithValue("$surface", form.Surface);
            command.Parameters.AddWithValue("$formKind", form.Kind.ToString());
            command.Parameters.AddWithValue("$formSourceKind", form.SourceKind.ToString());
            command.ExecuteNonQuery();
        }

        private static void InsertDerivation(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sourceSenseId,
            string targetSenseId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Derivations (SourceSenseId, TargetSenseId) VALUES ($sourceSenseId, $targetSenseId);";
            command.Parameters.AddWithValue("$sourceSenseId", sourceSenseId);
            command.Parameters.AddWithValue("$targetSenseId", targetSenseId);
            command.ExecuteNonQuery();
        }

        private static void InsertMetadata(
            SqliteConnection connection,
            SqliteTransaction transaction,
            IReadOnlyDictionary<string, string> metadata)
        {
            foreach (var item in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO BuildMetadata (Key, Value) VALUES ($key, $value);";
                command.Parameters.AddWithValue("$key", item.Key);
                command.Parameters.AddWithValue("$value", item.Value);
                command.ExecuteNonQuery();
            }
        }

        private static void ExecuteNonQuery(
            SqliteConnection connection,
            SqliteTransaction? transaction,
            string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static string ToCode(EnglishPartOfSpeech partOfSpeech)
        {
            return partOfSpeech switch
            {
                EnglishPartOfSpeech.Noun => "n",
                EnglishPartOfSpeech.Verb => "v",
                EnglishPartOfSpeech.Adjective => "a",
                EnglishPartOfSpeech.AdjectiveSatellite => "s",
                EnglishPartOfSpeech.Adverb => "r",
                _ => "unknown"
            };
        }
    }
}
