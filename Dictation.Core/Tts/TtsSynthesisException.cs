namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示业务层语音合成失败或 Provider 返回了无效音频。
    /// </summary>
    public sealed class TtsSynthesisException : Exception
    {
        public TtsSynthesisException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
