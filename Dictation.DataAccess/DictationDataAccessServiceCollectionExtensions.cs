using Dictation.DataAccess.Runtime;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
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
            services.AddSingleton<SqliteConnectionFactory>();
            services.AddSingleton<LexiconSqliteConnectionFactory>();
            services.AddSingleton<JapaneseLexiconDataAccessor>();
            services.AddSingleton<EnglishLexiconDataAccessor>();
            services.AddSingleton<ImportInboxDataAccessor>();
            services.AddSingleton<SchemaMigrationRunner>(serviceProvider =>
                new SchemaMigrationRunner(
                    serviceProvider.GetRequiredService<ApplicationPaths>(),
                    serviceProvider.GetRequiredService<SqliteConnectionFactory>()));
            return services;
        }
    }
}
