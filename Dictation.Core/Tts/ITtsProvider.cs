namespace Dictation.Core.Tts
{
    public interface ITtsProvider
    {
        Task<TtsAudioDto> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
