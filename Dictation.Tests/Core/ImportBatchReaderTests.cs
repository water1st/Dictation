using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Startup;
using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>通过同容器真实暂存和用户丢弃验证按批次回读，不只依赖Pending列表。</summary>
    public sealed class ImportBatchReaderTests
    {
        [Fact]
        public async Task PendingAndCompletedBatch_ShareServiceAndPreserveOriginalSnapshot()
        {
            var token = TestContext.Current.CancellationToken;
            using var directory = new TemporaryDirectory();
            var paths = new ApplicationPaths(directory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var services = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                .AddDictationDataAccess(paths.Root).AddDictationCore();
            using var provider = services.BuildServiceProvider();
            await provider.GetRequiredService<StartupInitializer>().InitializeAsync(token);
            var reader = provider.GetRequiredService<IImportBatchReader>();
            Assert.Same(reader, provider.GetRequiredService<IImportStaging>());
            Assert.Same(reader, provider.GetRequiredService<IImportInboxActions>());
            var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new(LearningLanguage.Japanese,
                ImportSourceKind.Mcp, new[] { new ImportSelectionDto("食べます", "jmdict:1:1") }), token);
            var batch = await reader.GetBatchAsync(staged.Batch.BatchId, token);
            Assert.Equal(ImportBatchStatus.Pending, batch.Status);
            Assert.Equal("吃", Assert.Single(batch.Items).WordGroup!.Members.Single().Meanings[0]);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new(batch.BatchId, batch.Items.Select(item => item.ItemId).ToArray(), ImportDecision.Discard), token);
            Assert.Empty(await provider.GetRequiredService<IImportInboxQuery>().GetPendingBatchesAsync(LearningLanguage.Japanese, token));
            Assert.Equal(ImportBatchStatus.Completed, (await reader.GetBatchAsync(batch.BatchId, token)).Status);
            await Assert.ThrowsAsync<ArgumentException>(() => reader.GetBatchAsync(" ", token));
            await Assert.ThrowsAsync<ImportInboxDataAccessException>(() => reader.GetBatchAsync("unknown", token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetBatchAsync(batch.BatchId, new CancellationToken(true)));
        }
    }
}
