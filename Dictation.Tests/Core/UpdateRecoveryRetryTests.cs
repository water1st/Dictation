using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dictation.Core.Updating;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    /// <summary>验证恢复中途碰到真实Windows文件锁时，旧备份仍完整且解除锁后能重试。</summary>
    public sealed class UpdateRecoveryRetryTests
    {
        [Fact]
        public async Task Rollback_FileLockKeepsAllBackupCopiesAndCanRetryAfterUnlock()
        {
            using var directory = new TemporaryDirectory();
            var root = Path.Combine(directory.Path, "app");
            Directory.CreateDirectory(Path.Combine(root, "Updater"));
            var files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" };
            foreach (var file in files) File.WriteAllText(Path.Combine(root, file), "old-" + file);
            File.WriteAllText(Path.Combine(root, "package-manifest.json"), JsonSerializer.Serialize(new { version = "1.0.1", files }));
            var package = Path.Combine(directory.Path, "update.zip");
            using (var archive = new ZipArchive(File.Create(package), ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(file).Open());
                    writer.Write("new-" + file);
                }
                using var manifest = new StreamWriter(archive.CreateEntry("package-manifest.json").Open());
                manifest.Write(JsonSerializer.Serialize(new { version = "1.0.2", files }));
            }
            var installer = new UpdateInstaller();
            var receipt = await installer.ApplyAsync(new(root, package,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))), "1.0.2"), TestContext.Current.CancellationToken);
            using (var locked = File.Open(Path.Combine(root, "Updater", "Dictation.Updater.exe"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var failure = await Record.ExceptionAsync(() => installer.RollbackAsync(receipt, TestContext.Current.CancellationToken));
                Assert.True(failure is IOException or UnauthorizedAccessException);
                foreach (var file in files)
                {
                    Assert.True(File.Exists(Path.Combine(receipt.TransactionDirectory, "backup", file)));
                    Assert.Equal("old-" + file, File.ReadAllText(Path.Combine(receipt.TransactionDirectory, "backup", file)));
                }
            }
            await installer.RollbackAsync(receipt, TestContext.Current.CancellationToken);
            foreach (var file in files) Assert.Equal("old-" + file, File.ReadAllText(Path.Combine(root, file)));
            Assert.False(Directory.Exists(receipt.TransactionDirectory));
        }
    }
}
