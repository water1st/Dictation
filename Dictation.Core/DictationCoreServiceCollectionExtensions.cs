using Dictation.Core;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DictationCoreServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationCore(this IServiceCollection services)
        {
            services.AddSingleton<WordCollection>();
            services.AddSingleton<WordDrawingCollection>();


            services.AddKeyedTransient<ITTSPlayer, JapaneseTTSPlayer>("ja");
            services.AddKeyedTransient<ITTSPlayer, EnglishTTSPlayer>("en");
            services.AddTransient<DictationManager>();
            services.AddTransient<TTSFactory>();

            return services;
        }
    }
}
