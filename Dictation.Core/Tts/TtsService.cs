namespace Dictation.Core.Tts
{
    public sealed class TtsService
    {
        private const int MaximumTextLength = 500;
        private readonly ITtsProvider provider;

        public TtsService(ITtsProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            this.provider = provider;
        }

        public async Task<TtsAudioDto> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateRequest(request);
            cancellationToken.ThrowIfCancellationRequested();

            TtsAudioDto? audio;
            try
            {
                audio = await provider.SynthesizeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new TtsSynthesisException("TTS Provider 生成 WAV 音频失败。", exception);
            }

            if (!IsValidWav(audio?.WavData))
            {
                throw new TtsSynthesisException("TTS Provider 返回的音频不是有效的 WAV 文件。");
            }

            return audio!;
        }

        private static void ValidateRequest(TtsSynthesisRequestDto request)
        {
            if (!Enum.IsDefined(request.Language))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.Language),
                    request.Language,
                    "TTS 语言未定义。");
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("TTS 文本不能为空或仅包含空白字符。", nameof(request.Text));
            }

            if (request.Text.Length > MaximumTextLength)
            {
                throw new ArgumentException("TTS 文本不能超过 500 个字符。", nameof(request.Text));
            }
        }

        private static bool IsValidWav(byte[]? data)
        {
            return data is { Length: >= 12 }
                && data[0] == (byte)'R'
                && data[1] == (byte)'I'
                && data[2] == (byte)'F'
                && data[3] == (byte)'F'
                && data[8] == (byte)'W'
                && data[9] == (byte)'A'
                && data[10] == (byte)'V'
                && data[11] == (byte)'E';
        }
    }
}
