using System.IO.Pipes;

namespace Dictation.Core.Tts
{
    /// <summary>
    /// 通过当前 TTS 宿主会话的命名管道请求语音合成。
    /// </summary>
    internal sealed class NamedPipeTtsProvider : ITtsProvider
    {
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);
        private readonly TtsHostSession session;

        /// <summary>
        /// 通过当前 TTS 宿主会话的命名管道请求语音合成。
        /// </summary>
        /// <param name="session">当前 TTS 宿主会话和管道连接信息。</param>
        public NamedPipeTtsProvider(TtsHostSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <inheritdoc />
        public async Task<TtsAudioDto> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            await using var client = new NamedPipeClientStream(
                ".",
                session.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCancellation.CancelAfter(ConnectionTimeout);
            try
            {
                await client.ConnectAsync(connectionCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TtsProviderException("连接 TTS Host 超时。", null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new TtsProviderException("无法连接 TTS Host。", exception);
            }

            try
            {
                await TtsPipeCodec.WriteRequestAsync(
                    client,
                    new TtsPipeRequest(TtsPipeMessageType.Synthesize, request.Language, request.Text),
                    cancellationToken).ConfigureAwait(false);
                var response = await TtsPipeCodec.ReadResponseAsync(client, cancellationToken).ConfigureAwait(false);

                return response.Type switch
                {
                    TtsPipeMessageType.Audio when response.Payload is not null
                        => new TtsAudioDto(response.Payload),
                    TtsPipeMessageType.Error
                        => throw new TtsProviderException($"TTS Host 返回错误：{response.Error}"),
                    _ => throw new TtsProviderException("TTS Host 返回了未知响应。")
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TtsProviderException)
            {
                throw;
            }
            catch (TtsPipeProtocolException exception)
            {
                throw new TtsProviderException("TTS Host 返回了无效协议数据。", exception);
            }
            catch (Exception exception)
            {
                throw new TtsProviderException("TTS Host 传输失败。", exception);
            }
        }
    }
}
