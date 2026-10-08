using Dictation.Core.Practice;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Core.Review;
using Dictation.Core.SingleInstance;
using Dictation.Core.Startup;
using Dictation.Core.Tts;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// 提供 Dictation Core 业务服务及其能力别名的依赖注入注册入口。
    /// </summary>
    public static class DictationCoreServiceCollectionExtensions
    {
        /// <summary>
        /// 注册 Core 层服务；词库检索与整组解析能力共享同一个词库服务单例。
        /// </summary>
        /// <param name="services">应用程序唯一组合根使用的服务集合。</param>
        /// <returns>完成 Core 业务服务注册后的原服务集合。</returns>
        public static IServiceCollection AddDictationCore(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddSingleton<SettingsService>();
            services.AddSingleton<StartupInitializer>();
            services.AddSingleton<SingleInstanceCoordinator>();
            services.AddSingleton<TtsService>();
            services.AddSingleton<LexiconLookupService>();
            services.AddSingleton<ILexiconSearch>(serviceProvider =>
                serviceProvider.GetRequiredService<LexiconLookupService>());
            services.AddSingleton<ILexiconImportGroupResolver>(serviceProvider =>
                serviceProvider.GetRequiredService<LexiconLookupService>());
            services.AddSingleton<ImportInboxService>();
            services.AddSingleton<IImportStaging>(serviceProvider =>
                serviceProvider.GetRequiredService<ImportInboxService>());
            services.AddSingleton<IImportInboxQuery>(serviceProvider =>
                serviceProvider.GetRequiredService<ImportInboxService>());
            services.AddSingleton<IImportInboxActions>(serviceProvider =>
                serviceProvider.GetRequiredService<ImportInboxService>());
            services.AddSingleton<ITextImportPreparation, TextImportPreparationService>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(Random.Shared);
            services.AddSingleton<PracticePlanOptionValidator>();
            services.AddSingleton<EnglishExampleParadigmResolver>();
            services.AddSingleton<LearningPlanService>();
            services.AddSingleton<ILearningPlanReader>(serviceProvider =>
                serviceProvider.GetRequiredService<LearningPlanService>());
            services.AddSingleton<ILearningPlanEditor>(serviceProvider =>
                serviceProvider.GetRequiredService<LearningPlanService>());
            services.AddSingleton<ILearningLibraryManager>(serviceProvider =>
                serviceProvider.GetRequiredService<LearningPlanService>());
            services.AddSingleton<ILearningSessionStartPreparer>(serviceProvider =>
                serviceProvider.GetRequiredService<LearningPlanService>());
            services.AddSingleton<PracticeQuestionScheduler>();
            services.AddSingleton<LearningSessionService>();
            services.AddSingleton<ILearningSessionOperations>(serviceProvider =>
                serviceProvider.GetRequiredService<LearningSessionService>());
            services.AddSingleton<ILearningSessionReader>(serviceProvider =>
                serviceProvider.GetRequiredService<LearningSessionService>());
            services.AddSingleton<LearningProgressService>();
            services.AddSingleton<ILearningProgressReader>(serviceProvider =>
                serviceProvider.GetRequiredService<LearningProgressService>());
            services.AddSingleton<AnswerEvaluationService>();
            services.AddSingleton<FsrsScheduler>();
            services.AddSingleton<LearningMemorySeedPlanner>();
            services.AddSingleton<IReviewLibraryQuery, ReviewLibraryService>();
            services.AddSingleton<ReviewPlanService>();
            services.AddSingleton<IReviewPlanReader>(provider => provider.GetRequiredService<ReviewPlanService>());
            services.AddSingleton<IReviewPlanEditor>(provider => provider.GetRequiredService<ReviewPlanService>());
            services.AddSingleton<IReviewLibraryManager>(provider => provider.GetRequiredService<ReviewPlanService>());
            services.AddSingleton<ReviewSessionSelectionPlanner>();
            services.AddSingleton<IReviewQuestionReader, ReviewQuestionService>();
            services.AddSingleton<IReviewSessionAnswerer, ReviewAnswerService>();
            services.AddSingleton<ReviewMemoryUpdatePlanner>();
            services.AddSingleton<ReviewSessionService>();
            services.AddSingleton<IReviewSessionStarter>(provider => provider.GetRequiredService<ReviewSessionService>());
            services.AddSingleton<IReviewSessionReader>(provider => provider.GetRequiredService<ReviewSessionService>());
            return services;
        }
    }
}
