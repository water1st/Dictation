namespace Dictation.Core
{
    internal interface ILanguageSettableTTSPlayer : ITTSPlayer
    {
        void SetLanguage(string language);
    }
}
