namespace Dictation.Services.Tts
{
    public sealed record TtsSynthesisRequestDto(TtsLanguage Language, string Text);
}
