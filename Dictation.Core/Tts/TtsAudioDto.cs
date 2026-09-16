namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示仅驻留内存的 WAV 音频结果。
    /// </summary>
    public sealed record TtsAudioDto(byte[] WavData);
}
