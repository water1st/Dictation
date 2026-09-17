namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示 TTS Provider 的连接、传输或远端处理失败。
    /// </summary>
    public sealed class TtsProviderException : Exception
    {
        /// <summary>
        /// 记录失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public TtsProviderException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
