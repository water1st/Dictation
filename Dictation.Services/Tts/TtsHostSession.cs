namespace Dictation.Services.Tts
{
    public sealed class TtsHostSession
    {
        public TtsHostSession(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName)
                || pipeName.Length > 128
                || pipeName.Contains('/', StringComparison.Ordinal)
                || pipeName.Contains('\\', StringComparison.Ordinal)
                || pipeName.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException("TTS Host 管道名无效。", nameof(pipeName));
            }

            PipeName = pipeName;
        }

        public string PipeName { get; }
    }
}
