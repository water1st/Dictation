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
    /// <summary>
    /// 验证英语数据库结构、构建统计、来源指纹和可重复构建。
    /// </summary>
    public sealed class EnglishLexiconDatabaseBuilderTests
    {
        [Fact]
        public void Build_WritesLexemesSensesFormsDefinitionsExamplesAndDirectDerivations()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");

            var summary = new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, outputPath));

            Assert.True(File.Exists(outputPath));
            Assert.Equal(10, summary.LexemeCount);
            Assert.Equal(11, summary.SenseCount);
            Assert.Equal(11, summary.SynsetCount);
            Assert.Equal(29, summary.FormCount);
            Assert.Equal(11, summary.DefinitionCount);
            Assert.Equal(1, summary.ExampleCount);
            Assert.Equal(2, summary.DerivationCount);
            Assert.Equal(64, summary.SourceFingerprintSha256.Length);
            Assert.Equal(0, summary.UnclassifiedSourceFormCount);
            Assert.Equal(11, summary.LearningSenseCount);
            Assert.Equal(2, summary.ExactLearningSenseCount);
            Assert.Equal(7, summary.BroadLearningSenseCount);
            Assert.Equal(2, summary.MissingLearningSenseCount);

            using var connection = OpenReadOnly(outputPath);
            Assert.Equal(
                new[] { "bank%n-1", "bank%n-2", "child%n", "conversion%n", "convert%v", "do%v", "go%n-1", "go%v", "good%a", "take off%v" },
                ReadStrings(connection, "SELECT LexemeId FROM Lexemes ORDER BY LexemeId;"));
            Assert.Equal(
                new[] { "goes", "went", "gone", "going" },
                ReadStrings(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'go%v' AND FormKind <> 'Base' ORDER BY FormId;"));
            Assert.Equal(
                new[] { "Source", "Source", "Rule", "Rule" },
                ReadStrings(connection, "SELECT FormSourceKind FROM Forms WHERE LexemeId = 'go%v' AND Surface <> 'go' ORDER BY FormKind, Surface;"));
            Assert.Equal("goes", ReadScalar(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'go%n-1' AND FormKind = 'Plural';"));
            Assert.Equal("go", ReadScalar(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'go%v' AND FormKind = 'Base';"));
            Assert.Equal(
                new[] { "do", "does", "did", "done", "doing" },
                ReadStrings(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'do%v' ORDER BY FormId;"));
            Assert.Equal(
                new[] { "child", "children" },
                ReadStrings(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'child%n' ORDER BY FormKind;"));
            Assert.Equal(
                new[] { "good", "better", "best" },
                ReadStrings(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'good%a' ORDER BY FormKind;"));
            Assert.Equal("take off", ReadScalar(connection, "SELECT Surface FROM Forms WHERE LexemeId = 'take off%v' AND FormKind = 'Base';"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM Forms WHERE LexemeId = 'take off%v';"));
            Assert.Equal("to move", ReadScalar(connection, "SELECT Text FROM Definitions WHERE SynsetId = '00000001-v';"));
            Assert.Equal("go far", ReadScalar(connection, "SELECT Text FROM Examples WHERE SynsetId = '00000001-v';"));
            Assert.Equal("2", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations;"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations WHERE SourceSenseId = 'go%2:01:00::' AND TargetSenseId = 'convert%2:30:00::';"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations WHERE SourceSenseId = 'convert%2:30:00::' AND TargetSenseId = 'conversion%1:04:00::';"));
            Assert.Equal("0", ReadScalar(connection, "SELECT COUNT(*) FROM Derivations WHERE SourceSenseId = 'go%2:01:00::' AND TargetSenseId = 'conversion%1:04:00::';"));
            Assert.Equal("i10001", ReadScalar(connection, "SELECT Ili FROM Synsets WHERE SynsetId = '00000001-v';"));
            Assert.Equal("前往", ReadScalar(connection, "SELECT Text FROM LearningMeanings WHERE LearningSenseId = 'go%2:01:00::';"));
            Assert.Equal("去；行走", ReadScalar(connection, "SELECT Text FROM LearningMeanings WHERE LearningSenseId = 'ecdict:go:v';"));
            Assert.Equal("0", ReadScalar(connection, "SELECT COUNT(*) FROM LearningMeanings WHERE Text = 'to move';"));
            Assert.Equal("11", ReadScalar(connection, "SELECT COUNT(*) FROM LearningSenseSources;"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM LearningSenseSources WHERE LearningSenseId = 'ecdict:go:v' AND SenseId = 'go%2:02:00::';"));
            Assert.Equal("Missing", ReadScalar(connection, "SELECT MeaningPrecision FROM LearningSenses WHERE LearningSenseId = 'missing:conversion:n';"));
            Assert.Equal(
                Enumerable.Range(0, 11).Select(value => value.ToString()).ToArray(),
                ReadStrings(connection, "SELECT SortOrder FROM LearningSenses ORDER BY SortOrder;"));
        }

        [Fact]
        public void Build_CreatesRequiredSchemaForeignKeysAndIndexes()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, outputPath));

            using var connection = OpenReadOnly(outputPath);
            Assert.Equal(
                new[] { "BuildMetadata", "Definitions", "Derivations", "Examples", "Forms", "LearningMeanings", "LearningSenseSources", "LearningSenses", "Lexemes", "Senses", "Synsets" },
                ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'table' AND name <> 'sqlite_sequence' ORDER BY name;"));
            Assert.Equal(
                new[] { "IX_Derivations_TargetSenseId", "IX_Forms_Surface", "IX_LearningSenseSources_SenseId", "IX_LearningSenses_Headword", "IX_Lexemes_Lemma", "IX_Senses_SynsetId" },
                ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'index' AND name LIKE 'IX_%' ORDER BY name;"));
            Assert.Equal(
                new[] { "Key", "Value" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('BuildMetadata') ORDER BY cid;"));
            Assert.Equal(
                new[] { "LexemeId", "Lemma", "SourceEntryKey", "PartOfSpeech", "IsPhrase" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('Lexemes') ORDER BY cid;"));
            Assert.Equal(
                new[] { "SynsetId", "PartOfSpeech", "Ili" },
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
            Assert.Equal(
                new[] { "LearningSenseId", "SourceLexemeId", "Headword", "PartOfSpeech", "IsPhrase", "MeaningSource", "MeaningPrecision", "SortOrder" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('LearningSenses') ORDER BY cid;"));
            Assert.Equal(
                new[] { "LearningSenseId", "SenseId" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('LearningSenseSources') ORDER BY cid;"));
            Assert.Equal(
                new[] { "LearningSenseId", "MeaningIndex", "Text" },
                ReadStrings(connection, "SELECT name FROM pragma_table_info('LearningMeanings') ORDER BY cid;"));
            Assert.Equal("2", ReadScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_list('Senses');"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_list('Forms');"));
            Assert.Equal("2", ReadScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_list('Derivations');"));
            Assert.Equal("1", ReadScalar(connection, "SELECT COUNT(*) FROM BuildMetadata WHERE Key = 'schema_version' AND Value = '2';"));
            Assert.Equal("Open English WordNet", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'source_name';"));
            Assert.Equal("0", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'unclassified_source_forms';"));
            Assert.Equal(64, ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'source_fingerprint_sha256';")!.Length);
            Assert.Equal(64, ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'cow_sha256';")!.Length);
            Assert.Equal(64, ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'ecdict_sha256';")!.Length);
            Assert.Equal("2", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'exact_learning_senses';"));
            Assert.Equal("7", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'broad_learning_senses';"));
            Assert.Equal("2", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'missing_learning_senses';"));
            Assert.Equal("2", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'cow_exact_ili_count';"));
            Assert.Equal("11", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'oewn_ili_count';"));
            Assert.Equal("18.18", ReadScalar(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'exact_ili_coverage_percent';"));
        }

        [Fact]
        public void Build_FingerprintIsStableIgnoresFramesAndChangesForConsumedYaml()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            var options = BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, outputPath);

            new EnglishLexiconDatabaseBuilder().Build(options);
            var firstFingerprint = ReadMetadata(outputPath, "source_fingerprint_sha256");
            new EnglishLexiconDatabaseBuilder().Build(options);
            var secondFingerprint = ReadMetadata(outputPath, "source_fingerprint_sha256");
            Assert.Equal(firstFingerprint, secondFingerprint);

            File.WriteAllText(Path.Combine(inputs.WordNetPath, "frames.yaml"), "this: is ignored");
            Directory.CreateDirectory(Path.Combine(inputs.WordNetPath, ".git"));
            File.WriteAllText(Path.Combine(inputs.WordNetPath, ".git", "entries-hidden.yaml"), "not: valid WordNet");
            new EnglishLexiconDatabaseBuilder().Build(options);
            Assert.Equal(firstFingerprint, ReadMetadata(outputPath, "source_fingerprint_sha256"));

            File.AppendAllText(Path.Combine(inputs.WordNetPath, "entries-core.yaml"), Environment.NewLine);
            new EnglishLexiconDatabaseBuilder().Build(options);
            Assert.NotEqual(firstFingerprint, ReadMetadata(outputPath, "source_fingerprint_sha256"));
        }

        [Fact]
        public void Build_WhenYamlOrFormFails_PreservesExistingOutputAndRemovesTemporaryFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            File.WriteAllText(outputPath, "existing database");
            File.WriteAllText(Path.Combine(inputs.WordNetPath, "entries-core.yaml"), "go: [");

            Assert.Throws<LexiconBuildException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, outputPath)));

            Assert.Equal("existing database", File.ReadAllText(outputPath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, "*.tmp"));
        }

        [Fact]
        public void Build_WhenSourceFormCannotBeClassified_PreservesExistingOutputAndRemovesTemporaryFile()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            File.WriteAllText(outputPath, "existing database");
            var entriesPath = Path.Combine(inputs.WordNetPath, "entries-core.yaml");
            File.WriteAllText(entriesPath, File.ReadAllText(entriesPath).Replace("- went", "- mystery", StringComparison.Ordinal));

            var exception = Assert.Throws<LexiconBuildException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, outputPath)));

            Assert.Contains("mystery", exception.Message, StringComparison.Ordinal);
            Assert.Equal("existing database", File.ReadAllText(outputPath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, "*.tmp"));
        }

        [Fact]
        public void Build_RejectsOutputInsideSourceRoot()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(inputs.WordNetPath, "EnglishLexicon.db");

            var exception = Assert.Throws<ArgumentException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, outputPath)));

            Assert.Contains("WordNet", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Build_RejectsMissingRootMissingOutputParentAndDirectoryOutput()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var missingRoot = Path.Combine(temporaryDirectory.Path, "missing");
            var outputPath = Path.Combine(temporaryDirectory.Path, "out.db");
            var inputs = CreateFixture(temporaryDirectory.Path);
            Assert.Throws<DirectoryNotFoundException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(missingRoot, inputs.CowPath, inputs.EcdictPath, outputPath)));

            var missingParentOutput = Path.Combine(temporaryDirectory.Path, "missing-parent", "out.db");
            Assert.Throws<DirectoryNotFoundException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, missingParentOutput)));

            var directoryOutput = Path.Combine(temporaryDirectory.Path, "output-directory");
            Directory.CreateDirectory(directoryOutput);
            Assert.Throws<ArgumentException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, inputs.EcdictPath, directoryOutput)));

            Assert.Throws<FileNotFoundException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, Path.Combine(temporaryDirectory.Path, "missing-cow.xml"), inputs.EcdictPath, outputPath)));
            Assert.Throws<FileNotFoundException>(() => new EnglishLexiconDatabaseBuilder().Build(
                BuilderOptions.ForEnglish(inputs.WordNetPath, inputs.CowPath, Path.Combine(temporaryDirectory.Path, "missing-ecdict.db"), outputPath)));
        }

        [Fact]
        public void Cli_BuildsWordNetAndPrintsChineseSummary()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            var output = new StringWriter();

            var result = LexiconBuilderCli.Run(
                new[] { "--wordnet", inputs.WordNetPath, "--cow", inputs.CowPath, "--ecdict", inputs.EcdictPath, "--output", outputPath },
                output,
                new StringWriter());

            Assert.Equal(0, result);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("lexemes=10", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("source_fingerprint_sha256=", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("exact=2", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("broad=7", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("missing=2", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("goes", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Cli_RejectsMissingOrMultipleSourcesAndDuplicateOutput()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputs = CreateFixture(temporaryDirectory.Path);
            var outputPath = Path.Combine(temporaryDirectory.Path, "EnglishLexicon.db");
            var argumentSets = new[]
            {
                new[] { "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--cow", inputs.CowPath, "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--ecdict", inputs.EcdictPath, "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--cow", inputs.CowPath, "--ecdict", inputs.EcdictPath, "--tomoshi", "tomoshi.db", "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--jmdict", "jmdict.gz", "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--wordnet", inputs.WordNetPath, "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--cow", inputs.CowPath, "--cow", inputs.CowPath, "--ecdict", inputs.EcdictPath, "--output", outputPath },
                new[] { "--wordnet", inputs.WordNetPath, "--cow", inputs.CowPath, "--ecdict", inputs.EcdictPath, "--output", outputPath, "--output", outputPath }
            };

            foreach (var args in argumentSets)
            {
                var error = new StringWriter();
                Assert.Equal(2, LexiconBuilderCli.Run(args, TextWriter.Null, error));
                Assert.NotEmpty(error.ToString());
            }
        }

        private static EnglishBuildInputs CreateFixture(string parentPath)
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
                child:
                  n:
                    form:
                    - children
                    sense:
                    - id: 'child%1:18:00::'
                      synset: 00000009-n
                do:
                  v:
                    form:
                    - did
                    - done
                    sense:
                    - id: 'do%2:41:00::'
                      synset: 00000010-v
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
                good:
                  a:
                    form:
                    - better
                    - best
                    sense:
                    - id: 'good%3:00:00::'
                      synset: 00000011-a
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
                  ili: i10003
                  partOfSpeech: n
                00000004-n:
                  definition:
                  - a bank of objects
                  ili: i10004
                  partOfSpeech: n
                00000005-n:
                  definition:
                  - a departure
                  ili: i10005
                  partOfSpeech: n
                00000007-n:
                  definition:
                  - an execution
                  ili: i10007
                  partOfSpeech: n
                00000009-n:
                  definition:
                  - a young person
                  ili: i10009
                  partOfSpeech: n
                """);
            File.WriteAllText(Path.Combine(root, "verb.1.yaml"), """
                00000001-v:
                  definition:
                  - to move
                  example:
                  - go far
                  ili: i10001
                  partOfSpeech: v
                00000002-v:
                  definition:
                  - to function
                  ili: i10002
                  partOfSpeech: v
                00000006-v:
                  definition:
                  - to change
                  ili: i10006
                  partOfSpeech: v
                00000008-v:
                  definition:
                  - to leave
                  ili: i10008
                  partOfSpeech: v
                00000010-v:
                  definition:
                  - to perform
                  ili: i10010
                  partOfSpeech: v
                """);
            File.WriteAllText(Path.Combine(root, "adj.1.yaml"), """
                00000011-a:
                  definition:
                  - having a positive quality
                  ili: i10011
                  partOfSpeech: a
                """);

            var cowPath = Path.Combine(parentPath, "omw-cmn.xml");
            File.WriteAllText(cowPath, """
                <LexicalResource>
                  <Lexicon id="test" language="cmn-Hans">
                    <LexicalEntry id="go"><Lemma writtenForm="前往" /><Sense id="cmn-go" synset="cmn-synset-go" /></LexicalEntry>
                    <LexicalEntry id="bank"><Lemma writtenForm="银行" /><Sense id="cmn-bank" synset="cmn-synset-bank" /></LexicalEntry>
                    <Synset id="cmn-synset-go" ili="i10001" members="cmn-go" />
                    <Synset id="cmn-synset-bank" ili="i10003" members="cmn-bank" />
                  </Lexicon>
                </LexicalResource>
                """);
            var ecdictPath = EcdictTranslationReaderTests.CreateDatabase(
                parentPath,
                new Dictionary<string, string>
                {
                    ["bank"] = "n. 银行；岸",
                    ["child"] = "n. 儿童；孩子",
                    ["convert"] = "v. 转换",
                    ["do"] = "v. 做；执行",
                    ["go"] = "v. 去；行走\nn. 围棋",
                    ["good"] = "adj. 好的；优良的"
                });
            return new EnglishBuildInputs(root, cowPath, ecdictPath);
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

        private sealed record EnglishBuildInputs(string WordNetPath, string CowPath, string EcdictPath);
    }
}
