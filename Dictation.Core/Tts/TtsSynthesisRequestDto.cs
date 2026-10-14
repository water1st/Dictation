namespace Dictation.Core.Tts
{
    /// <summary>
    /// 表示一次语音合成所需的语言与文本。
    /// </summary>
    public sealed record TtsSynthesisRequestDto(TtsLanguage Language, string Text);
}
