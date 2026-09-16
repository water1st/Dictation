using System.IO.Pipes;
using Dictation.Core.Tts;

namespace Dictation.TtsHost
{
    /// <summary>
    /// 串行接收命名管道请求，并按语言路由到同一 DI 容器中的合成引擎。
    /// </summary>
    internal sealed class TtsPipeServer : IDisposable
    {
        private readonly TtsHostSession session;
        private readonly IReadOnlyDictionary<TtsLanguage, ITtsSynthesisEngine> engines;
        private readonly CancellationTokenSource stop = new();
        private int disposed;

        public TtsPipeServer(
            TtsHostSession session,
            IEnumerable<ITtsSynthesisEngine> engines)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            ArgumentNullException.ThrowIfNull(engines);
            this.engines = BuildEngineMap(engines);
        }

        /// <summary>
        /// 持续处理客户端连接，直到收到关闭请求或取消信号。
        /// </summary>
        internal async Task RunAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                stop.Token);
            var runToken = linkedCancellation.Token;

            while (!runToken.IsCancellationRequested)
            {
                await using var pipe = new NamedPipeServerStream(
                    session.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                try
                {
                    await pipe.WaitForConnectionAsync(runToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (runToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (runToken.IsCancellationRequested)
                {
                    break;
                }

                var shutdownRequested = await HandleClientAsync(pipe, runToken).ConfigureAwait(false);
                if (shutdownRequested)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            stop.Cancel();
            stop.Dispose();
        }

        private async Task<bool> HandleClientAsync(
            NamedPipeServerStream pipe,
            CancellationToken cancellationToken)
        {
            TtsPipeRequest request;
            try
            {
                request = await TtsPipeCodec.ReadRequestAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await TryWriteErrorAsync(pipe, exception, cancellationToken).ConfigureAwait(false);
                return false;
            }

            switch (request.Type)
            {
                case TtsPipeMessageType.Ping:
                    await TryWriteResponseAsync(
                        pipe,
                        new TtsPipeResponse(TtsPipeMessageType.Pong),
                        cancellationToken).ConfigureAwait(false);
                    return false;

                case TtsPipeMessageType.Shutdown:
                    await TryWriteResponseAsync(
                        pipe,
                        new TtsPipeResponse(TtsPipeMessageType.ShutdownAcknowledged),
                        cancellationToken).ConfigureAwait(false);
                    return true;

                case TtsPipeMessageType.Synthesize:
                    return await HandleSynthesisAsync(pipe, request, cancellationToken).ConfigureAwait(false);

                default:
                    await TryWriteErrorAsync(
                        pipe,
                        new TtsPipeProtocolException("请求消息类型无效。"),
                        cancellationToken).ConfigureAwait(false);
                    return false;
            }
        }

        private async Task<bool> HandleSynthesisAsync(
            NamedPipeServerStream pipe,
            TtsPipeRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Language is null || request.Text is null)
            {
                await TryWriteErrorAsync(
                    pipe,
                    new TtsPipeProtocolException("合成请求缺少语言或文本。"),
                    cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (!engines.TryGetValue(request.Language.Value, out var engine))
            {
                await TryWriteErrorAsync(
                    pipe,
                    new InvalidOperationException($"TTS 不支持语言 {request.Language.Value}，尚未注册对应引擎。"),
                    cancellationToken).ConfigureAwait(false);
                return false;
            }

            try
            {
                var audio = await engine.SynthesizeAsync(
                    new TtsSynthesisRequestDto(request.Language.Value, request.Text),
                    cancellationToken).ConfigureAwait(false);
                if (audio is null)
                {
                    throw new InvalidOperationException("TTS 引擎未返回 WAV 数据。");
                }

                await TryWriteResponseAsync(
                    pipe,
                    new TtsPipeResponse(TtsPipeMessageType.Audio, audio),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await TryWriteErrorAsync(pipe, exception, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        private static IReadOnlyDictionary<TtsLanguage, ITtsSynthesisEngine> BuildEngineMap(
            IEnumerable<ITtsSynthesisEngine> registeredEngines)
        {
            var map = new Dictionary<TtsLanguage, ITtsSynthesisEngine>();
            foreach (var engine in registeredEngines)
            {
                ArgumentNullException.ThrowIfNull(engine);
                var supportedLanguages = engine.SupportedLanguages
                    ?? throw new ArgumentException("TTS 引擎必须声明支持的语言。", nameof(registeredEngines));
                foreach (var language in supportedLanguages)
                {
                    if (!Enum.IsDefined(language))
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(registeredEngines),
                            language,
                            "TTS 引擎声明了未定义的语言。");
                    }

                    if (!map.TryAdd(language, engine))
                    {
                        throw new ArgumentException(
                            $"TTS 语言 {language} 被多个引擎重复注册。",
                            nameof(registeredEngines));
                    }
                }
            }

            return map;
        }

        private static async Task TryWriteResponseAsync(
            NamedPipeServerStream pipe,
            TtsPipeResponse response,
            CancellationToken cancellationToken)
        {
            try
            {
                await TtsPipeCodec.WriteResponseAsync(pipe, response, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // 客户端可能已断开；当前请求结束后继续监听下一个客户端。
            }
        }

        private static Task TryWriteErrorAsync(
            NamedPipeServerStream pipe,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? "TTS Host 处理请求失败。"
                : exception.Message;
            return TryWriteResponseAsync(
                pipe,
                new TtsPipeResponse(
                    TtsPipeMessageType.Error,
                    Error: $"TTS Host 处理请求失败：{message}"),
                cancellationToken);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
        }
    }
}
