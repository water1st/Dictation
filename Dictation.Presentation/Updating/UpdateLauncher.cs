using System.Diagnostics;
using Dictation.Core.Updating;

namespace Dictation.Presentation.Updating
{
    /// <summary>启动已准备的独占更新器；不负责安装业务，不主动关闭窗口。</summary>
    internal sealed class UpdateLauncher : IUpdateLauncher
    {
        /// <summary>供唯一DI容器创建无状态进程适配器。</summary>
        public UpdateLauncher() { }

        /// <inheritdoc />
        public void Launch(PreparedUpdateDto update)
        {
            using var process = Process.Start(CreateStartInfo(update, Environment.ProcessId));
            if (process is null) throw new InvalidOperationException("无法启动独立更新器。");
        }

        /// <summary>构建无shell且隐藏的独占runner启动条件。</summary>
        /// <param name="update">Core准备完成的更新。</param>
        /// <param name="parentProcessId">需要等待正常退出的原应用进程。</param>
        internal static ProcessStartInfo CreateStartInfo(PreparedUpdateDto update, int parentProcessId)
        {
            ArgumentNullException.ThrowIfNull(update);
            var info = new ProcessStartInfo(update.RunnerPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(update.RunnerPath)!
            };
            foreach (var argument in new[] { "--RootDirectory", update.RootDirectory, "--PackagePath", update.PackagePath,
                "--Sha256", update.Sha256, "--Version", update.Version, "--ParentProcessId", parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) })
                info.ArgumentList.Add(argument);
            return info;
        }
    }
}
