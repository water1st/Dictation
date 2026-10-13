using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dictation.Core;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>真实安装后再确认或回滚，验证启动失败时不会只剩新版程序。</summary>
    public sealed class UpdateRecoveryTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task Apply_ReturnsOwnedReceiptAndPreservesBackupUntilConfirmation()
        {
            using var fixture = new RecoveryFixture();
            var receipt = await fixture.ApplyAsync();
            Assert.Equal(fixture.Root, receipt.RootDirectory);
            Assert.Equal("1.0.2", receipt.Version);
            Assert.StartsWith(Path.Combine(fixture.Root, "CACHE", "Updates") + Path.DirectorySeparatorChar, receipt.TransactionDirectory);
            Assert.Equal("old", File.ReadAllText(Path.Combine(receipt.TransactionDirectory, "backup", "Dictation.Presentation.exe")));
            Assert.Equal("new", File.ReadAllText(fixture.Path("Dictation.Presentation.exe")));
        }

        [Fact]
        public async Task Rollback_RestoresOldManifestRemovedFilesAndRemovesAddedFiles()
        {
            using var fixture = new RecoveryFixture();
            var receipt = await fixture.ApplyAsync();
            await fixture.Services.GetRequiredService<IUpdateRollback>().RollbackAsync(receipt, Token);
            Assert.Equal("old", File.ReadAllText(fixture.Path("Dictation.Presentation.exe")));
            Assert.Equal("old-obsolete", File.ReadAllText(fixture.Path("obsolete.dll")));
            Assert.False(File.Exists(fixture.Path("added.dll")));
            using var manifest = JsonDocument.Parse(File.ReadAllText(fixture.Path("package-manifest.json")));
            Assert.Equal("1.0.1", manifest.RootElement.GetProperty("version").GetString());
            fixture.AssertUserData();
        }

        [Fact]
        public async Task Commit_DeletesOnlyReceiptTransactionAndKeepsNewProgramAndUserFiles()
        {
            using var fixture = new RecoveryFixture();
            var receipt = await fixture.ApplyAsync();
            await fixture.Services.GetRequiredService<IUpdateCommit>().CommitAsync(receipt, Token);
            Assert.False(Directory.Exists(receipt.TransactionDirectory));
            Assert.Equal("new", File.ReadAllText(fixture.Path("Dictation.Presentation.exe")));
            Assert.Equal("added", File.ReadAllText(fixture.Path("added.dll")));
            fixture.AssertUserData();
        }

        [Fact]
        public async Task Rollback_RejectsForeignDirectoryBeforeDeletingAnything()
        {
            using var fixture = new RecoveryFixture();
            var receipt = await fixture.ApplyAsync();
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Services.GetRequiredService<IUpdateRollback>()
                .RollbackAsync(receipt with { TransactionDirectory = fixture.Path("DATA") }, Token));
            Assert.Equal("new", File.ReadAllText(fixture.Path("Dictation.Presentation.exe")));
            fixture.AssertUserData();
        }

        [Fact]
        public async Task Commit_RejectsWrongVersionAndKeepsRecoverableBackup()
        {
            using var fixture = new RecoveryFixture();
            var receipt = await fixture.ApplyAsync();
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Services.GetRequiredService<IUpdateCommit>()
                .CommitAsync(receipt with { Version = "9.0.0" }, Token));
            Assert.True(File.Exists(Path.Combine(receipt.TransactionDirectory, "backup", "Dictation.Presentation.exe")));
        }

        [Fact]
        public async Task Rollback_CanceledBeforeStartLeavesBackupAndNewProgramUntouched()
        {
            using var fixture = new RecoveryFixture();
            var receipt = await fixture.ApplyAsync();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Services.GetRequiredService<IUpdateRollback>()
                .RollbackAsync(receipt, canceled.Token));
            Assert.Equal("new", File.ReadAllText(fixture.Path("Dictation.Presentation.exe")));
            Assert.True(File.Exists(Path.Combine(receipt.TransactionDirectory, "backup", "Dictation.Presentation.exe")));
        }

        [Fact]
        public async Task Container_ExposesNarrowInstallationCapabilitiesUsingOneInstance()
        {
            using var fixture = new RecoveryFixture();
            Assert.Same(fixture.Services.GetRequiredService<IUpdateInstaller>(), fixture.Services.GetRequiredService<IUpdateRollback>());
            Assert.Same(fixture.Services.GetRequiredService<IUpdateInstaller>(), fixture.Services.GetRequiredService<IUpdateCommit>());
            var receipt = await fixture.ApplyAsync();
            await fixture.Services.GetRequiredService<IUpdateRollback>().RollbackAsync(receipt, Token);
            Assert.Equal("old", File.ReadAllText(fixture.Path("Dictation.Presentation.exe")));
        }

        /// <summary>创建真实旧文件、完整ZIP及原容器，不模拟安装与恢复。</summary>
        private sealed class RecoveryFixture : IDisposable
        {
            private readonly TemporaryDirectory directory = new();
            private readonly ApplyUpdateRequestDto request;
            internal ServiceProvider Services { get; }
            internal string Root => System.IO.Path.Combine(directory.Path, "app");
            internal string Path(string relative) => System.IO.Path.Combine(Root, relative);

            internal RecoveryFixture()
            {
                Directory.CreateDirectory(Root);
                var old = new Dictionary<string, string>
                {
                    ["Dictation.Presentation.exe"] = "old", ["Updater/Dictation.Updater.exe"] = "old-runner", ["obsolete.dll"] = "old-obsolete"
                };
                foreach (var item in old) Write(item.Key, item.Value);
                Write("package-manifest.json", JsonSerializer.Serialize(new { version = "1.0.1", files = old.Keys.ToArray() }));
                foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" }) Write(name + "/keep.bin", "user-" + name);
                Write("notes.txt", "user-note");
                var package = System.IO.Path.Combine(directory.Path, "update.zip");
                var next = new Dictionary<string, string>
                {
                    ["Dictation.Presentation.exe"] = "new", ["Updater/Dictation.Updater.exe"] = "new-runner", ["added.dll"] = "added"
                };
                using (var zip = new ZipArchive(File.Create(package), ZipArchiveMode.Create))
                {
                    foreach (var item in next)
                    {
                        using var writer = new StreamWriter(zip.CreateEntry(item.Key).Open());
                        writer.Write(item.Value);
                    }
                    using var manifest = new StreamWriter(zip.CreateEntry("package-manifest.json").Open());
                    manifest.Write(JsonSerializer.Serialize(new { version = "1.0.2", files = next.Keys.ToArray() }));
                }
                request = new(Root, package, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))), "1.0.2");
                Services = new ServiceCollection().AddDictationDataAccess(Root).AddDictationCore().BuildServiceProvider();
            }
            internal Task<UpdateInstallationDto> ApplyAsync() => Services.GetRequiredService<IUpdateInstaller>().ApplyAsync(request, Token);
            internal void AssertUserData()
            {
                foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" })
                    Assert.Equal("user-" + name, File.ReadAllText(Path(name + "/keep.bin")));
                Assert.Equal("user-note", File.ReadAllText(Path("notes.txt")));
            }
            private void Write(string relative, string text)
            {
                var file = Path(relative);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
                File.WriteAllText(file, text);
            }
            public void Dispose() { Services.Dispose(); directory.Dispose(); }
        }
    }
}
