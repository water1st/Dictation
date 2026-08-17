using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dictation.LexiconBuilder;
using Dictation.LexiconBuilder.WordNet;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Dictation.Tests.LexiconBuilder
{
    public sealed class EnglishLexiconDatabaseBuilderTests
    {
        [Fact]
        public void Build_WritesLexemesSensesFormsDefinitionsExamplesAndDirectDerivations()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");

            var summary = new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, outputPath));

            Assert.True(File.Exists(outputPath));
            Assert.Equal(7, summary.LexemeCount);
            Assert.Equal(8, summary.SenseCount);
            Assert.Equal(8, summary.SynsetCount);
            Assert.Equal(19, summary.FormCount);
            Assert.Equal(8, summary.DefinitionCount);
            Assert.Equal(1, summary.ExampleCount);
            Assert.Equal(2, summary.DerivationCount);
            Assert.Equal(64, summary.SourceFingerprintSha256.Length);
            Assert.Equal(0, summary.UnclassifiedSourceFormCount);

            using var connection = OpenReadOnly(outputPath);
            Assert.Equal(
                new[] { "bank%n-1", "bank%n-2", "conversion%n", "convert%v", "go%n-1", "go%v", "take off%v" },
                ReadStrings(connection, "SELECT LexemeId FROM Lexemes ORDER BY LexemeId;"));
            Assert.Equal(
                new[] { "goes", "went", "gone", "going" },
                ReadStrings(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'go%v' AND FormKind <> 'Base' ORDER BY FormId;"));
            Assert.Equal(
                new[] { "Source", "Source", "Rule", "Rule" },
                ReadStrings(connection, "SELECT FormSourceKind FROM Forms WHERE LexemeId = 'go%v' AND Surface <> 'go' ORDER BY FormKind, Surface;"));
            Assert.Equal("goes", ReadScalar(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'go%n-1' AND FormKind = 'Plural';"));
            Assert.Equal("go", ReadScalar(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'go%v' AND FormKind = 'Base';"));
            Assert.Equal("take off", ReadScalar(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'take off%v' AND FormKind = 'Base';"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM Forms WHERE LexemeId = 'take off%v';"));
            Assert.Equal("to move", ReadScalar(connection, "SELECT Text FROM Definitions WHERE SynsetId = '00000001-v';"));
            Assert.Equal("go far", ReadScalar(connection, "SELECT Text FROM Examples WHERE SynsetId = '00000001-v';"));
            Assert.Equal("2", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations;"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations WHERE SourceSenseId = 'go%2:01:00::' AND TargetSenseId = 'convert%2:30:00::';"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations WHERE SourceSenseId = 'convert%2:30:00::' AND TargetSenseId = 'conversion%1:04:00::';"));
            Assert.Equal("0", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations WHERE SourceSenseId = 'go%2:01:00::' AND TargetSenseId = 'conversion%1:04:00::';"));
        }

        [Fact]
        public void Build_CreatesRequiredSchemaForeignKeysAndIndexes()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, outputPath));

            using var connection = OpenReadOnly(outputPath);
            Assert.Equal(
                new[] { "BuildMetadata", "Definitions", "Derivations", "Examples", "Forms", "Lexemes", "Senses", "Synsets" },
                ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'table' AND name <> 'sqlite_sequence' ORDER BY name;"));
            Assert.Equal(
                new[] { "IX_Derivations_TargetSenseId", "IX_Forms_Surface", "IX_Lexemes_Lemma", "IX_Senses_SynsetId" },
                ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'index' AND name LIKE 'IX_%' ORDER BY name;"));
            Assert.Equal(
                new[] { "Key", "Value" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('BuildMetadata') ORDER BY cid;"));
            Assert.Equal(
                new[] { "LexemeId", "Lemma", "SourceEntryKey", "PartOfSpeech", "IsPhrase" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Lexemes') ORDER BY cid;"));
            Assert.Equal(
                new[] { "SynsetId", "PartOfSpeech" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Synsets') ORDER BY cid;"));
            Assert.Equal(
                new[] { "SenseId", "LexemeId", "SynsetId", "SenseOrder" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Senses') ORDER BY cid;"));
            Assert.Equal(
                new[] { "SynsetId", "DefinitionIndex", "Text" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Definitions') ORDER BY cid;"));
            Assert.Equal(
                new[] { "SynsetId", "ExampleIndex", "Text" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Examples') ORDER BY cid;"));
            Assert.Equal(
                new[] { "FormId", "LexemeId", "Surface", "FormKind", "FormSourceKind" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Forms') ORDER BY cid;"));
            Assert.Equal(
                new[] { "SourceSenseId", "TargetSenseId" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Derivations') ORDER BY cid;"));
            Assert.Equal("2", ReadScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_list('Senses');"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_list('Forms');"));
            Assert.Equal("2", ReadScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_list('Derivations');"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM BuildMetadata WHERE Key = 'schema_version' AND Value = '1';"));
            Assert.Equal("Open English WordNet", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'source_name';"));
            Assert.Equal("0", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'unclassified_source_forms';"));
            Assert.Equal(64, ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'source_fingerprint_sha256';")!.Length);
        }

        [Fact]
        public void Build_FingerprintIsStableIgnoresFramesAndChangesForConsumedYaml()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            var options = new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, outputPath);

            new EnglishLexiconDatabaseBuilder().Build(options);
            var firstFingerprint = ReadMetadata(outputPath, "source_fingerprint_sha256");
            new EnglishLexiconDatabaseBuilder().Build(options);
            var secondFingerprint = ReadMetadata(outputPath, "source_fingerprint_sha256");
            Assert.Equal(firstFingerprint, secondFingerprint);

            File.WriteAllText(Path.Combine(sourcePath, "frames.yaml"), "this: is ignored");
            Directory.CreateDirectory(Path.Combine(sourcePath, ".git"));
            File.WriteAllText(Path.Combine(sourcePath, ".git", "entries-hidden.yaml"), "not: valid WordNet");
            new EnglishLexiconDatabaseBuilder().Build(options);
            Assert.Equal(firstFingerprint, ReadMetadata(outputPath, "source_fingerprint_sha256"));

            File.AppendAllText(Path.Combine(sourcePath, "entries-core.yaml"), Environment.NewLine);
            new EnglishLexiconDatabaseBuilder().Build(options);
            Assert.NotEqual(firstFingerprint, ReadMetadata(outputPath, "source_fingerprint_sha256"));
        }

        [Fact]
        public void Build_WhenYamlOrFormFails_PreservesExistingOutputAndRemovesTemporaryFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            File.WriteAllText(outputPath, "existing database");
            File.WriteAllText(Path.Combine(sourcePath, "entries-core.yaml"), "go: [");

            Assert.Throws<LexiconBuildException>(() => new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, outputPath)));

            Assert.Equal("existing database", File.ReadAllText(outputPath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, "*.tmp"));
        }

        [Fact]
        public void Build_WhenSourceFormCannotBeClassified_PreservesExistingOutputAndRemovesTemporaryFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            File.WriteAllText(outputPath, "existing database");
            var entriesPath = Path.Combine(sourcePath, "entries-core.yaml");
            File.WriteAllText(entriesPath, File.ReadAllText(entriesPath).Replace("- went", "- mystery", StringComparison.Ordinal));

            var exception = Assert.Throws<LexiconBuildException>(() => new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, outputPath)));

            Assert.Contains("mystery", exception.Message, StringComparison.Ordinal);
            Assert.Equal("existing database", File.ReadAllText(outputPath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, "*.tmp"));
        }

        [Fact]
        public void Build_RejectsOutputInsideSourceRoot()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(sourcePath, "EnglishLexicon.db");

            var exception = Assert.Throws<ArgumentException>(() => new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, outputPath)));

            Assert.Contains("WordNet", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Build_RejectsMissingRootMissingOutputParentAndDirectoryOutput()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var missingRoot = Path.Combine(temporaryDirectory.Path, "missing");
            var outputPath = Path.Combine(temporaryDirectory.Path, "out.db");
            Assert.Throws<DirectoryNotFoundException>(() => new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, missingRoot, outputPath)));

            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var missingParentOutput = Path.Combine(temporaryDirectory.Path, "missing-parent", "out.db");
            Assert.Throws<DirectoryNotFoundException>(() => new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, missingParentOutput)));

            var directoryOutput = Path.Combine(temporaryDirectory.Path, "output-directory");
            Directory.CreateDirectory(directoryOutput);
            Assert.Throws<ArgumentException>(() => new EnglishLexiconDatabaseBuilder().Build(
                new BuilderOptions(LexiconSourceKind.WordNet, sourcePath, directoryOutput)));
        }

        [Fact]
        public void Cli_BuildsWordNetAndPrintsChineseSummary()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            var output = new StringWriter();

            var result = LexiconBuilderCli.Run(
                new[] { "--wordnet", sourcePath, "--output", outputPath },
                output,
                new StringWriter());

            Assert.Equal(0, result);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("lexemes=7", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("source_fingerprint_sha256=", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("goes", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Cli_RejectsMissingOrMultipleSourcesAndDuplicateOutput()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var sourcePath = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            var argumentSets = new[]
            {
                new[] { "--output", outputPath },
                new[] { "--wordnet", sourcePath },
                new[] { "--wordnet", sourcePath, "--jmdict", "jmdict.gz", "--output", outputPath },
                new[] { "--wordnet", sourcePath, "--wordnet", sourcePath, "--output", outputPath },
                new[] { "--wordnet", sourcePath, "--output", outputPath, "--output", outputPath }
            };

            foreach (var args in argumentSets)
            {
                var error = new StringWriter();
                Assert.Equal(2, LexiconBuilderCli.Run(args, TextWriter.Null, error));
                Assert.NotEmpty(error.ToString());
            }
        }

        private static string CreateFixture(string parentPath)
        {
            var root = Path.Combine(parentPath, "english-wordnet");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "entries-core.yaml"), """
                bank:
                  n-1:
                    sense:
                    - id: 'bank%1:14:00::'
                      synset: 00000003-n
                  n-2:
                    sense:
                    - id: 'bank%1:17:00::'
                      synset: 00000004-n
                convert:
                  v:
                    form:
                    - converted
                    - converting
                    sense:
                    - id: 'convert%2:30:00::'
                      derivation:
                      - 'conversion%1:04:00::'
                      synset: 00000006-v
                conversion:
                  n:
                    form:
                    - conversions
                    sense:
                    - id: 'conversion%1:04:00::'
                      synset: 00000007-n
                go:
                  n-1:
                    form:
                    - goes
                    sense:
                    - id: 'go%1:15:00::'
                      synset: 00000005-n
                  v:
                    form:
                    - went
                    - gone
                    sense:
                    - id: 'go%2:01:00::'
                      derivation:
                      - 'convert%2:30:00::'
                      synset: 00000001-v
                    - id: 'go%2:02:00::'
                      synset: 00000002-v
                take off:
                  v:
                    sense:
                    - id: 'take_off%2:38:00::'
                      synset: 00000008-v
                """);
            File.WriteAllText(Path.Combine(root, "noun.1.yaml"), """
                00000003-n:
                  definition:
                  - a financial institution
                  partOfSpeech: n
                00000004-n:
                  definition:
                  - a bank of objects
                  partOfSpeech: n
                00000005-n:
                  definition:
                  - a departure
                  partOfSpeech: n
                00000007-n:
                  definition:
                  - an execution
                  partOfSpeech: n
                """);
            File.WriteAllText(Path.Combine(root, "verb.1.yaml"), """
                00000001-v:
                  definition:
                  - to move
                  example:
                  - go far
                  partOfSpeech: v
                00000002-v:
                  definition:
                  - to function
                  partOfSpeech: v
                00000006-v:
                  definition:
                  - to change
                  partOfSpeech: v
                00000008-v:
                  definition:
                  - to leave
                  partOfSpeech: v
                """);
            return root;
        }

        private static SqliteConnection OpenReadOnly(string path)
        {
            var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            connection.Open();
            return connection;
        }

        private static string ReadMetadata(string databasePath, string key)
        {
            using var connection = OpenReadOnly(databasePath);
            return ReadScalar(connection, $"SELECT Value FROM BuildMetadata WHERE Key = '{key}';")!;
        }

        private static string? ReadScalar(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar()?.ToString();
        }

        private static string[] ReadStrings(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var values = new List<string>();
            while (reader.Read())
            {
                values.Add(reader.GetString(0));
            }

            return values.ToArray();
        }
    }
}
