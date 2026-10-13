using System.Text.Json;

namespace Dictation.Core.Updating
{
    /// <summary>保存及校验限定在CACHE独占目录内的恢复记录，不操作用户数据库。</summary>
    internal static class UpdateRecoveryStore
    {
        private const string RecordName = "recovery.json";

        /// <summary>安装前记录实际旧文件与将安装的新文件，不把缺失旧文件伪装成备份。</summary>
        /// <param name="root">安装根目录。</param>
        /// <param name="job">此调用创建的独占事务。</param>
        /// <param name="installed">旧发行清单。</param>
        /// <param name="package">已校验新发行清单。</param>
        internal static void Write(string root, string job, UpdatePackageManifest installed, UpdatePackageManifest package)
        {
            var previous = installed.Files.Append(UpdatePackageValidator.ManifestName)
                .Where(path => File.Exists(UpdatePackageValidator.Destination(root, path))).ToArray();
            var next = package.Files.Append(UpdatePackageValidator.ManifestName).ToArray();
            var record = new UpdateRecoveryRecord(root, installed.Version, package.Version, previous, next);
            using var stream = new FileStream(Path.Combine(job, RecordName), FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(stream, record);
        }

        /// <summary>拒绝外部目录、错误身份或不安全文件清单，恢复不能信任公开收据。</summary>
        /// <param name="receipt">等待确认或回滚的收据。</param>
        /// <returns>与收据身份一致的恢复记录。</returns>
        internal static UpdateRecoveryRecord Read(UpdateInstallationDto receipt)
        {
            ArgumentNullException.ThrowIfNull(receipt);
            if (!Path.IsPathFullyQualified(receipt.RootDirectory) || !Path.IsPathFullyQualified(receipt.TransactionDirectory))
                throw new InvalidDataException("恢复收据必须使用完全限定路径。");
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(receipt.RootDirectory));
            var job = Path.TrimEndingDirectorySeparator(Path.GetFullPath(receipt.TransactionDirectory));
            var name = Path.GetFileName(job);
            if (root == Path.GetPathRoot(root) || !Guid.TryParseExact(name, "N", out _)
                || !string.Equals(Path.GetDirectoryName(job), Path.Combine(root, "CACHE", "Updates"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("恢复目录不是应用的独占更新事务。");
            UpdatePackageValidator.EnsureNoLinks(root, Path.GetRelativePath(root, Path.Combine(job, RecordName)));
            UpdateRecoveryRecord? record;
            try
            {
                using var stream = File.OpenRead(Path.Combine(job, RecordName));
                record = JsonSerializer.Deserialize<UpdateRecoveryRecord>(stream);
            }
            catch (JsonException exception) { throw new InvalidDataException("恢复记录格式错误。", exception); }
            if (record is null || record.PreviousFiles is null || record.NewFiles is null
                || !string.Equals(record.RootDirectory, root, StringComparison.OrdinalIgnoreCase)
                || record.Version != receipt.Version || !Version.TryParse(record.Version, out _)
                || !Version.TryParse(record.PreviousVersion, out _))
                throw new InvalidDataException("恢复收据与事务身份不符。");
            foreach (var files in new[] { record.PreviousFiles, record.NewFiles })
            {
                if (!files.Contains(UpdatePackageValidator.ManifestName, StringComparer.OrdinalIgnoreCase)
                    || !files.Contains("Dictation.Presentation.exe", StringComparer.OrdinalIgnoreCase)
                    || files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
                    throw new InvalidDataException("恢复记录的文件清单不完整或重复。");
                foreach (var file in files)
                {
                    UpdatePackageValidator.ValidateRelativePath(file);
                    UpdatePackageValidator.EnsureNoLinks(root, file);
                    UpdatePackageValidator.EnsureNoLinks(job, "backup/" + file);
                }
            }
            return record;
        }

        /// <summary>递归清理前先检查全部目录项，不能跟随被改成链接的任务子目录。</summary>
        /// <param name="directory">已校验属于该收据的独占事务目录。</param>
        internal static void EnsureOwnedTree(string directory)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("恢复事务包含重解析点。");
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("恢复事务包含重解析点。");
                if ((attributes & FileAttributes.Directory) != 0) EnsureOwnedTree(entry);
            }
        }
    }
}
