using Dictation.DataAccess.Runtime;

namespace Dictation.Core.Startup
{
    /// <summary>
    /// 在业务启动前创建运行目录并完成用户数据库迁移。
    /// </summary>
    public sealed class StartupInitializer
    {
        private readonly ApplicationPaths applicationPaths;
        private readonly SchemaMigrationRunner schemaMigrationRunner;

        /// <summary>
        /// 在业务启动前创建运行目录并完成用户数据库迁移。
        /// </summary>
        /// <param name="applicationPaths">资源、运行数据和备份路径。</param>
        /// <param name="schemaMigrationRunner">负责用户数据库结构校验、备份及迁移的执行器。</param>
        public StartupInitializer(
            ApplicationPaths applicationPaths,
            SchemaMigrationRunner schemaMigrationRunner)
        {
            this.applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
            this.schemaMigrationRunner = schemaMigrationRunner ?? throw new ArgumentNullException(nameof(schemaMigrationRunner));
        }

        /// <summary>
        /// 先创建运行目录，再执行结构迁移；失败阻止业务启动。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            applicationPaths.EnsureRuntimeDirectories();
            await schemaMigrationRunner.MigrateAsync(cancellationToken);
        }
    }
}
