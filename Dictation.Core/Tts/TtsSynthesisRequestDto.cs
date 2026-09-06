namespace Dictation.Core.Tts
{
    public sealed record TtsSynthesisRequestDto(TtsLanguage Language, string Text);
}
