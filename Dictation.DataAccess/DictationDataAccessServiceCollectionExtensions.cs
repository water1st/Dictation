using Dictation.DataAccess.Configuration;
using Dictation.DataAccess.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.DataAccess
{
    public static class DictationDataAccessServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationDataAccess(
            this IServiceCollection services,
            string applicationRootDirectory)
        {
            ArgumentNullException.ThrowIfNull(services);

            var applicationPaths = new ApplicationPaths(applicationRootDirectory);
            services.AddSingleton(applicationPaths);
            services.AddSingleton<SettingsDataAccessor>();
            return services;
        }
    }
}
