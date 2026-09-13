using Dictation.DataAccess.Runtime;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
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
            services.AddSingleton<WordGroupMemberSnapshotReader>();
            services.AddSingleton(serviceProvider =>
                new ImportInboxDataAccessor(
                    serviceProvider.GetRequiredService<SqliteConnectionFactory>(),
                    serviceProvider.GetRequiredService<WordGroupMemberSnapshotReader>()));
            services.AddSingleton(serviceProvider =>
                new LearningPlanDataAccessor(
                    serviceProvider.GetRequiredService<SqliteConnectionFactory>(),
                    serviceProvider.GetRequiredService<WordGroupMemberSnapshotReader>()));
            services.AddSingleton(serviceProvider =>
                new LearningSessionDataAccessor(
                    serviceProvider.GetRequiredService<SqliteConnectionFactory>(),
                    serviceProvider.GetRequiredService<WordGroupMemberSnapshotReader>()));
            services.AddSingleton<SchemaMigrationRunner>(serviceProvider =>
                new SchemaMigrationRunner(
                    serviceProvider.GetRequiredService<ApplicationPaths>(),
                    serviceProvider.GetRequiredService<SqliteConnectionFactory>()));
            return services;
        }
    }
}
