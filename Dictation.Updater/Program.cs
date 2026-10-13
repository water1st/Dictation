using System.Diagnostics;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dictation.Updater
{
    /// <summary>在独占运行目录使用同一个Host与Core安装能力执行隐藏更新。</summary>
    internal static class Program
    {
        /// <summary>从微软命令行配置接收父PID与已准备安装条件，等待退出后更新并确认重启。</summary>
        /// <param name="args">由主程序使用ArgumentList传入的启动条件。</param>
        /// <returns>新版启动并确认成功为0，否则为1。</returns>
        private static async Task<int> Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            var root = builder.Configuration["RootDirectory"];
            // 错误参数不能在驱动器根目录、源码目录或目录链接后面创建日志。
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) return 1;
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (root == Path.GetPathRoot(root) || !Directory.Exists(root)
                || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0
                || !File.Exists(Path.Combine(root, "Dictation.Presentation.exe"))
                || !File.Exists(Path.Combine(root, "package-manifest.json"))) return 1;
            builder.Services.AddDictationDataAccess(root).AddDictationCore();
            using var host = builder.Build();
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Dictation.Updater");
            UpdateInstallationDto? receipt = null;
            Process? newApplication = null;
            var parentExited = false;
            try
            {
                var request = builder.Configuration.Get<ApplyUpdateRequestDto>()
                    ?? throw new ArgumentException("更新器缺少安装条件。");
                var parentId = builder.Configuration.GetValue<int>("ParentProcessId");
                if (parentId <= 0 || parentId == Environment.ProcessId) throw new ArgumentException("父进程标识无效。");
                await host.StartAsync();
                await WaitForParentAsync(parentId);
                parentExited = true;
                // 不解析StartupInitializer，不迁移或直接写用户数据库，全部安装业务复用Core。
                receipt = await host.Services.GetRequiredService<IUpdateInstaller>().ApplyAsync(request);
                var readyFile = Path.Combine(receipt.TransactionDirectory, "startup.ready");
                newApplication = StartApplication(root, readyFile);
                await WaitForReadyAsync(newApplication, readyFile);
                try { await host.Services.GetRequiredService<IUpdateCommit>().CommitAsync(receipt); }
                catch (Exception cleanupFailure)
                {
                    // 新版已加载就绪，清理失败只保留旧备份，不能回滚正在运行的新版。
                    WriteFailure(root, cleanupFailure, logger);
                }
                return 0;
            }
            catch (Exception failure)
            {
                WriteFailure(root, failure, logger);
                if (newApplication is not null && !newApplication.HasExited)
                {
                    // 只停止此更新器刚启动且未确认就绪的新版，绝不强杀原应用。
                    newApplication.Kill(entireProcessTree: true);
                    await newApplication.WaitForExitAsync();
                }
                if (receipt is not null)
                {
                    try { await host.Services.GetRequiredService<IUpdateRollback>().RollbackAsync(receipt); }
                    catch (Exception rollbackFailure)
                    {
                        WriteFailure(root, rollbackFailure, logger);
                        return 1;
                    }
                }
                if (parentExited)
                {
                    try { using var previous = StartApplication(root, readyFile: null); }
                    catch (Exception restartFailure) { WriteFailure(root, restartFailure, logger); }
                }
                return 1;
            }
            finally
            {
                newApplication?.Dispose();
                await host.StopAsync();
            }
        }

        /// <summary>等待原应用正常退出；已退出的PID不视为安装错误。</summary>
        /// <param name="parentId">由原应用传入的进程标识。</param>
        private static async Task WaitForParentAsync(int parentId)
        {
            Process parent;
            try { parent = Process.GetProcessById(parentId); }
            catch (ArgumentException) { return; }
            using (parent) await parent.WaitForExitAsync();
        }

        /// <summary>启动主程序；仅新版通过子进程环境接收限定事务的就绪路径。</summary>
        /// <param name="root">原应用根目录。</param>
        /// <param name="readyFile">新版就绪标记，重启旧版时为空。</param>
        /// <returns>本次创建的主程序进程，调用方拥有句柄。</returns>
        private static Process StartApplication(string root, string? readyFile)
        {
            var info = new ProcessStartInfo(Path.Combine(root, "Dictation.Presentation.exe"))
                { UseShellExecute = false, WorkingDirectory = root };
            info.Environment.Remove("DICTATION_UPDATE_READY_FILE");
            if (readyFile is not null) info.Environment["DICTATION_UPDATE_READY_FILE"] = readyFile;
            return Process.Start(info) ?? throw new InvalidOperationException("无法启动主程序。");
        }

        /// <summary>新版需完成迁移、Host和窗口Loaded才就绪，提前退出或超时触发恢复。</summary>
        /// <param name="application">由本更新器创建的新版进程。</param>
        /// <param name="readyFile">此安装事务的唯一就绪路径。</param>
        private static async Task WaitForReadyAsync(Process application, string readyFile)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            while (true)
            {
                if (application.HasExited) throw new InvalidOperationException("新版在完成启动前退出。");
                if (File.Exists(readyFile) && await File.ReadAllTextAsync(readyFile, timeout.Token) == "ready") return;
                await Task.Delay(250, timeout.Token);
            }
        }

        /// <summary>使用同一日志目录记录安装或恢复错误，不让日志写入失败掩盖原错误。</summary>
        /// <param name="root">已存在且非链接的发行根目录。</param>
        /// <param name="failure">实际错误。</param>
        /// <param name="logger">Host创建的标准日志能力。</param>
        private static void WriteFailure(string root, Exception failure, ILogger logger)
        {
            logger.LogError(failure, "更新未完成，保留恢复证据。");
            try
            {
                var directory = Path.Combine(root, "LOGS");
                if (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
                Directory.CreateDirectory(directory);
                var file = Path.Combine(directory, "update.log");
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) return;
                File.AppendAllText(file, $"{DateTimeOffset.UtcNow:O} {failure}{Environment.NewLine}");
            }
            catch (Exception loggingFailure) when (loggingFailure is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(loggingFailure, "无法写入更新日志，旧备份仍按原流程保留。");
            }
        }
    }
}
