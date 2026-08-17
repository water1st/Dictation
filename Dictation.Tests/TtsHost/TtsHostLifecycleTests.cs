using System.Diagnostics;
using System.IO.Pipes;
using Dictation.Services;
using Dictation.Services.Tts;
using Dictation.TtsHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dictation.Tests.TtsHost
{
    public sealed class TtsHostLifecycleTests
    {
        [Fact]
        public async Task StartAsync_WhenExecutableIsMissing_ThrowsChineseException()
        {
            var session = CreateSession();
            using var lifecycle = new TtsHostLifecycle(
                Path.Combine(AppContext.BaseDirectory, "missing", "Dictation.TtsHost.exe"),
                session);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lifecycle.StartAsync(TestContext.Current.CancellationToken));

            Assert.Contains("不存在", exception.Message);
            Assert.Null(lifecycle.ProcessId);
        }

        [Fact]
        public void AddLocalTtsHost_RegistersOneSessionProviderAndLifecycle()
        {
            using var services = new ServiceCollection()
                .AddLocalTtsHost(GetTtsHostPath())
                .BuildServiceProvider();

            var session = services.GetRequiredService<TtsHostSession>();
            var provider = services.GetRequiredService<NamedPipeTtsProvider>();
            var interfaceProvider = services.GetRequiredService<ITtsProvider>();
            var lifecycle = services.GetRequiredService<TtsHostLifecycle>();
            var hostedService = services.GetRequiredService<IHostedService>();

            Assert.Same(provider, interfaceProvider);
            Assert.Same(lifecycle, hostedService);
            Assert.StartsWith("Dictation.Tts.", session.PipeName, StringComparison.Ordinal);
            Assert.True(Guid.TryParseExact(session.PipeName["Dictation.Tts.".Length..], "N", out _));
        }

        [Fact]
        public void TtsHostOptions_RejectsMissingInvalidAndNonPositiveParentProcessId()
        {
            var pipeName = CreateSession().PipeName;

            Assert.False(TtsHostOptions.TryParse(Array.Empty<string>(), out _));
            Assert.False(TtsHostOptions.TryParse(new[] { "--pipe", pipeName }, out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", pipeName, "--parent-process-id", "not-a-number" },
                out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", pipeName, "--parent-process-id", "0" },
                out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", pipeName, "--parent-process-id", "-1" },
                out _));
        }

        [Fact]
        public async Task RegisteredTtsService_UsesRealHostAndPropagatesSynthesisFailure()
        {
            using var services = new ServiceCollection()
                .AddDictationServices()
                .AddLocalTtsHost(GetTtsHostPath())
                .BuildServiceProvider();
            var lifecycle = services.GetRequiredService<TtsHostLifecycle>();
            var ttsService = services.GetRequiredService<TtsService>();
            var processId = default(int?);

            await lifecycle.StartAsync(TestContext.Current.CancellationToken);
            processId = lifecycle.ProcessId;
            try
            {
                var exception = await Assert.ThrowsAsync<TtsSynthesisException>(() =>
                    ttsService.SynthesizeAsync(
                        new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                        TestContext.Current.CancellationToken));

                Assert.NotNull(exception.InnerException);
                Assert.Contains("英语 TTS 模型尚未配置", exception.InnerException!.Message);
            }
            finally
            {
                await lifecycle.StopAsync(TestContext.Current.CancellationToken);
            }

            Assert.True(processId.HasValue);
            Assert.False(IsProcessRunning(processId.Value));
        }

        [Fact]
        public async Task StartAsync_StartsRealHostAfterPingPongAndStopAsyncWaitsForExit()
        {
            var lifecycle = new TtsHostLifecycle(GetTtsHostPath(), CreateSession());
            using (lifecycle)
            {
                await lifecycle.StartAsync(TestContext.Current.CancellationToken);
                var processId = lifecycle.ProcessId;

                Assert.True(processId.HasValue);
                Assert.Equal(
                    TtsPipeMessageType.Pong,
                    (await SendControlAsync(lifecycle.Session, TtsPipeMessageType.Ping)).Type);

                await lifecycle.StopAsync(TestContext.Current.CancellationToken);

                Assert.False(IsProcessRunning(processId.Value));
                Assert.Null(lifecycle.ProcessId);
            }
        }

        [Fact]
        public async Task StopAsync_IsIdempotentAfterNormalShutdown()
        {
            using var lifecycle = new TtsHostLifecycle(GetTtsHostPath(), CreateSession());
            await lifecycle.StartAsync(TestContext.Current.CancellationToken);

            await lifecycle.StopAsync(TestContext.Current.CancellationToken);
            await lifecycle.StopAsync(TestContext.Current.CancellationToken);

            Assert.Null(lifecycle.ProcessId);
        }

        [Fact]
        public async Task Dispose_WhenHostIsRunningStopsTheStartedProcess()
        {
            var lifecycle = new TtsHostLifecycle(GetTtsHostPath(), CreateSession());
            await lifecycle.StartAsync(TestContext.Current.CancellationToken);
            var processId = lifecycle.ProcessId;

            lifecycle.Dispose();

            Assert.True(processId.HasValue);
            Assert.False(IsProcessRunning(processId.Value));
        }

        [Fact]
        public async Task TtsHost_WhenParentProcessExitsStopsAutomatically()
        {
            Process? parent = null;
            Process? host = null;
            try
            {
                parent = StartParentProcess();
                var session = CreateSession();
                host = StartTtsHostProcess(session, parent.Id);

                Assert.Equal(TtsPipeMessageType.Pong, (await SendControlAsync(session, TtsPipeMessageType.Ping)).Type);

                parent.Kill();
                await parent.WaitForExitAsync(TestContext.Current.CancellationToken);
                await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

                Assert.True(host.HasExited);
            }
            finally
            {
                StopProcess(host);
                StopProcess(parent);
            }
        }

        private static string GetTtsHostPath()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Dictation.TtsHost.exe");
            Assert.True(File.Exists(path), $"测试输出目录缺少 {path}。");
            return path;
        }

        private static TtsHostSession CreateSession()
        {
            return new TtsHostSession($"Dictation.Tests.Tts.{Guid.NewGuid():N}");
        }

        private static async Task<TtsPipeResponse> SendControlAsync(
            TtsHostSession session,
            TtsPipeMessageType messageType)
        {
            await using var client = new NamedPipeClientStream(
                ".",
                session.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(1000, TestContext.Current.CancellationToken);
            await TtsPipeCodec.WriteRequestAsync(
                client,
                new TtsPipeRequest(messageType),
                TestContext.Current.CancellationToken);
            return await TtsPipeCodec.ReadResponseAsync(client, TestContext.Current.CancellationToken);
        }

        private static Process StartParentProcess()
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                ArgumentList = { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 60" }
            });
            Assert.NotNull(process);
            return process;
        }

        private static Process StartTtsHostProcess(TtsHostSession session, int parentProcessId)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = GetTtsHostPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                ArgumentList =
                {
                    "--pipe",
                    session.PipeName,
                    "--parent-process-id",
                    parentProcessId.ToString()
                }
            });
            Assert.NotNull(process);
            return process;
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void StopProcess(Process? process)
        {
            if (process is null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
