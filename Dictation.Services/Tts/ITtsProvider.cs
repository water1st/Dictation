namespace Dictation.Services.Tts
{
    public interface ITtsProvider
    {
        Task<TtsAudioDto> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
