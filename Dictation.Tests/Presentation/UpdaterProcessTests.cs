using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Presentation
{
    /// <summary>运行实际WinExe更新器，验证坏包及新程序启动失败时保留或恢复旧文件。</summary>
    public sealed class UpdaterProcessTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Runner_InvalidHashOrUnstartableNewProgramPreservesOldReleaseAndUserFiles(bool invalidHash)
        {
            using var directory = new TemporaryDirectory();
            var root = Path.Combine(directory.Path, "app with spaces");
            Directory.CreateDirectory(Path.Combine(root, "Updater"));
            var old = new Dictionary<string, string>
            {
                ["Dictation.Presentation.exe"] = "old-not-a-PE", ["Updater/Dictation.Updater.exe"] = "old-runner", ["obsolete.dll"] = "old-obsolete"
            };
            foreach (var item in old) File.WriteAllText(Path.Combine(root, item.Key), item.Value);
            File.WriteAllText(Path.Combine(root, "package-manifest.json"), JsonSerializer.Serialize(new { version = "1.0.1", files = old.Keys.ToArray() }));
            foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" })
            {
                Directory.CreateDirectory(Path.Combine(root, name));
                File.WriteAllText(Path.Combine(root, name, "keep.bin"), "user-" + name);
            }
            var package = Path.Combine(directory.Path, "update.zip");
            var next = new Dictionary<string, string>
            {
                ["Dictation.Presentation.exe"] = "new-not-a-PE", ["Updater/Dictation.Updater.exe"] = "new-runner", ["added.dll"] = "new-added"
            };
            using (var archive = new ZipArchive(File.Create(package), ZipArchiveMode.Create))
            {
                foreach (var item in next)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(item.Key).Open());
                    writer.Write(item.Value);
                }
                using var manifest = new StreamWriter(archive.CreateEntry("package-manifest.json").Open());
                manifest.Write(JsonSerializer.Serialize(new { version = "1.0.2", files = next.Keys.ToArray() }));
            }
            var hash = invalidHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package)));
            var info = new ProcessStartInfo(FindRunner()) { UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = directory.Path };
            foreach (var argument in new[] { "--RootDirectory", root, "--PackagePath", package, "--Sha256", hash,
                "--Version", "1.0.2", "--ParentProcessId", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture) }) info.ArgumentList.Add(argument);
            using var process = Process.Start(info)!;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                if (!process.HasExited) Assert.Equal(IntPtr.Zero, process.MainWindowHandle);
                await process.WaitForExitAsync(timeout.Token);
                Assert.NotEqual(0, process.ExitCode);
                foreach (var item in old) Assert.Equal(item.Value, File.ReadAllText(Path.Combine(root, item.Key)));
                Assert.False(File.Exists(Path.Combine(root, "added.dll")));
                using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "package-manifest.json")));
                Assert.Equal("1.0.1", manifest.RootElement.GetProperty("version").GetString());
                foreach (var name in new[] { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE" })
                    Assert.Equal("user-" + name, File.ReadAllText(Path.Combine(root, name, "keep.bin")));
                Assert.True(File.Exists(Path.Combine(root, "LOGS", "update.log")));
                Assert.False(File.Exists(Path.Combine(root, "DATA", "UserData.db")));
            }
            finally
            {
                // 仅在测试超时时停止此测试刚创建的子进程，不能影响真实应用或父进程。
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(TestContext.Current.CancellationToken); }
            }
        }

        /// <summary>使用当前测试配置构建出的真实更新器，不硬编码用户工作区绝对路径。</summary>
        private static string FindRunner()
        {
            var location = new DirectoryInfo(AppContext.BaseDirectory);
            var configuration = location.Parent!.Name;
            while (location is not null && !File.Exists(Path.Combine(location.FullName, "Dictation.sln"))) location = location.Parent;
            if (location is null) throw new InvalidOperationException("实际进程测试需要仓库构建目录。");
            return Path.Combine(location.FullName, "Dictation.Updater", "bin", configuration, "net10.0-windows", "Dictation.Updater.exe");
        }
    }
}
