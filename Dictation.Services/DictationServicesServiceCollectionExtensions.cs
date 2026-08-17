using Microsoft.Extensions.DependencyInjection;
using Dictation.Services.Configuration;

namespace Dictation.Services
{
    public static class DictationServicesServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddSingleton<SettingsService>();
            return services;
        }
    }
}
