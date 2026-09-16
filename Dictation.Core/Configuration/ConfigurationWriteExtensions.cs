using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Dictation.Core.Configuration
{
    /// <summary>
    /// 为现有 <see cref="IConfiguration"/> 提供原子 JSON 写回能力。
    /// </summary>
    internal static class ConfigurationWriteExtensions
    {
        /// <summary>
        /// 先写同目录临时文件，再以原子替换保存配置并重新加载配置根。
        /// </summary>
        internal static async Task SetValueAsync<T>(
            this IConfiguration configuration,
            string filePath,
            T value,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(value);
            if (configuration is not IConfigurationRoot configurationRoot)
            {
                throw new InvalidOperationException("设置写回要求 IConfigurationRoot。");
            }

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("设置文件路径必须包含目录。", nameof(filePath));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var temporaryFile = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}-{Guid.NewGuid():N}.tmp");

            try
            {
                Directory.CreateDirectory(directory);
                await using (var stream = new FileStream(
                    temporaryFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        value,
                        cancellationToken: cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryFile, fullPath, overwrite: true);
                configurationRoot.Reload();
            }
            catch (OperationCanceledException)
            {
                DeleteTemporaryFile(temporaryFile);
                throw;
            }
            catch (IOException exception)
            {
                DeleteTemporaryFile(temporaryFile);
                throw new SettingsPersistenceException("保存设置文件失败。", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                DeleteTemporaryFile(temporaryFile);
                throw new SettingsPersistenceException("保存设置文件失败。", exception);
            }
        }

        private static void DeleteTemporaryFile(string temporaryFile)
        {
            try
            {
                if (File.Exists(temporaryFile))
                {
                    File.Delete(temporaryFile);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
