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
        Task<byte[]> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
