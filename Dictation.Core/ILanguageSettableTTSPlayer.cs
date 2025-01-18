namespace Dictation.Core
{
    public interface ILanguageSettableTTSPlayer : ITTSPlayer
    {
        void SetLanguage(string language);
    }
}
