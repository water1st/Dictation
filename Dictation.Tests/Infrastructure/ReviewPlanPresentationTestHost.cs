using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>为复习计划页面创建隔离的真实三层 DI 与 SQLite，不启动模型进程。</summary>
    internal static class ReviewPlanPresentationTestHost
    {
        internal static async Task<ServiceProvider> CreateAsync(string root, bool createLexicons = true, Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(root).AddDictationCore().AddDictationPresentation();
            configure?.Invoke(services);
            var provider = services.BuildServiceProvider();
            try
            {
                var paths = provider.GetRequiredService<ApplicationPaths>();
                paths.EnsureRuntimeDirectories();
                if (createLexicons) LexiconTestDatabases.CreateBoth(paths);
                await provider.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(TestContext.Current.CancellationToken);
                return provider;
            }
            catch { provider.Dispose(); throw; }
        }

        internal static async Task<string> ImportAsync(ServiceProvider provider, string word = "食べる", string sense = "jmdict:1:1", LearningLanguage language = LearningLanguage.Japanese)
        {
            var result = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(language,
                ImportSourceKind.Manual, new[] { new ImportSelectionDto(word, sense) }), TestContext.Current.CancellationToken);
            var item = Assert.Single(result.Batch.Items);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                result.Batch.BatchId, new[] { item.ItemId }, ImportDecision.Review), TestContext.Current.CancellationToken);
            return item.WordGroupId!;
        }
    }
}
