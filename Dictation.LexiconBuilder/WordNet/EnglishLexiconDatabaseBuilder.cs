using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using Dictation.LexiconBuilder;
using Microsoft.Data.Sqlite;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 将 WordNet、中文来源与词形写入英语词库并记录构建元数据。
    /// </summary>
    internal sealed class EnglishLexiconDatabaseBuilder
    {
        internal const string SchemaVersion = "2";

        /// <summary>
        /// 按构建选项读取来源并写入词库数据库，返回构建统计与来源校验信息。
        /// </summary>
        /// <param name="options">包含来源、语言和输出路径的已解析构建选项。</param>
        /// <returns>含实际输出统计和来源哈希的构建结果。</returns>
        internal EnglishLexiconBuildSummary Build(BuilderOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.SourceKind != LexiconSourceKind.WordNet)
            {
                throw new ArgumentException("英语构建器需要 Open English WordNet 来源。", nameof(options));
            }

            var sourceRoot = Path.GetFullPath(options.SourcePath);
            var cowPath = Path.GetFullPath(
                options.ChineseWordNetXmlPath
                    ?? throw new ArgumentException("英语构建器需要 Chinese Open WordNet。", nameof(options)));
            var ecdictPath = Path.GetFullPath(
                options.EcdictDatabasePath
                    ?? throw new ArgumentException("英语构建器需要 ECDICT。", nameof(options)));
            var outputPath = Path.GetFullPath(options.OutputPath);
            ValidatePaths(sourceRoot, cowPath, ecdictPath, outputPath);
            var sourceFiles = WordNetSourceFiles.Enumerate(sourceRoot);
            var sourceFingerprint = WordNetSourceFiles.ComputeFingerprint(sourceFiles);
            var cowSha256 = ComputeSha256(cowPath);
            var ecdictSha256 = ComputeSha256(ecdictPath);
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
                    cowPath,
                    ecdictPath,
                    temporaryPath,
                    sourceFingerprint,
                    cowSha256,
                    ecdictSha256,
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
            string cowPath,
            string ecdictPath,
            string temporaryPath,
            string sourceFingerprint,
            string cowSha256,
            string ecdictSha256,
            string outputPath)
        {
            var lexicon = new WordNetYamlParser().ParseDirectory(sourceRoot);
            var chineseMeaningsByIli = new ChineseOpenWordNetParser().ParseByIli(cowPath);
            using var ecdict = new EcdictTranslationReader(ecdictPath);
            var learningSenses = new EnglishLearningSenseBuilder().Build(lexicon, chineseMeaningsByIli, ecdict);
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

            foreach (var learningSense in learningSenses.OrderBy(item => item.SortOrder))
            {
                InsertLearningSense(connection, transaction, learningSense);
                foreach (var sourceSenseId in learningSense.SourceSenseIds)
                {
                    InsertLearningSenseSource(
                        connection,
                        transaction,
                        learningSense.LearningSenseId,
                        sourceSenseId);
                }

                for (var index = 0; index < learningSense.Meanings.Count; index++)
                {
                    InsertLearningMeaning(
                        connection,
                        transaction,
                        learningSense.LearningSenseId,
                        index,
                        learningSense.Meanings[index]);
                }
            }

            var senseCount = lexicon.Lexemes.Sum(lexeme => lexeme.Senses.Count);
            var definitionCount = lexicon.Synsets.Sum(synset => synset.Definitions.Count);
            var exampleCount = lexicon.Synsets.Sum(synset => synset.Examples.Count);
            var exactLearningSenseCount = learningSenses.Count(item => item.MeaningPrecision == "Exact");
            var broadLearningSenseCount = learningSenses.Count(item => item.MeaningPrecision == "Broad");
            var missingLearningSenseCount = learningSenses.Count(item => item.MeaningPrecision == "Missing");
            var oewnIlis = lexicon.Synsets
                .Select(synset => synset.Ili)
                .Where(ili => !string.IsNullOrWhiteSpace(ili))
                .Select(ili => ili!)
                .ToHashSet(StringComparer.Ordinal);
            var exactIliCount = oewnIlis.Count(ili =>
                chineseMeaningsByIli.TryGetValue(ili, out var meanings)
                && meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning)));
            var exactIliCoverage = oewnIlis.Count == 0
                ? 0m
                : decimal.Round(exactIliCount * 100m / oewnIlis.Count, 2, MidpointRounding.AwayFromZero);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schema_version"] = SchemaVersion,
                ["generator_version"] = typeof(EnglishLexiconDatabaseBuilder).Assembly.GetName().Version?.ToString() ?? "unknown",
                ["source_name"] = "Open English WordNet",
                ["source_fingerprint_sha256"] = sourceFingerprint,
                ["cow_sha256"] = cowSha256,
                ["ecdict_sha256"] = ecdictSha256,
                ["source_files"] = sourceFiles.Count.ToString(CultureInfo.InvariantCulture),
                ["lexemes"] = lexicon.Lexemes.Count.ToString(CultureInfo.InvariantCulture),
                ["senses"] = senseCount.ToString(CultureInfo.InvariantCulture),
                ["synsets"] = lexicon.Synsets.Count.ToString(CultureInfo.InvariantCulture),
                ["definitions"] = definitionCount.ToString(CultureInfo.InvariantCulture),
                ["examples"] = exampleCount.ToString(CultureInfo.InvariantCulture),
                ["forms"] = formCount.ToString(CultureInfo.InvariantCulture),
                ["derivations"] = derivationCount.ToString(CultureInfo.InvariantCulture),
                ["learning_senses"] = learningSenses.Count.ToString(CultureInfo.InvariantCulture),
                ["exact_learning_senses"] = exactLearningSenseCount.ToString(CultureInfo.InvariantCulture),
                ["broad_learning_senses"] = broadLearningSenseCount.ToString(CultureInfo.InvariantCulture),
                ["missing_learning_senses"] = missingLearningSenseCount.ToString(CultureInfo.InvariantCulture),
                ["oewn_ili_count"] = oewnIlis.Count.ToString(CultureInfo.InvariantCulture),
                ["cow_exact_ili_count"] = exactIliCount.ToString(CultureInfo.InvariantCulture),
                ["exact_ili_coverage_percent"] = exactIliCoverage.ToString("F2", CultureInfo.InvariantCulture),
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
                learningSenses.Count,
                exactLearningSenseCount,
                broadLearningSenseCount,
                missingLearningSenseCount,
                0,
                sourceFingerprint,
                cowSha256,
                ecdictSha256,
                outputPath);
        }

        private static void ValidatePaths(
            string sourceRoot,
            string cowPath,
            string ecdictPath,
            string outputPath)
        {
            if (!Directory.Exists(sourceRoot))
            {
                throw new DirectoryNotFoundException($"WordNet 输入目录不存在：{sourceRoot}。");
            }

            if (!File.Exists(cowPath))
            {
                throw new FileNotFoundException($"Chinese Open WordNet 文件不存在：{cowPath}。", cowPath);
            }

            if (!File.Exists(ecdictPath))
            {
                throw new FileNotFoundException($"ECDICT 数据库不存在：{ecdictPath}。", ecdictPath);
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

            if (string.Equals(outputPath, cowPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputPath, ecdictPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("输出数据库不能覆盖中文来源文件。", nameof(outputPath));
            }
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
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
                    IsPhrase INTEGER NOT NULL CHECK (IsPhrase IN (0, 1)),
                    MeaningSource TEXT NOT NULL CHECK (MeaningSource IN ('ChineseOpenWordNet', 'Ecdict', 'Missing')),
                    MeaningPrecision TEXT NOT NULL CHECK (MeaningPrecision IN ('Exact', 'Broad', 'Missing')),
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
                CREATE INDEX IX_Lexemes_Lemma ON Lexemes (Lemma);
                CREATE INDEX IX_Forms_Surface ON Forms (Surface);
                CREATE INDEX IX_Senses_SynsetId ON Senses (SynsetId);
                CREATE INDEX IX_Derivations_TargetSenseId ON Derivations (TargetSenseId);
                CREATE INDEX IX_LearningSenses_Headword ON LearningSenses (Headword, PartOfSpeech, SortOrder);
                CREATE INDEX IX_LearningSenseSources_SenseId ON LearningSenseSources (SenseId);
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
            command.CommandText = "INSERT INTO Synsets (SynsetId, PartOfSpeech, Ili) VALUES ($synsetId, $partOfSpeech, $ili);";
            command.Parameters.AddWithValue("$synsetId", synset.SynsetId);
            command.Parameters.AddWithValue("$partOfSpeech", ToCode(synset.PartOfSpeech));
            command.Parameters.AddWithValue("$ili", (object?)synset.Ili ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        private static void InsertLearningSense(
            SqliteConnection connection,
            SqliteTransaction transaction,
            EnglishLearningSense learningSense)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO LearningSenses (
                    LearningSenseId, SourceLexemeId, Headword, PartOfSpeech, IsPhrase,
                    MeaningSource, MeaningPrecision, SortOrder)
                VALUES (
                    $learningSenseId, $sourceLexemeId, $headword, $partOfSpeech, $isPhrase,
                    $meaningSource, $meaningPrecision, $sortOrder);
                """;
            command.Parameters.AddWithValue("$learningSenseId", learningSense.LearningSenseId);
            command.Parameters.AddWithValue("$sourceLexemeId", learningSense.SourceLexemeId);
            command.Parameters.AddWithValue("$headword", learningSense.Headword);
            command.Parameters.AddWithValue("$partOfSpeech", ToCode(learningSense.PartOfSpeech));
            command.Parameters.AddWithValue("$isPhrase", learningSense.IsPhrase ? 1 : 0);
            command.Parameters.AddWithValue("$meaningSource", learningSense.MeaningSource);
            command.Parameters.AddWithValue("$meaningPrecision", learningSense.MeaningPrecision);
            command.Parameters.AddWithValue("$sortOrder", learningSense.SortOrder);
            command.ExecuteNonQuery();
        }

        private static void InsertLearningSenseSource(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string learningSenseId,
            string senseId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO LearningSenseSources (LearningSenseId, SenseId) VALUES ($learningSenseId, $senseId);";
            command.Parameters.AddWithValue("$learningSenseId", learningSenseId);
            command.Parameters.AddWithValue("$senseId", senseId);
            command.ExecuteNonQuery();
        }

        private static void InsertLearningMeaning(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string learningSenseId,
            int meaningIndex,
            string text)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO LearningMeanings (LearningSenseId, MeaningIndex, Text) VALUES ($learningSenseId, $meaningIndex, $text);";
            command.Parameters.AddWithValue("$learningSenseId", learningSenseId);
            command.Parameters.AddWithValue("$meaningIndex", meaningIndex);
            command.Parameters.AddWithValue("$text", text);
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
