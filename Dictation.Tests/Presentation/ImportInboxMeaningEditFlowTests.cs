using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.DataAccess;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证收件箱详情的中文释义编辑能贯通业务服务和用户数据库。
    /// </summary>
    public sealed class ImportInboxMeaningEditFlowTests
    {
        [Fact]
        public async Task SaveAsync_PersistsChineseMeaningsAndAllowsLearningDecision()
        {
            using var directory = new TemporaryDirectory();
            var service = await CreateServiceAsync(directory);
            var staged = await StageMissingMeaningAsync(service);
            var item = Assert.Single(staged.Batch.Items);
            var viewModel = new ImportInboxDetailViewModel(service);
            viewModel.Initialize(staged.Batch, item.ItemId);
            var editor = Assert.Single(viewModel.Members);
            editor.ChineseMeaningDraft = "符号\n标记";

            await editor.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Contains("已保存", editor.SaveStatusMessage, StringComparison.Ordinal);
            var pending = Assert.Single(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            var updated = Assert.Single(Assert.Single(pending.Items).WordGroup!.Members);
            Assert.Equal(new[] { "符号", "标记" }, updated.Meanings);
            await service.DecideAsync(
                new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, ImportDecision.Learning),
                TestContext.Current.CancellationToken);
            Assert.Empty(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SaveAsync_RejectsBlankDraftWithoutChangingPendingGroup()
        {
            using var directory = new TemporaryDirectory();
            var service = await CreateServiceAsync(directory);
            var staged = await StageMissingMeaningAsync(service);
            var viewModel = new ImportInboxDetailViewModel(service);
            viewModel.Initialize(staged.Batch, Assert.Single(staged.Batch.Items).ItemId);
            var editor = Assert.Single(viewModel.Members);
            editor.ChineseMeaningDraft = " \r\n\t ";

            await editor.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Contains("至少", editor.SaveStatusMessage, StringComparison.Ordinal);
            var pending = Assert.Single(await service.GetPendingBatchesAsync(
                LearningLanguage.English,
                TestContext.Current.CancellationToken));
            Assert.Empty(Assert.Single(Assert.Single(pending.Items).WordGroup!.Members).Meanings);
        }

        [Fact]
        public async Task SaveAsync_ReportsDatabaseRejectionAndPreservesCancellation()
        {
            using var directory = new TemporaryDirectory();
            var service = await CreateServiceAsync(directory);
            var staged = await StageMissingMeaningAsync(service);
            var item = Assert.Single(staged.Batch.Items);
            var viewModel = new ImportInboxDetailViewModel(service);
            viewModel.Initialize(staged.Batch, item.ItemId);
            var editor = Assert.Single(viewModel.Members);
            editor.ChineseMeaningDraft = "符号";
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => editor.SaveAsync(cancellation.Token));
            await service.DecideAsync(
                new DecideImportBatchRequestDto(staged.Batch.BatchId, new[] { item.ItemId }, ImportDecision.Discard),
                TestContext.Current.CancellationToken);
            await editor.SaveAsync(TestContext.Current.CancellationToken);

            Assert.Contains("保存", editor.SaveStatusMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("已保存", editor.SaveStatusMessage, StringComparison.Ordinal);
        }

        private static async Task<ImportInboxService> CreateServiceAsync(TemporaryDirectory directory)
        {
            var paths = new ApplicationPaths(directory.Path);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            var connectionFactory = new SqliteConnectionFactory(paths);
            await new SchemaMigrationRunner(paths, connectionFactory)
                .MigrateAsync(TestContext.Current.CancellationToken);
            var lexiconFactory = new LexiconSqliteConnectionFactory(paths);
            return new ImportInboxService(
                new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(lexiconFactory),
                    new EnglishLexiconDataAccessor(lexiconFactory)),
                DataAccessorTestFactory.CreateImportInbox(connectionFactory));
        }

        private static Task<StageImportBatchResultDto> StageMissingMeaningAsync(ImportInboxService service)
        {
            return service.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("%", "missing:symbols:v") }),
                TestContext.Current.CancellationToken);
        }
    }
}
