namespace Dictation.Services.Tts
{
    public sealed class TtsProviderException : Exception
    {
        public TtsProviderException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
