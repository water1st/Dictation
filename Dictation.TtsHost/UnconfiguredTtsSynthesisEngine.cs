using Dictation.Services.Tts;

namespace Dictation.TtsHost
{
    public sealed class UnconfiguredTtsSynthesisEngine : ITtsSynthesisEngine
    {
        private static readonly IReadOnlyCollection<TtsLanguage> Languages =
            Array.AsReadOnly(new[] { TtsLanguage.Japanese, TtsLanguage.English });

        public IReadOnlyCollection<TtsLanguage> SupportedLanguages => Languages;

        public Task<byte[]> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<byte[]>(new InvalidOperationException("TTS 模型尚未配置。"));
        }
    }
}
