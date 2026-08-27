using Dictation.Services.Configuration;

namespace Dictation.Services.Lexicon
{
    public sealed record LexiconSearchRequestDto(
        LearningLanguage Language,
        string Query,
        int Limit = 20);
}
