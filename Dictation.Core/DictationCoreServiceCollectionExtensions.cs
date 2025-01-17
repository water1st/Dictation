using Dictation.Core;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DictationCoreServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationCore(this IServiceCollection services)
        {
            services.AddSingleton<WordCollection>();
            services.AddSingleton<WordDrawingCollection>();

            services.AddTransient<SystemTTSPlayer>();
            services.AddTransient<JapaneseEdgeTTSPlayer>();
            services.AddTransient<DictationManager>();
            services.AddTransient<TTSFactory>();

            return services;
        }
    }
}
