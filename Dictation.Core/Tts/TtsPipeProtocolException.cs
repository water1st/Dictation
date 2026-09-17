namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示管道帧不符合 TTS 字节协议约束。
    /// </summary>
    public sealed class TtsPipeProtocolException : Exception
    {
        /// <summary>
        /// 记录失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        public TtsPipeProtocolException(string message) : base(message) { }

        /// <summary>
        /// 记录失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public TtsPipeProtocolException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
