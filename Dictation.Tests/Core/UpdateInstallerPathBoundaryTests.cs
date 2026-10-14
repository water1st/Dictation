using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dictation.Core.Updating;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    /// <summary>验证 Windows 根目录别名与 CACHE 子目录链接不能绕过更新路径边界。</summary>
    public sealed class UpdateInstallerPathBoundaryTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Apply_DriveRootIsRejectedBeforeReadingPackage(bool forwardSlash)
        {
            using var directory = new TemporaryDirectory();
            var drive = Path.GetPathRoot(directory.Path)!;
            if (forwardSlash) drive = drive.Replace('\\', '/');
            // 包路径确保不存在，即使错误地通过根目录校验也不可能替换真实驱动器文件。
            var request = new ApplyUpdateRequestDto(drive, Path.Combine(directory.Path, "does-not-exist.zip"), new string('0', 64), "1.0.2");
            await Assert.ThrowsAnyAsync<ArgumentException>(() => new UpdateInstaller().ApplyAsync(request, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Apply_CacheUpdatesJunctionDoesNotWriteOutsideInstallation()
        {
            using var directory = new TemporaryDirectory();
            var root = Path.Combine(directory.Path, "app");
            var outside = Path.Combine(directory.Path, "outside");
            Directory.CreateDirectory(Path.Combine(root, "CACHE"));
            Directory.CreateDirectory(Path.Combine(root, "Updater"));
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "marker.txt"), "outside-user-file");
            var files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" };
            foreach (var file in files) File.WriteAllText(Path.Combine(root, file), "old");
            File.WriteAllText(Path.Combine(root, "package-manifest.json"), JsonSerializer.Serialize(new { version = "1.0.1", files }));
            var package = Path.Combine(directory.Path, "update.zip");
            using (var zip = new ZipArchive(File.Create(package), ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(file).Open());
                    writer.Write("new");
                }
                using var manifest = new StreamWriter(zip.CreateEntry("package-manifest.json").Open());
                manifest.Write(JsonSerializer.Serialize(new { version = "1.0.2", files }));
            }
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("New-Item -ItemType Junction -Path $env:DICTATION_TEST_LINK -Target $env:DICTATION_TEST_TARGET -ErrorAction Stop | Out-Null");
            start.Environment["DICTATION_TEST_LINK"] = Path.Combine(root, "CACHE", "Updates");
            start.Environment["DICTATION_TEST_TARGET"] = outside;
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, process.ExitCode);
            var request = new ApplyUpdateRequestDto(root, package, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))), "1.0.2");
            try
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateInstaller().ApplyAsync(request, TestContext.Current.CancellationToken));
                Assert.Equal("old", File.ReadAllText(Path.Combine(root, "Dictation.Presentation.exe")));
                Assert.Equal("outside-user-file", File.ReadAllText(Path.Combine(outside, "marker.txt")));
                Assert.Single(Directory.EnumerateFileSystemEntries(outside));
            }
            finally
            {
                // 先只移除自己创建的链接，避免外层临时目录先删除目标后留下失效 junction。
                Directory.Delete(Path.Combine(root, "CACHE", "Updates"));
            }
        }
    }
}
