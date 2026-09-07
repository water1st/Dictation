using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Core.SingleInstance;
using Dictation.Core.Startup;
using Dictation.Core.Tts;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DictationCoreServiceCollectionExtensions
    {
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
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(Random.Shared);
            services.AddSingleton<LearningPlanService>();
            services.AddSingleton<LearningSessionService>();
            services.AddSingleton<AnswerEvaluationService>();
            return services;
        }
    }
}
