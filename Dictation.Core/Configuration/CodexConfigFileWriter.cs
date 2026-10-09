namespace Dictation.Core.Configuration
{
    /// <summary>负责Codex配置的原子文件替换及并发修改保护，不解析TOML。</summary>
    internal static class CodexConfigFileWriter
    {
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
                    File.Replace(temporary, fullPath, destinationBackupFileName: null);
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
