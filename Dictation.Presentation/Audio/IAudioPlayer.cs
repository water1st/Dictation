namespace Dictation.Presentation.Audio
{
    public interface IAudioPlayer
    {
        Task PlayWavAsync(ReadOnlyMemory<byte> wavData, CancellationToken cancellationToken = default);
    }
}
