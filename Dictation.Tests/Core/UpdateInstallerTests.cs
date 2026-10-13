using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dictation.Core.Updating;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    /// <summary>用真实 ZIP、隔离目录和 Windows 文件锁验证全量更新的边界与回滚。</summary>
    public sealed class UpdateInstallerTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task Apply_ReplacesManagedFilesAndPreservesAllUserDirectories()
        {
            using var fixture = new PackageFixture();
            await new UpdateInstaller().ApplyAsync(fixture.Request(), Token);
            Assert.Equal("new", File.ReadAllText(fixture.InRoot("Dictation.Presentation.exe")));
            Assert.Equal("new-resource", File.ReadAllText(fixture.InRoot("Resources/item.dat")));
            Assert.Equal("added", File.ReadAllText(fixture.InRoot("added.dll")));
            Assert.False(File.Exists(fixture.InRoot("obsolete.dll")));
            Assert.Equal("user-note", File.ReadAllText(fixture.InRoot("notes.txt")));
            foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" })
                Assert.Equal("user-" + name, File.ReadAllText(fixture.InRoot(name + "/keep.bin")));
            using var manifest = JsonDocument.Parse(File.ReadAllText(fixture.InRoot("package-manifest.json")));
            Assert.Equal("1.0.2", manifest.RootElement.GetProperty("version").GetString());
        }

        [Fact]
        public async Task Apply_HashMismatchLeavesOriginalProgramAndDataUntouched()
        {
            using var fixture = new PackageFixture();
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request() with { Sha256 = new string('0', 64) }, Token));
            fixture.AssertOriginal();
        }

        [Fact]
        public async Task Apply_RejectsCorruptArchiveBeforeReplacingFiles()
        {
            using var fixture = new PackageFixture();
            File.WriteAllText(fixture.Package, "这不是ZIP文件");
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
        }

        [Theory]
        [InlineData("../escape.txt")]
        [InlineData("/escape.txt")]
        [InlineData("C:/escape.txt")]
        [InlineData("CONFIG/settings.json")]
        [InlineData("data/UserData.db")]
        [InlineData("LOGS/log.txt")]
        [InlineData("Backups/old.db")]
        [InlineData("CACHE/task.bin")]
        [InlineData(".git/config")]
        [InlineData("NUL.txt")]
        [InlineData("folder/CON.txt")]
        [InlineData("folder./file.txt")]
        public async Task Apply_RejectsUnsafePathsWithoutTouchingOriginalFiles(string path)
        {
            using var fixture = new PackageFixture();
            fixture.NewFiles.Add(path, "禁止写入");
            fixture.WritePackage();
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
            Assert.False(File.Exists(System.IO.Path.Combine(fixture.DirectoryPath, "escape.txt")));
        }

        [Fact]
        public async Task Apply_RejectsCaseInsensitiveDuplicateEntries()
        {
            using var fixture = new PackageFixture();
            fixture.WritePackage(extraEntry: "aaa.DLL");
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
        }

        [Fact]
        public async Task Apply_RejectsZipSymbolicLink()
        {
            using var fixture = new PackageFixture();
            fixture.NewFiles.Add("link.bin", "../DATA/UserData.db");
            fixture.WritePackage(symbolicEntry: "link.bin");
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
        }

        [Fact]
        public async Task Apply_RejectsMissingDeclaredFile()
        {
            using var fixture = new PackageFixture();
            fixture.WritePackage(omitEntry: "AAA.dll");
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
        }

        [Theory]
        [InlineData("1.0.1")]
        [InlineData("1.0.0")]
        public async Task Apply_RejectsSameVersionOrDowngrade(string version)
        {
            using var fixture = new PackageFixture();
            fixture.WritePackage(version: version);
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request() with { Version = version }, Token));
            fixture.AssertOriginal();
        }

        [Fact]
        public async Task Apply_RejectsManifestVersionDifferentFromRequestedRelease()
        {
            using var fixture = new PackageFixture();
            fixture.WritePackage(version: "2.0.0");
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
        }

        [Fact]
        public async Task Apply_RejectsUnknownUserFileCollisionBeforeMovingProgram()
        {
            using var fixture = new PackageFixture();
            fixture.NewFiles.Add("notes.txt", "不要覆盖用户笔记");
            fixture.WritePackage();
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
            Assert.Equal("user-note", File.ReadAllText(fixture.InRoot("notes.txt")));
        }

        [Fact]
        public async Task Apply_FileLockRollsBackFilesAlreadyMoved()
        {
            using var fixture = new PackageFixture();
            using (var locked = File.Open(fixture.InRoot("Dictation.Presentation.exe"), FileMode.Open, FileAccess.Read, FileShare.Read))
                await Assert.ThrowsAnyAsync<IOException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            fixture.AssertOriginal();
            Assert.Equal("old-a", File.ReadAllText(fixture.InRoot("AAA.dll")));
            Assert.False(File.Exists(fixture.InRoot("added.dll")));
        }

        [Fact]
        public async Task Apply_CancelledRequestDoesNotMoveAnyFiles()
        {
            using var fixture = new PackageFixture();
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), canceled.Token));
            fixture.AssertOriginal();
        }

        [Fact]
        public async Task Apply_RejectsUnmanagedInstallation()
        {
            using var fixture = new PackageFixture();
            File.Delete(fixture.InRoot("package-manifest.json"));
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(fixture.Request(), Token));
            Assert.Equal("old", File.ReadAllText(fixture.InRoot("Dictation.Presentation.exe")));
        }

        [Theory]
        [InlineData("relative", null, "1.0.2")]
        [InlineData(null, "abc", "1.0.2")]
        [InlineData(null, "nonhex", "1.0.2")]
        [InlineData(null, null, "1.0")]
        public async Task Apply_RejectsMalformedRequest(string? root, string? hash, string version)
        {
            using var fixture = new PackageFixture();
            var request = fixture.Request();
            request = request with { RootDirectory = root ?? request.RootDirectory,
                Sha256 = hash == "nonhex" ? new string('z', 64) : hash ?? request.Sha256, Version = version };
            await Assert.ThrowsAnyAsync<ArgumentException>(() => new UpdateInstaller().ApplyAsync(request, Token));
            fixture.AssertOriginal();
        }

        /// <summary>只在测试独占目录构造旧程序、用户数据与可控的新 ZIP，不使用产品打包器生成期望。</summary>
        private sealed class PackageFixture : IDisposable
        {
            private readonly TemporaryDirectory directory = new();
            private readonly Dictionary<string, string> oldFiles = new()
            {
                ["Dictation.Presentation.exe"] = "old", ["AAA.dll"] = "old-a", ["obsolete.dll"] = "old-remove",
                ["Updater/Dictation.Updater.exe"] = "old-runner", ["Resources/item.dat"] = "old-resource"
            };
            internal Dictionary<string, string> NewFiles { get; } = new()
            {
                ["Dictation.Presentation.exe"] = "new", ["AAA.dll"] = "new-a", ["added.dll"] = "added",
                ["Updater/Dictation.Updater.exe"] = "new-runner", ["Resources/item.dat"] = "new-resource"
            };
            internal string DirectoryPath => directory.Path;
            internal string Root => System.IO.Path.Combine(directory.Path, "app");
            internal string Package => System.IO.Path.Combine(directory.Path, "update.zip");
            internal string InRoot(string relative) => System.IO.Path.Combine(Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

            internal PackageFixture()
            {
                Directory.CreateDirectory(Root);
                foreach (var file in oldFiles) WriteRoot(file.Key, file.Value);
                WriteRoot("package-manifest.json", JsonSerializer.Serialize(new { version = "1.0.1", files = oldFiles.Keys.ToArray() }));
                foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" }) WriteRoot(name + "/keep.bin", "user-" + name);
                WriteRoot("notes.txt", "user-note");
                WritePackage();
            }

            internal ApplyUpdateRequestDto Request() => new(Root, Package, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Package))), "1.0.2");

            internal void WritePackage(string version = "1.0.2", string? extraEntry = null, string? symbolicEntry = null, string? omitEntry = null)
            {
                using var zip = new ZipArchive(File.Create(Package), ZipArchiveMode.Create);
                foreach (var file in NewFiles)
                {
                    if (file.Key == omitEntry) continue;
                    var entry = zip.CreateEntry(file.Key);
                    if (file.Key == symbolicEntry) entry.ExternalAttributes = unchecked((int)0xA1FF0000);
                    using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                    writer.Write(file.Value);
                }
                if (extraEntry is not null)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(extraEntry).Open());
                    writer.Write("duplicate");
                }
                using var manifest = new StreamWriter(zip.CreateEntry("package-manifest.json").Open());
                manifest.Write(JsonSerializer.Serialize(new { version, files = NewFiles.Keys.ToArray() }));
            }

            internal void AssertOriginal()
            {
                foreach (var file in oldFiles) Assert.Equal(file.Value, File.ReadAllText(InRoot(file.Key)));
                foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" }) Assert.Equal("user-" + name, File.ReadAllText(InRoot(name + "/keep.bin")));
            }
            private void WriteRoot(string relative, string text)
            {
                var path = InRoot(relative);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
            public void Dispose() => directory.Dispose();
        }
    }
}
