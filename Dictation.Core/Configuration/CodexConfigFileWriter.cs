namespace Dictation.Core.Configuration
{
    /// <summary>负责Codex配置的原子文件替换及并发修改保护，不解析TOML。</summary>
    internal static class CodexConfigFileWriter
    {
        /// <summary>提交已准备文档并检查实际被原子替换的文件版本。</summary>
        /// <param name="path">客户端配置路径。</param>
        /// <param name="temporary">同目录准备文件。</param>
        /// <param name="original">操作开始时的快照。</param>
        /// <param name="replacement">本次准备的新内容。</param>
        internal static async Task CommitAsync(string path, string temporary, byte[] original, byte[] replacement)
        {
            var backup = Path.Combine(Path.GetDirectoryName(path)!, $".codex-conflict-{Guid.NewGuid():N}.bak");
            // 旧句柄不能证明路径仍指向旧文件；原子替换同时保留实际被替换的版本，随后核验该版本。
            File.Replace(temporary, path, backup);
            var displaced = await File.ReadAllBytesAsync(backup);
            if (original.AsSpan().SequenceEqual(displaced))
            {
                File.Delete(backup);
                return;
            }

            // 替换已经发生后不再响应取消，必须先完成恢复或保留恢复副本，不能丢下半次操作。
            var restored = await RestoreDisplacedAsync(path, backup, replacement);
            if (restored)
            {
                File.Delete(backup);
            }
            var message = restored ? "Codex配置被外部同时修改，本次操作已取消并恢复外部版本，请重新读取后重试。"
                : $"Codex配置再次变化或被占用，本次操作已取消。冲突版本保存在同目录的{Path.GetFileName(backup)}，请检查后重试。";
            throw new SettingsPersistenceException(message, new IOException("配置快照与实际被替换版本不同。"));
        }

        /// <summary>仅在当前内容仍为本次写入时恢复冲突版本，不覆盖后续外部更改。</summary>
        /// <param name="path">当前配置路径。</param>
        /// <param name="backup">原子替换保留下来的冲突版本。</param>
        /// <param name="replacement">本次写入内容，用于辨认可安全恢复的版本。</param>
        /// <returns>是否恢复完成；否表示外部再次修改，备份仍须保留。</returns>
        internal static async Task<bool> RestoreDisplacedAsync(string path, string backup, byte[] replacement)
        {
            try
            {
                // 恢复需独占当前路径的文件：在核对及写回期间拒绝读取、写入和原子换档。
                // 若外部已再次保存，则不回退那个新版本，冲突副本保留供恢复。
                await using var current = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None,
                    4096, FileOptions.Asynchronous);
                using var snapshot = new MemoryStream();
                await current.CopyToAsync(snapshot);
                if (!replacement.AsSpan().SequenceEqual(snapshot.GetBuffer().AsSpan(0, checked((int)snapshot.Length))))
                {
                    return false;
                }
                var bytes = await File.ReadAllBytesAsync(backup);
                current.Position = 0;
                await current.WriteAsync(bytes);
                current.SetLength(bytes.Length);
                await current.FlushAsync();
                current.Flush(flushToDisk: true);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 无法独占或恢复失败时必须保留副本，不能为了清理临时文件而删除外部数据。
                return false;
            }
        }

        /// <summary>仅在原文件仍与读取快照一致时替换；新建时禁止覆盖外部新文件。</summary>
        /// <param name="path">配置完整路径。</param>
        /// <param name="original">原始字节；为空表示读取时文件不存在。</param>
        /// <param name="replacement">已验证的完整新文档。</param>
        /// <param name="cancellationToken">取消准备或替换。</param>
        internal static async Task WriteAsync(string path, byte[]? original, byte[] replacement, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath)!;
            var temporary = Path.Combine(directory, $".codex-{Guid.NewGuid():N}.tmp");
            try
            {
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(temporary, replacement, cancellationToken);
                if (original is not null)
                {
                    // 禁止其它进程写入读到的同一文件；允许原子替换所需的删除共享。
                    await using var locked = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                        4096, FileOptions.Asynchronous);
                    using var current = new MemoryStream();
                    await locked.CopyToAsync(current, cancellationToken);
                    if (!original.AsSpan().SequenceEqual(current.GetBuffer().AsSpan(0, checked((int)current.Length))))
                        throw new IOException("配置已被外部修改，请重新读取后重试。");
                    cancellationToken.ThrowIfCancellationRequested();
                    await CommitAsync(fullPath, temporary, original, replacement);
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // 原先不存在时绝不覆盖期间被其它进程创建的文件。
                    File.Move(temporary, fullPath, overwrite: false);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new SettingsPersistenceException("Codex配置无法保存，文件可能已被修改或占用；请重新读取后重试。", exception);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
