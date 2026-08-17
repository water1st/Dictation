using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

using Microsoft.Data.Sqlite;

using Dictation.LexiconBuilder;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.LexiconBuilder
{

    public sealed class JapaneseLexiconDatabaseBuilderTests
    {
        [Fact]
        public async Task Build_WritesStableSenseRowsFormsAndMetadata()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputPath = Path.Combine(temporaryDirectory.Path, "JMdict.gz");
            var outputPath = Path.Combine(temporaryDirectory.Path, "JapaneseLexicon.db");
            await WriteGzipAsync(inputPath, """
                <!-- JMdict created: 2026-01-02 -->
                <!DOCTYPE JMdict [<!ENTITY v1 "Ichidan verb"><!ENTITY v5k "Godan verb with 'ku' ending"><!ENTITY n "Common noun">]>
                <JMdict>
                  <entry><ent_seq>12345</ent_seq><k_ele><keb>食べる</keb><ke_pri>ichi1</ke_pri></k_ele><r_ele><reb>たべる</reb><re_pri>ichi1</re_pri></r_ele><sense><pos>&v1;</pos><gloss g_type="verb">to eat</gloss></sense></entry>
                  <entry><ent_seq>12346</ent_seq><k_ele><keb>書く</keb></k_ele><r_ele><reb>かく</reb></r_ele><sense><pos>&v5k;</pos><gloss>to write</gloss></sense><sense><gloss>to draw</gloss></sense></entry>
                </JMdict>
                """);

            var summary = new JapaneseLexiconDatabaseBuilder().Build(new BuilderOptions(inputPath, outputPath));

            Assert.True(File.Exists(outputPath));
            Assert.Equal(2, summary.SourceEntryCount);
            Assert.Equal(3, summary.EntryCount);
            Assert.Equal(3, summary.SenseCount);
            Assert.True(summary.FormCount >= 8);
            await using var connection = new SqliteConnection($"Data Source={outputPath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            Assert.Equal(
                new[] { "jmdict:12345:1", "jmdict:12346:1", "jmdict:12346:2" },
                await ReadStringsAsync(connection, "SELECT EntryId FROM Entries ORDER BY EntryId;"));
            Assert.Equal("Ichidan", await ReadScalarAsync(connection, "SELECT VerbClass FROM Entries WHERE EntryId = 'jmdict:12345:1';"));
            Assert.Equal("GodanK", await ReadScalarAsync(connection, "SELECT VerbClass FROM Entries WHERE EntryId = 'jmdict:12346:1';"));
            Assert.Equal("to draw", await ReadScalarAsync(connection, "SELECT Text FROM Glosses WHERE EntryId = 'jmdict:12346:2';"));
            Assert.Equal("verb", await ReadScalarAsync(connection, "SELECT GlossType FROM Glosses WHERE EntryId = 'jmdict:12345:1';"));
            Assert.Equal("Ichidan verb", await ReadScalarAsync(connection, "SELECT Value FROM PartsOfSpeech WHERE EntryId = 'jmdict:12345:1' AND SortIndex = 0;"));
            Assert.Equal("jmdict:12345:1", await ReadScalarAsync(connection, "SELECT EntryId FROM Forms WHERE Surface = '食べます' LIMIT 1;"));
            Assert.Equal("jmdict:12345:1", await ReadScalarAsync(connection, "SELECT EntryId FROM Forms WHERE Surface = '食べて' LIMIT 1;"));
            Assert.Equal("jmdict:12345:1", await ReadScalarAsync(connection, "SELECT EntryId FROM Forms WHERE Surface = '食べた' LIMIT 1;"));
            Assert.Equal("2026-01-02", await ReadScalarAsync(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'jmdict_created';"));
            Assert.Equal("2", await ReadScalarAsync(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'source_entries';"));
            Assert.Equal("3", await ReadScalarAsync(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'entries';"));
            Assert.Equal("3", await ReadScalarAsync(connection, "SELECT Value FROM BuildMetadata WHERE Key = 'senses';"));
            Assert.Equal("1", await ReadScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Forms_Surface';"));
        }

        [Fact]
        public async Task Build_MarksRareAndNoKanjiWithoutDroppingForms()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputPath = Path.Combine(temporaryDirectory.Path, "JMdict.gz");
            var outputPath = Path.Combine(temporaryDirectory.Path, "JapaneseLexicon.db");
            await WriteGzipAsync(inputPath, """
                <JMdict>
                  <entry><ent_seq>1</ent_seq><k_ele><keb>稀語</keb></k_ele><r_ele><reb>きご</reb></r_ele><sense><pos>Common noun</pos><gloss>rare word</gloss></sense></entry>
                  <entry><ent_seq>2</ent_seq><r_ele><reb>かな</reb><re_nokanji>nokanji</re_nokanji></r_ele><sense><pos>Common noun</pos><gloss>kana only</gloss></sense></entry>
                </JMdict>
                """);

            new JapaneseLexiconDatabaseBuilder().Build(new BuilderOptions(inputPath, outputPath));

            await using var connection = new SqliteConnection($"Data Source={outputPath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Rare", await ReadScalarAsync(connection, "SELECT KanjiUsage FROM Entries WHERE EntryId = 'jmdict:1:1';"));
            Assert.Equal("None", await ReadScalarAsync(connection, "SELECT KanjiUsage FROM Entries WHERE EntryId = 'jmdict:2:1';"));
            Assert.Equal("かな", await ReadScalarAsync(connection, "SELECT Surface FROM Forms WHERE EntryId = 'jmdict:2:1';"));
        }

        [Fact]
        public async Task Build_WhenInputFails_LeavesExistingOutputAndRemovesOwnTemp()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputPath = Path.Combine(temporaryDirectory.Path, "broken.gz");
            var outputPath = Path.Combine(temporaryDirectory.Path, "JapaneseLexicon.db");
            await File.WriteAllTextAsync(inputPath, "not gzip", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(outputPath, "existing database", TestContext.Current.CancellationToken);

            Assert.ThrowsAny<Exception>(() => new JapaneseLexiconDatabaseBuilder().Build(new BuilderOptions(inputPath, outputPath)));
            Assert.Equal("existing database", await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, "*.tmp"));
        }

        [Fact]
        public void Cli_RejectsInvalidArguments()
        {
            var argumentSets = new[]
            {
                new[] { "--jmdict", "--output" },
                new[] { "--jmdict", "x", "--jmdict", "y", "--output", "z" },
                new[] { "--unknown", "x", "--output", "z" }
            };

            foreach (var args in argumentSets)
            {
                var error = new StringWriter();
                Assert.Equal(2, LexiconBuilderCli.Run(args, TextWriter.Null, error));
                Assert.NotEmpty(error.ToString());
            }
        }

        [Fact]
        public async Task Cli_WhenInputIsMissingOrOutputDirectoryIsMissingReturnsTwo()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var missingInput = Path.Combine(temporaryDirectory.Path, "missing.gz");
            var validInput = Path.Combine(temporaryDirectory.Path, "valid.gz");
            var validOutput = Path.Combine(temporaryDirectory.Path, "JapaneseLexicon.db");
            await WriteGzipAsync(validInput, "<JMdict />");
            var missingInputError = new StringWriter();
            Assert.Equal(
                2,
                LexiconBuilderCli.Run(
                    new[] { "--jmdict", missingInput, "--output", validOutput },
                    TextWriter.Null,
                    missingInputError));

            var missingDirectoryError = new StringWriter();
            Assert.Equal(
                2,
                LexiconBuilderCli.Run(
                    new[] { "--jmdict", validInput, "--output", Path.Combine(temporaryDirectory.Path, "missing", "out.db") },
                    TextWriter.Null,
                    missingDirectoryError));
            Assert.NotEmpty(missingInputError.ToString());
            Assert.NotEmpty(missingDirectoryError.ToString());
        }

        [Fact]
        public async Task Cli_WhenGzipIsCorruptedReturnsTwoAndDoesNotCreateOutput()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputPath = Path.Combine(temporaryDirectory.Path, "broken.gz");
            var outputPath = Path.Combine(temporaryDirectory.Path, "JapaneseLexicon.db");
            await File.WriteAllTextAsync(inputPath, "not gzip", TestContext.Current.CancellationToken);
            var error = new StringWriter();

            Assert.Equal(
                2,
                LexiconBuilderCli.Run(
                    new[] { "--jmdict", inputPath, "--output", outputPath },
                    TextWriter.Null,
                    error));
            Assert.False(File.Exists(outputPath));
            Assert.NotEmpty(error.ToString());
        }

        [Fact]
        public async Task Cli_RejectsSameExistingInputAndOutputPath()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var samePath = Path.Combine(temporaryDirectory.Path, "same.gz");
            await WriteGzipAsync(samePath, "<JMdict />");
            var error = new StringWriter();

            Assert.Equal(
                2,
                LexiconBuilderCli.Run(
                    new[] { "--jmdict", samePath, "--output", samePath },
                    TextWriter.Null,
                    error));
            Assert.Contains("同一个文件", error.ToString());
        }

        [Fact]
        public async Task Build_DropsNonEnglishOnlySensesAndKeepsCanonicalGlosses()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputPath = Path.Combine(temporaryDirectory.Path, "JMdict.gz");
            var outputPath = Path.Combine(temporaryDirectory.Path, "JapaneseLexicon.db");
            await WriteGzipAsync(inputPath, """
                <JMdict><entry>
                  <ent_seq>700</ent_seq><r_ele><reb>たべる</reb></r_ele>
                  <sense><pos>Ichidan verb</pos><gloss>to eat</gloss><gloss xml:lang="dut">eten</gloss></sense>
                  <sense><gloss xml:lang="ger">essen</gloss></sense>
                </entry></JMdict>
                """);

            var summary = new JapaneseLexiconDatabaseBuilder().Build(new BuilderOptions(inputPath, outputPath));

            Assert.Equal(1, summary.SourceEntryCount);
            Assert.Equal(1, summary.EntryCount);
            await using var connection = new SqliteConnection($"Data Source={outputPath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("1", await ReadScalarAsync(connection, "SELECT COUNT(*) FROM Entries;"));
            Assert.Equal("2", await ReadScalarAsync(connection, "SELECT COUNT(*) FROM Glosses;"));
        }

        [Fact]
        public async Task Build_StoresSuruNounRolesAndOriginalPartsOfSpeech()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var inputPath = Path.Combine(temporaryDirectory.Path, "JMdict.gz");
            var outputPath = Path.Combine(temporaryDirectory.Path, "JapaneseLexicon.db");
            await WriteGzipAsync(inputPath, """
                <JMdict><entry>
                  <ent_seq>1399790</ent_seq><k_ele><keb>掃除</keb></k_ele><r_ele><reb>そうじ</reb></r_ele>
                  <sense><pos>noun (common) (futsuumeishi)</pos><pos>noun or participle which takes the aux. verb suru</pos><pos>transitive verb</pos><gloss>cleaning</gloss></sense>
                </entry></JMdict>
                """);

            new JapaneseLexiconDatabaseBuilder().Build(new BuilderOptions(inputPath, outputPath));

            await using var connection = new SqliteConnection($"Data Source={outputPath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Equal("NounVerb", await ReadScalarAsync(connection, "SELECT LexicalCategory FROM Entries WHERE EntryId = 'jmdict:1399790:1';"));
            Assert.Equal("3", await ReadScalarAsync(connection, "SELECT COUNT(*) FROM PartsOfSpeech WHERE EntryId = 'jmdict:1399790:1';"));
            Assert.Equal("Noun", await ReadScalarAsync(connection, "SELECT LexicalRole FROM Forms WHERE EntryId = 'jmdict:1399790:1' AND Surface = '掃除' LIMIT 1;"));
            Assert.Equal("Verb", await ReadScalarAsync(connection, "SELECT LexicalRole FROM Forms WHERE EntryId = 'jmdict:1399790:1' AND Surface = '掃除する' LIMIT 1;"));
        }

        private static async Task WriteGzipAsync(string path, string content)
        {
            await using var file = File.Create(path);
            await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
            await using var writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(content);
        }

        private static async Task<string?> ReadScalarAsync(SqliteConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
        }

        private static async Task<string[]> ReadStringsAsync(SqliteConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                values.Add(reader.GetString(0));
            }

            return values.ToArray();
        }
    }
}
