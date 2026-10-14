namespace Dictation.Core.Updating
{
    /// <summary>记录受管理文件的移动顺序，失败恢复旧文件；不操作未管理的用户文件。</summary>
    internal static class UpdateFileTransaction
    {
        /// <summary>先备份旧文件再放入新文件，任何安装失败均回滚已发生的移动。</summary>
        /// <param name="root">已验证安装目录。</param>
        /// <param name="stage">此任务的解压目录。</param>
        /// <param name="backup">此任务的原文件备份目录。</param>
        /// <param name="oldFiles">旧版文件清单。</param>
        /// <param name="newFiles">新版文件清单。</param>
        /// <param name="cancellationToken">取消安装，但不取消回滚。</param>
        internal static void Replace(string root, string stage, string backup, IReadOnlyList<string> oldFiles,
            IReadOnlyList<string> newFiles, CancellationToken cancellationToken)
        {
            var previous = oldFiles.Append(UpdatePackageValidator.ManifestName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var next = newFiles.Append(UpdatePackageValidator.ManifestName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in previous.Union(next, StringComparer.OrdinalIgnoreCase))
            {
                UpdatePackageValidator.EnsureNoLinks(root, path);
                var destination = UpdatePackageValidator.Destination(root, path);
                if (Directory.Exists(destination) || (File.Exists(destination) && !previous.Contains(path)))
                    throw new InvalidDataException("更新文件与未管理的用户文件或目录冲突。");
            }
            var saved = new List<string>();
            var installed = new List<string>();
            try
            {
                foreach (var path in previous.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = UpdatePackageValidator.Destination(root, path);
                    if (!File.Exists(source)) continue;
                    var target = UpdatePackageValidator.Destination(backup, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(source, target);
                    saved.Add(path);
                }
                foreach (var path in next.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = UpdatePackageValidator.Destination(root, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(UpdatePackageValidator.Destination(stage, path), target);
                    installed.Add(path);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception failure)
            {
                var rollbackFailures = new List<Exception>();
                // 回滚不能被取消打断；无法恢复时保留备份并报告，不伪装成成功。
                foreach (var path in installed.AsEnumerable().Reverse())
                {
                    try { File.Delete(UpdatePackageValidator.Destination(root, path)); }
                    catch (Exception exception) { rollbackFailures.Add(exception); }
                }
                foreach (var path in saved.AsEnumerable().Reverse())
                {
                    try { File.Move(UpdatePackageValidator.Destination(backup, path), UpdatePackageValidator.Destination(root, path)); }
                    catch (Exception exception) { rollbackFailures.Add(exception); }
                }
                if (rollbackFailures.Count > 0) throw new AggregateException("更新失败，部分旧文件无法恢复，备份已保留。", new[] { failure }.Concat(rollbackFailures));
                throw;
            }
        }
    }
}
