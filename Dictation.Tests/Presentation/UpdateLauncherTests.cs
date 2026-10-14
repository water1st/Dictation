using System.ComponentModel;
using System.Diagnostics;
using Dictation.Core.Updating;
using Dictation.Presentation.Updating;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证实际进程启动协议及无窗口设置，不依赖shell拼接或模拟不存在的成功进程。</summary>
    public sealed class UpdateLauncherTests
    {
        [Fact]
        public void StartInfo_UsesHiddenIndependentRunnerAndSeparateArgumentsIncludingParentPid()
        {
            using var directory = new TemporaryDirectory();
            var root = Path.Combine(directory.Path, "app with spaces");
            var job = Path.Combine(root, "CACHE", "Updates", "job");
            var runner = Path.Combine(job, "runner", "Dictation.Updater.exe");
            var package = Path.Combine(job, "Dictation-win-x64.zip");
            var hash = new string('A', 64);
            var info = UpdateLauncher.CreateStartInfo(new(job, runner, package, hash, "1.0.2", root), 12345);
            Assert.Equal(runner, info.FileName);
            Assert.Equal(Path.GetDirectoryName(runner), info.WorkingDirectory);
            Assert.False(info.UseShellExecute);
            Assert.True(info.CreateNoWindow);
            Assert.Equal(ProcessWindowStyle.Hidden, info.WindowStyle);
            Assert.Equal(new[] { "--RootDirectory", root, "--PackagePath", package, "--Sha256", hash,
                "--Version", "1.0.2", "--ParentProcessId", "12345" }, info.ArgumentList);
            Assert.Equal(string.Empty, info.Arguments);
        }

        [Fact]
        public void Launch_MissingRunnerReportsNativeFailureInsteadOfClaimingSuccess()
        {
            using var directory = new TemporaryDirectory();
            var runner = Path.Combine(directory.Path, "missing.exe");
            Assert.Throws<Win32Exception>(() => new UpdateLauncher().Launch(new(directory.Path, runner,
                Path.Combine(directory.Path, "package.zip"), new string('A', 64), "1.0.2", directory.Path)));
        }
    }
}
