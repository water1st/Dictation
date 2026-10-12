using System.Text;
using Dictation.Core.Configuration;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    /// <summary>复现锁后外部原子换档，验证最终提交保护及第三方后续更新不被回退覆盖。</summary>
    public sealed class CodexConfigCommitTests
    {
        [Fact]
        public async Task AtomicExternalReplacementAfterLock_IsRejectedAndRestored()
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "config.toml");
            var original = Encoding.UTF8.GetBytes("model='original'\n");
            var external = Encoding.UTF8.GetBytes("model='external' # 外部注释\n");
            var replacement = Encoding.UTF8.GetBytes("model='original'\n[mcp_servers.dictation]\nurl='http://127.0.0.1:47821/mcp'\n");
            await File.WriteAllBytesAsync(path, original, TestContext.Current.CancellationToken);
            await using var oldHandle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var externalTemporary = Path.Combine(directory.Path, "external.tmp");
            await File.WriteAllBytesAsync(externalTemporary, external, TestContext.Current.CancellationToken);
            File.Replace(externalTemporary, path, null);
            using var oldSnapshot = new MemoryStream();
            await oldHandle.CopyToAsync(oldSnapshot, TestContext.Current.CancellationToken);
            Assert.Equal(original, oldSnapshot.ToArray());
            var staged = Path.Combine(directory.Path, "registration.tmp");
            await File.WriteAllBytesAsync(staged, replacement, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<SettingsPersistenceException>(() => CodexConfigFileWriter.CommitAsync(path, staged, original, replacement));
            Assert.Equal(external, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.Equal(new[] { "config.toml" }, Directory.GetFiles(directory.Path).Select(Path.GetFileName).Order().ToArray());
        }

        [Fact]
        public async Task RecoveryAfterAnotherExternalUpdate_DoesNotOverwriteLatestAndKeepsBackup()
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "config.toml");
            var backup = Path.Combine(directory.Path, "conflict.bak");
            var displaced = Encoding.UTF8.GetBytes("model='first-external'\n");
            var latest = Encoding.UTF8.GetBytes("model='latest-external'\n");
            await File.WriteAllBytesAsync(backup, displaced, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(path, latest, TestContext.Current.CancellationToken);
            Assert.False(await CodexConfigFileWriter.RestoreDisplacedAsync(path, backup, Encoding.UTF8.GetBytes("our replacement")));
            Assert.Equal(latest, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.Equal(displaced, await File.ReadAllBytesAsync(backup, TestContext.Current.CancellationToken));
        }
    }
}
