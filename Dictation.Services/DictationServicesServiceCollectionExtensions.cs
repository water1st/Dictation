using Microsoft.Extensions.DependencyInjection;
using Dictation.Services.Configuration;
using Dictation.Services.Startup;

namespace Dictation.Services
{
    public static class DictationServicesServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddSingleton<SettingsService>();
            services.AddSingleton<StartupInitializer>();
            return services;
        }
    }
}
