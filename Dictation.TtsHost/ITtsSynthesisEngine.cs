using Dictation.Core.Tts;

namespace Dictation.TtsHost
{
    /// <summary>
    /// 定义 TTS 宿主内部可组合的语言合成引擎能力。
    /// </summary>
    internal interface ITtsSynthesisEngine
    {
        /// <summary>
        /// 获取该引擎唯一负责的语言集合。
        /// </summary>
        IReadOnlyCollection<TtsLanguage> SupportedLanguages { get; }

        /// <summary>
        /// 按需加载模型并返回内存中的 WAV 音频。
        /// </summary>
        /// <param name="request">语言必须属于 <see cref="SupportedLanguages"/>，文本必须非空。</param>
        /// <param name="cancellationToken">在模型推理前检查并传播的取消标记。</param>
        /// <returns>仅驻留内存、由调用方消费的完整 WAV 字节；引擎不持久化音频。</returns>
        Task<byte[]> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
