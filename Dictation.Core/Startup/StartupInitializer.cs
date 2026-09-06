using Dictation.DataAccess.Runtime;

namespace Dictation.Core.Startup
{
    public sealed class StartupInitializer
    {
        private readonly ApplicationPaths applicationPaths;
        private readonly SchemaMigrationRunner schemaMigrationRunner;

        public StartupInitializer(
            ApplicationPaths applicationPaths,
            SchemaMigrationRunner schemaMigrationRunner)
        {
            this.applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
            this.schemaMigrationRunner = schemaMigrationRunner ?? throw new ArgumentNullException(nameof(schemaMigrationRunner));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            applicationPaths.EnsureRuntimeDirectories();
            await schemaMigrationRunner.MigrateAsync(cancellationToken);
        }
    }
}
