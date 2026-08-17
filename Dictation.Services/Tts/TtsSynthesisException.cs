namespace Dictation.Services.Tts
{
    public sealed class TtsSynthesisException : Exception
    {
        public TtsSynthesisException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
