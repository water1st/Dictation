using System.ComponentModel;
using System.Diagnostics;
using Dictation.Core.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.TtsHost
{
    /// <summary>
    /// 配置独立 TTS 宿主进程，并在父进程退出或收到关闭请求时结束服务。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 运行独立 TTS 进程，监听父进程退出并在结束时释放宿主容器。
        /// </summary>
        /// <param name="args">管道名称、父进程标识等宿主启动参数。</param>
        /// <returns>正常结束为0，参数无效或服务失败为1。</returns>
        [STAThread]
        public static async Task<int> Main(string[] args)
        {
            if (!TtsHostOptions.TryParse(args, out var options) || options is null)
            {
                return 1;
            }

            try
            {
                using var services = BuildServiceProvider(options);
                var server = services.GetRequiredService<TtsPipeServer>();
                using var parentCancellation = new CancellationTokenSource();
                var parentMonitor = MonitorParentProcessAsync(options.ParentProcessId, parentCancellation);
                try
                {
                    await server.RunAsync(parentCancellation.Token).ConfigureAwait(false);
                }
                finally
                {
                    parentCancellation.Cancel();
                    try
                    {
                        await parentMonitor.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (parentCancellation.IsCancellationRequested)
                    {
                    }
                }

                return 0;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// 创建独立进程唯一的宿主容器；不在主应用进程中建立第二业务容器。
        /// </summary>
        /// <param name="options">已解析的宿主会话及父进程信息。</param>
        /// <returns>由进程入口负责释放的服务提供者。</returns>
        internal static ServiceProvider BuildServiceProvider(TtsHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return new ServiceCollection()
                .AddSingleton(options.Session)
                .AddSingleton<ITtsSynthesisEngine, PiperJapaneseTtsSynthesisEngine>()
                .AddSingleton<ITtsSynthesisEngine, KokoroEnglishTtsSynthesisEngine>()
                .AddSingleton<TtsPipeServer>()
                .BuildServiceProvider();
        }

        private static async Task MonitorParentProcessAsync(
            int parentProcessId,
            CancellationTokenSource stop)
        {
            Process parentProcess;
            try
            {
                parentProcess = Process.GetProcessById(parentProcessId);
            }
            catch (ArgumentException)
            {
                stop.Cancel();
                return;
            }
            catch (InvalidOperationException)
            {
                stop.Cancel();
                return;
            }
            catch (Win32Exception)
            {
                stop.Cancel();
                return;
            }

            using (parentProcess)
            {
                try
                {
                    await parentProcess.WaitForExitAsync(stop.Token).ConfigureAwait(false);
                    if (!stop.IsCancellationRequested)
                    {
                        stop.Cancel();
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                }
                catch (ArgumentException)
                {
                    stop.Cancel();
                }
                catch (InvalidOperationException)
                {
                    stop.Cancel();
                }
                catch (Win32Exception)
                {
                    stop.Cancel();
                }
            }
        }
    }
}
