namespace Dictation.Presentation.Audio
{
    /// <summary>
    /// 将内存中的 WAV 数据播放到本机音频输出，隔离播放器实现。
    /// </summary>
    public interface IAudioPlayer
    {
        /// <summary>
        /// 播放完整的内存 WAV 音频，取消时停止当前播放操作。
        /// </summary>
        /// <param name="wavData">包含 WAV 头及采样数据的完整内存音频。</param>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        Task PlayWavAsync(ReadOnlyMemory<byte> wavData, CancellationToken cancellationToken = default);
    }
}
