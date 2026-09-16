namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示 TTS Provider 的连接、传输或远端处理失败。
    /// </summary>
    public sealed class TtsProviderException : Exception
    {
        public TtsProviderException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
