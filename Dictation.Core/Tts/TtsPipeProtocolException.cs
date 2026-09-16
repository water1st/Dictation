namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示管道帧不符合 TTS 字节协议约束。
    /// </summary>
    public sealed class TtsPipeProtocolException : Exception
    {
        public TtsPipeProtocolException(string message) : base(message) { }

        public TtsPipeProtocolException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
