using System.ComponentModel;
using System.Diagnostics;
using Dictation.Services.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.TtsHost
{
    internal static class Program
    {
        [STAThread]
        public static async Task<int> Main(string[] args)
        {
            if (!TtsHostOptions.TryParse(args, out var options) || options is null)
            {
                return 1;
            }

            try
            {
                using var services = new ServiceCollection()
                    .AddSingleton(options.Session)
                    .AddSingleton<ITtsSynthesisEngine, UnconfiguredTtsSynthesisEngine>()
                    .AddSingleton<TtsPipeServer>()
                    .BuildServiceProvider();
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
