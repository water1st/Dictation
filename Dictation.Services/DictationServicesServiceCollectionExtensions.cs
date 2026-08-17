using Dictation.Services.Configuration;
using Dictation.Services.SingleInstance;
using Dictation.Services.Startup;
using Dictation.Services.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Services
{
    public static class DictationServicesServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddSingleton<SettingsService>();
            services.AddSingleton<StartupInitializer>();
            services.AddSingleton<SingleInstanceCoordinator>();
            services.AddSingleton<TtsService>();
            return services;
        }
    }
}
