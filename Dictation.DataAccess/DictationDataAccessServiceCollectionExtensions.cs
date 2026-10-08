using Dictation.DataAccess.Runtime;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Review;
using Dictation.DataAccess.Mistakes;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.DataAccess
{
    /// <summary>
    /// 在唯一容器中注册持久化组件及共享快照读取实例。
    /// </summary>
    public static class DictationDataAccessServiceCollectionExtensions
    {
        /// <summary>
        /// 注册路径、连接工厂和单例 DAO；共享读取器由同一容器实例提供。
        /// </summary>
        /// <param name="services">应用唯一依赖注入容器的注册集合。</param>
        /// <param name="applicationRootDirectory">应用完全限定根目录。</param>
        /// <returns>原注册集合，以支持连续注册。</returns>
        public static IServiceCollection AddDictationDataAccess(
            this IServiceCollection services,
            string applicationRootDirectory)
        {
            ArgumentNullException.ThrowIfNull(services);

            var applicationPaths = new ApplicationPaths(applicationRootDirectory);
            services.AddSingleton(applicationPaths);
            services.AddSingleton<SqliteConnectionFactory>();
            services.AddSingleton<WrongAnswerDataAccessor>();
            services.AddSingleton<LexiconSqliteConnectionFactory>();
            services.AddSingleton<JapaneseLexiconDataAccessor>();
            services.AddSingleton<EnglishLexiconDataAccessor>();
            services.AddSingleton<WordGroupMemberSnapshotReader>();
            services.AddSingleton<ReviewMemoryWriter>();
            services.AddSingleton<ReviewQuestionWriter>();
            services.AddSingleton(provider => new ReviewQuestionDataAccessor(provider.GetRequiredService<SqliteConnectionFactory>(),
                provider.GetRequiredService<ReviewQuestionWriter>()));
            services.AddSingleton(provider => new ReviewAnswerDataAccessor(provider.GetRequiredService<SqliteConnectionFactory>(),
                provider.GetRequiredService<ReviewQuestionWriter>(), provider.GetRequiredService<ReviewMemoryWriter>()));
            services.AddSingleton(provider => new ReviewSessionDataAccessor(
                provider.GetRequiredService<SqliteConnectionFactory>(),
                provider.GetRequiredService<WordGroupMemberSnapshotReader>()));
            services.AddSingleton(provider => new ReviewPlanDataAccessor(provider.GetRequiredService<SqliteConnectionFactory>()));
            services.AddSingleton(serviceProvider => new ReviewLibraryDataAccessor(
                serviceProvider.GetRequiredService<SqliteConnectionFactory>(),
                serviceProvider.GetRequiredService<WordGroupMemberSnapshotReader>()));
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
                    serviceProvider.GetRequiredService<WordGroupMemberSnapshotReader>(),
                    serviceProvider.GetRequiredService<ReviewMemoryWriter>()));
            services.AddSingleton<SchemaMigrationRunner>(serviceProvider =>
                new SchemaMigrationRunner(
                    serviceProvider.GetRequiredService<ApplicationPaths>(),
                    serviceProvider.GetRequiredService<SqliteConnectionFactory>()));
            return services;
        }
    }
}
