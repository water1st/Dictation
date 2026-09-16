namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示主进程与 TTS 宿主共享的单次命名管道会话。
    /// </summary>
    public sealed class TtsHostSession
    {
        /// <summary>
        /// 创建经过长度与路径字符约束验证的管道会话。
        /// </summary>
        /// <param name="pipeName">当前会话的唯一管道名。</param>
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
