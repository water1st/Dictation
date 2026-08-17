using System.Text.Json;
using Dictation.DataAccess.Runtime;

namespace Dictation.DataAccess.Configuration
{
    public sealed class SettingsDataAccessor
    {
        private readonly ApplicationPaths applicationPaths;

        public SettingsDataAccessor(ApplicationPaths applicationPaths)
        {
            this.applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
        }

        public async Task<SettingsPo?> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(applicationPaths.SettingsFile))
            {
                return null;
            }

            try
            {
                await using var stream = new FileStream(
                    applicationPaths.SettingsFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan);
                return await JsonSerializer.DeserializeAsync<SettingsPo>(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new SettingsDataAccessException("设置文件 JSON 格式无效。", exception);
            }
            catch (IOException exception)
            {
                throw new SettingsDataAccessException("读取设置文件失败。", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new SettingsDataAccessException("读取设置文件失败。", exception);
            }
        }

        public async Task SaveAsync(SettingsPo settings, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);
            applicationPaths.EnsureRuntimeDirectories();
            var temporaryFile = Path.Combine(applicationPaths.Config, $".settings-{Guid.NewGuid():N}.tmp");

            try
            {
                await using (var stream = new FileStream(
                    temporaryFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                File.Move(temporaryFile, applicationPaths.SettingsFile, overwrite: true);
            }
            catch (OperationCanceledException)
            {
                DeleteTemporaryFile(temporaryFile);
                throw;
            }
            catch (IOException exception)
            {
                DeleteTemporaryFile(temporaryFile);
                throw new SettingsDataAccessException("保存设置文件失败。", exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                DeleteTemporaryFile(temporaryFile);
                throw new SettingsDataAccessException("保存设置文件失败。", exception);
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
