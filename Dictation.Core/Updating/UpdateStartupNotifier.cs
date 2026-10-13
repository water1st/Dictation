using Dictation.DataAccess.Runtime;

namespace Dictation.Core.Updating
{
    /// <summary>验证并写入此新版安装所属的就绪标记，普通启动没有更新副作用。</summary>
    internal sealed class UpdateStartupNotifier : IUpdateStartupNotifier
    {
        private readonly ApplicationPaths paths;
        private readonly string? readyFile;
        /// <summary>组合固定根目录与唯一配置容器中由更新器传入的标记路径。</summary>
        /// <param name="paths">应用根目录。</param>
        /// <param name="readyFile">更新器传入的就绪标记，没有更新时为空。</param>
        internal UpdateStartupNotifier(ApplicationPaths paths, string? readyFile)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            this.readyFile = readyFile;
        }

        /// <inheritdoc />
        public void NotifyReady()
        {
            if (string.IsNullOrWhiteSpace(readyFile)) return;
            if (!Path.IsPathFullyQualified(readyFile) || Path.GetFileName(readyFile) != "startup.ready")
                throw new InvalidDataException("启动就绪标记不是有效的更新事务路径。");
            var installed = UpdatePackageValidator.ReadInstalled(paths.Root);
            var transaction = Path.GetDirectoryName(readyFile)!;
            UpdateRecoveryStore.Read(new(paths.Root, transaction, installed.Version));
            UpdatePackageValidator.EnsureNoLinks(paths.Root, Path.GetRelativePath(paths.Root, readyFile));
            File.WriteAllText(readyFile, "ready");
        }
    }
}
