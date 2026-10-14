namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示业务层语音合成失败或 Provider 返回了无效音频。
    /// </summary>
    public sealed class TtsSynthesisException : Exception
    {
        /// <summary>
        /// 记录失败说明及原始原因。
        /// </summary>
        /// <param name="message">供上层展示或记录的失败说明。</param>
        /// <param name="innerException">保留的底层失败原因。</param>
        public TtsSynthesisException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
