namespace Dictation.Core.Tts
{
    /// <summary>
    /// 定义业务层可替换的语音合成提供能力。
    /// </summary>
    public interface ITtsProvider
    {
        /// <summary>
        /// 根据语言与文本生成内存中的 WAV 音频。
        /// </summary>
        /// <param name="request">包含语言和文本的合成请求。</param>
        /// <param name="cancellationToken">用于中止合成的取消标记。</param>
        /// <returns>内存中的 WAV 音频。</returns>
        Task<TtsAudioDto> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
