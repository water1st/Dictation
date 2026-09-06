using Dictation.Core.Configuration;

namespace Dictation.Core.Lexicon
{
    public sealed record LexiconSearchRequestDto(
        LearningLanguage Language,
        string Query,
        int Limit = 20);
}
