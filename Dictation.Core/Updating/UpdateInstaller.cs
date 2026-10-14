using System.IO.Compression;

namespace Dictation.Core.Updating
{
    /// <summary>编排全量包校验、独占暂存和受管理文件的可回滚替换。</summary>
    internal sealed class UpdateInstaller : IUpdateInstaller, IUpdateRollback, IUpdateCommit
    {
        /// <summary>由唯一容器创建无状态安装器；复用内部包校验及文件事务。</summary>
        public UpdateInstaller() { }

        /// <inheritdoc />
        public async Task<UpdateInstallationDto> ApplyAsync(ApplyUpdateRequestDto request, CancellationToken cancellationToken = default)
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
            UpdateRecoveryStore.Write(root, job, installed, package);
            UpdateFileTransaction.Replace(root, stage, backup, installed.Files, package.Files, cancellationToken);
            return new(root, job, request.Version);
        }

        /// <inheritdoc />
        public Task RollbackAsync(UpdateInstallationDto installation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = UpdateRecoveryStore.Read(installation);
            var current = UpdatePackageValidator.ReadInstalled(installation.RootDirectory);
            if (current.Version != record.Version && current.Version != record.PreviousVersion)
                throw new InvalidDataException("当前安装已改变，不能使用此恢复事务。");
            var backup = Path.Combine(installation.TransactionDirectory, "backup");
            foreach (var path in record.PreviousFiles)
                if (!File.Exists(UpdatePackageValidator.Destination(backup, path)))
                    throw new InvalidDataException("旧文件备份缺失，保留当前程序和事务供恢复。");
            UpdateRecoveryStore.EnsureOwnedTree(installation.TransactionDirectory);
            // 保留备份副本直到全部恢复成功；清单最后恢复，失败可再次使用同一事务。
            foreach (var path in record.PreviousFiles.OrderBy(path => path.Equals(UpdatePackageValidator.ManifestName, StringComparison.OrdinalIgnoreCase)))
            {
                var target = UpdatePackageValidator.Destination(installation.RootDirectory, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(UpdatePackageValidator.Destination(backup, path), target, overwrite: true);
            }
            foreach (var path in record.NewFiles.Except(record.PreviousFiles, StringComparer.OrdinalIgnoreCase))
                File.Delete(UpdatePackageValidator.Destination(installation.RootDirectory, path));
            Directory.Delete(installation.TransactionDirectory, recursive: true);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task CommitAsync(UpdateInstallationDto installation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = UpdateRecoveryStore.Read(installation);
            var current = UpdatePackageValidator.ReadInstalled(installation.RootDirectory);
            if (current.Version != record.Version || !current.Files.Append(UpdatePackageValidator.ManifestName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(record.NewFiles))
                throw new InvalidDataException("当前安装不再是收据对应的新版，不能删除恢复备份。");
            UpdateRecoveryStore.EnsureOwnedTree(installation.TransactionDirectory);
            Directory.Delete(installation.TransactionDirectory, recursive: true);
            return Task.CompletedTask;
        }
    }
}
