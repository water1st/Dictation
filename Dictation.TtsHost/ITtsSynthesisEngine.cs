using Dictation.Services.Tts;

namespace Dictation.TtsHost
{
    public interface ITtsSynthesisEngine
    {
        IReadOnlyCollection<TtsLanguage> SupportedLanguages { get; }

        Task<byte[]> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
