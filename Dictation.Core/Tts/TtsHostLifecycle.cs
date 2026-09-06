using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using Microsoft.Extensions.Hosting;

namespace Dictation.Core.Tts
{
    public sealed class TtsHostLifecycle : IHostedService, IDisposable
    {
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

        private readonly string executablePath;
        private readonly TtsHostSession session;
        private Process? process;
        private int disposed;

        public TtsHostLifecycle(string executablePath, TtsHostSession session)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("TTS Host 可执行文件路径不能为空。", nameof(executablePath));
            }

            this.executablePath = executablePath;
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public TtsHostSession Session => session;

        public int? ProcessId => process?.Id;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
            if (process is not null && !process.HasExited)
            {
                return;
            }

            process?.Dispose();
            process = null;

            if (!File.Exists(executablePath))
            {
                throw new InvalidOperationException("TTS Host 可执行文件不存在。");
            }

            process = StartProcess();
            try
            {
                await WaitForReadyAsync(process, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                }

                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var startedProcess = process;
            if (startedProcess is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            try
            {
                if (!startedProcess.HasExited)
                {
                    try
                    {
                        await TrySendControlAsync(
                            new TtsPipeRequest(TtsPipeMessageType.Shutdown),
                            cancellationToken).ConfigureAwait(false);
                        await WaitForExitAsync(startedProcess, StopTimeout, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    }
                    catch
                    {
                        // 控制通信失败时继续执行有界终止兜底。
                    }
                }
            }
            finally
            {
                try
                {
                    if (!startedProcess.HasExited)
                    {
                        TryKill(startedProcess);
                        await WaitForExitAsync(startedProcess, ControlTimeout, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                }
                finally
                {
                    process = null;
                    startedProcess.Dispose();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                var startedProcess = process;
                if (startedProcess is not null)
                {
                    TryKill(startedProcess);
                    process = null;
                    startedProcess.Dispose();
                }
            }
        }

        private Process StartProcess()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("--pipe");
            startInfo.ArgumentList.Add(session.PipeName);
            startInfo.ArgumentList.Add("--parent-process-id");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

            try
            {
                return Process.Start(startInfo)
                    ?? throw new InvalidOperationException("TTS Host 进程未能启动。");
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException("无法启动 TTS Host。", exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException("无法启动 TTS Host。", exception);
            }
        }

        private async Task WaitForReadyAsync(Process startedProcess, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < StartupTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (startedProcess.HasExited)
                {
                    throw new InvalidOperationException("TTS Host 启动后提前退出。");
                }

                var response = await TrySendControlAsync(
                    new TtsPipeRequest(TtsPipeMessageType.Ping),
                    cancellationToken).ConfigureAwait(false);
                if (response?.Type == TtsPipeMessageType.Pong)
                {
                    return;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException("TTS Host 在 10 秒内未通过 Ping/Pong 就绪。");
        }

        private async Task<TtsPipeResponse?> TrySendControlAsync(
            TtsPipeRequest request,
            CancellationToken cancellationToken)
        {
            using var controlCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            controlCancellation.CancelAfter(ControlTimeout);
            try
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    session.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await client.ConnectAsync(controlCancellation.Token).ConfigureAwait(false);
                await TtsPipeCodec.WriteRequestAsync(client, request, controlCancellation.Token)
                    .ConfigureAwait(false);
                return await TtsPipeCodec.ReadResponseAsync(client, controlCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (UnauthorizedAccessException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (TtsPipeProtocolException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        private static async Task<bool> WaitForExitAsync(
            Process startedProcess,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            try
            {
                await startedProcess.WaitForExitAsync(timeoutCancellation.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        private static void TryKill(Process startedProcess)
        {
            try
            {
                if (!startedProcess.HasExited)
                {
                    startedProcess.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }
        }

    }
}
