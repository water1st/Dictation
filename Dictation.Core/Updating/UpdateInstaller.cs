using System.IO.Compression;

namespace Dictation.Core.Updating
{
    /// <summary>编排全量包校验、独占暂存和受管理文件的可回滚替换。</summary>
    internal sealed class UpdateInstaller : IUpdateInstaller
    {
        /// <summary>由唯一容器创建无状态安装器；复用内部包校验及文件事务。</summary>
        public UpdateInstaller() { }

        /// <inheritdoc />
        public async Task ApplyAsync(ApplyUpdateRequestDto request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            UpdatePackageValidator.ValidateRequest(request);
            var root = Path.GetFullPath(request.RootDirectory);
            var installed = UpdatePackageValidator.ReadInstalled(root);
            // 同一个只读句柄覆盖哈希校验与解压，避免再次打开时读取到被替换的包。
            await using var stream = File.OpenRead(request.PackagePath);
            var package = await UpdatePackageValidator.ReadPackageAsync(request, installed, stream, cancellationToken);
            UpdatePackageValidator.EnsureNoLinks(root, "CACHE/Updates");
            var job = Path.Combine(root, "CACHE", "Updates", Guid.NewGuid().ToString("N"));
            var stage = Path.Combine(job, "stage");
            var backup = Path.Combine(job, "backup");
            Directory.CreateDirectory(stage);
            // 解压交给 BCL，备份留在独占任务中；不清空根目录、不覆盖用户 BACKUPS。
            stream.Position = 0;
            await ZipFile.ExtractToDirectoryAsync(stream, stage, overwriteFiles: false, cancellationToken);
            UpdateFileTransaction.Replace(root, stage, backup, installed.Files, package.Files, cancellationToken);
        }
    }
}
