using Dictation.DataAccess.Runtime;

namespace Dictation.Core.Updating
{
    /// <summary>准备独占下载任务及已安装的受管理更新器，不替换程序或操作用户数据库。</summary>
    internal sealed class UpdatePackagePreparer
    {
        private readonly ApplicationPaths paths;
        private readonly GitHubUpdateProvider github;

        /// <summary>组合安装根目录与发行传输。</summary>
        /// <param name="paths">应用根目录。</param>
        /// <param name="github">固定发行来源。</param>
        public UpdatePackagePreparer(ApplicationPaths paths, GitHubUpdateProvider github)
        {
            this.paths = paths;
            this.github = github;
        }

        /// <summary>完整校验后返回启动条件，失败只删除本次独占任务。</summary>
        /// <param name="update">用户选中的新版。</param>
        /// <param name="cancellationToken">取消准备。</param>
        internal async Task<PreparedUpdateDto> PrepareAsync(AvailableUpdateDto update, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(update);
            cancellationToken.ThrowIfCancellationRequested();
            GitHubUpdateMapper.ValidateDownload(update);
            var installed = UpdatePackageValidator.ReadInstalled(paths.Root);
            if (!Version.TryParse(update.Version, out var next) || next <= Version.Parse(installed.Version))
                throw new InvalidDataException("只能下载比当前安装更新的正式版本。");
            var runnerFiles = installed.Files.Where(file => file.StartsWith("Updater/", StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var file in runnerFiles)
            {
                UpdatePackageValidator.EnsureNoLinks(paths.Root, file);
                if (!File.Exists(UpdatePackageValidator.Destination(paths.Root, file)))
                    throw new InvalidDataException("已安装的更新器文件不完整。");
            }
            UpdatePackageValidator.EnsureNoLinks(paths.Root, "CACHE/Updates");
            var job = Path.Combine(paths.Root, "CACHE", "Updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job);
            try
            {
                var package = Path.Combine(job, GitHubUpdateMapper.PackageName);
                var hash = await github.DownloadAsync(update, package, cancellationToken);
                var request = new ApplyUpdateRequestDto(paths.Root, package, hash, update.Version);
                UpdatePackageValidator.ValidateRequest(request);
                await using (var stream = File.OpenRead(package))
                    await UpdatePackageValidator.ReadPackageAsync(request, installed, stream, cancellationToken);
                var runnerDirectory = Path.Combine(job, "runner");
                foreach (var file in runnerFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var destination = UpdatePackageValidator.Destination(runnerDirectory, file["Updater/".Length..]);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(UpdatePackageValidator.Destination(paths.Root, file), destination, overwrite: false);
                }
                return new(job, Path.Combine(runnerDirectory, "Dictation.Updater.exe"), package, hash, update.Version, paths.Root);
            }
            catch
            {
                // 只清理本调用创建的随机任务，不能递归删除用户CACHE或原安装。
                Directory.Delete(job, recursive: true);
                throw;
            }
        }
    }
}
