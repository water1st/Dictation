using Dapper;
using Dictation.Core;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>用真实 SQLite 验证批量导入不会截断精确义项或混入前缀结果。</summary>
    public sealed class TextImportPaginationTests
    {
        [Theory]
        [InlineData(70)]
        [InlineData(100)]
        public async Task Prepare_MoreThanOnePageKeepsEveryExactSenseAndLastCanBeStaged(int count)
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var paths = provider.GetRequiredService<ApplicationPaths>();
            using (var connection = new SqliteConnection($"Data Source={paths.JapaneseLexiconDatabase};Pooling=False"))
            {
                connection.Open();
                for (var index = 0; index <= count; index++)
                {
                    var id = $"jmdict:{1000 + index}:1";
                    var surface = index < count ? "こう" : "こうじ";
                    connection.Execute("""
                        INSERT INTO Entries
                        SELECT @Id, @Sequence, 1, @Surface, @Surface, LexicalCategory,
                               VerbClass, Transitivity, KanjiUsage, IsPhrase
                        FROM Entries WHERE EntryId = 'jmdict:2:1';
                        INSERT INTO PartsOfSpeech SELECT @Id, SortIndex, Value
                        FROM PartsOfSpeech WHERE EntryId = 'jmdict:2:1';
                        INSERT INTO ChineseMeanings
                        VALUES (@Id, 0, @Meaning, 'Tomoshi', 'Exact');
                        INSERT INTO Forms (EntryId, Surface, Reading, FormKind, ScriptKind, LexicalRole, IsCommon, IsPrimary)
                        VALUES (@Id, @Surface, @Surface, 'Dictionary', 'Kana', 'Noun', 1, 1);
                        """, new { Id = id, Sequence = 1000 + index, Surface = surface, Meaning = $"中文义项 {index}" });
                }
            }

            var prepared = await provider.GetRequiredService<ITextImportPreparation>().PrepareAsync(
                LearningLanguage.Japanese, "こう", TestContext.Current.CancellationToken);
            var line = Assert.Single(prepared.Lines);

            Assert.Equal(count, line.Candidates.Count);
            Assert.Equal(count, line.Candidates.Select(x => x.SourceSenseId).Distinct().Count());
            Assert.DoesNotContain(line.Candidates, x => x.Headword == "こうじ");
            var last = line.Candidates[^1];
            Assert.Equal($"jmdict:{999 + count}:1", last.SourceSenseId);
            var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(
                new StageImportBatchRequestDto(LearningLanguage.Japanese, ImportSourceKind.Text,
                    new[] { new ImportSelectionDto(line.SourceText, last.SourceSenseId) }),
                TestContext.Current.CancellationToken);
            Assert.Equal(1, staged.AddedCount);
            Assert.Equal(last.SourceSenseId, Assert.Single(staged.Batch.Items).AnchorSourceSenseId);
            Assert.Contains($"中文义项 {count - 1}", Assert.Single(staged.Batch.Items).WordGroup!.Members[0].Meanings);
        }

        [Fact]
        public async Task Prepare_EnglishExactMatchIgnoresCaseButNotPrefix()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var paths = provider.GetRequiredService<ApplicationPaths>();
            using (var connection = new SqliteConnection($"Data Source={paths.EnglishLexiconDatabase};Pooling=False"))
            {
                connection.Open();
                connection.Execute("UPDATE Forms SET Surface = 'Go' WHERE Surface = 'go';");
            }

            var prepared = await provider.GetRequiredService<ITextImportPreparation>().PrepareAsync(
                LearningLanguage.English, "GO", TestContext.Current.CancellationToken);

            Assert.Contains(Assert.Single(prepared.Lines).Candidates, x => x.SourceSenseId == "go%2:01:00::");
            Assert.DoesNotContain(prepared.Lines[0].Candidates, x => x.Headword == "gone");
        }

        private static async Task<ServiceProvider> CreateProviderAsync(string root)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(root).AddDictationCore();
            var provider = services.BuildServiceProvider();
            var paths = provider.GetRequiredService<ApplicationPaths>();
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await provider.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(TestContext.Current.CancellationToken);
            return provider;
        }
    }
}
