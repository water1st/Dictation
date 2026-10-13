using Dictation.DataAccess.Runtime;

namespace Dictation.Core.Updating
{
    /// <summary>按现有发行安装的信息编排更新用例，禁止把开发目录当成可更新成品。</summary>
    internal sealed class UpdateService : IUpdateReleaseQuery
    {
        private readonly ApplicationPaths paths;
        private readonly GitHubUpdateProvider github;

        /// <summary>组合唯一根目录与官方发行来源。</summary>
        /// <param name="paths">已确定的应用根目录。</param>
        /// <param name="github">正式发行元数据提供器。</param>
        public UpdateService(ApplicationPaths paths, GitHubUpdateProvider github)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            this.github = github ?? throw new ArgumentNullException(nameof(github));
        }

        /// <inheritdoc />
        public Task<AvailableUpdateDto?> CheckAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(Path.Combine(paths.Root, UpdatePackageValidator.ManifestName)))
                return Task.FromResult<AvailableUpdateDto?>(null);
            var installed = UpdatePackageValidator.ReadInstalled(paths.Root);
            return github.CheckAsync(Version.Parse(installed.Version), cancellationToken);
        }
    }
}
