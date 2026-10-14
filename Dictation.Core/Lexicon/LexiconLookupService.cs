using Dictation.Core.Configuration;
using Dictation.DataAccess.Lexicon;
using System.Text;

namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 编排日语和英语词库查询，并对外提供检索与整组解析能力。
    /// </summary>
    internal sealed class LexiconLookupService : ILexiconSearch, ILexiconImportGroupResolver
    {
        private readonly JapaneseLexiconDataAccessor japaneseAccessor;
        private readonly EnglishLexiconDataAccessor englishAccessor;

        /// <summary>
        /// 使用两种词库的数据访问器创建查询服务。
        /// </summary>
        /// <param name="japaneseAccessor">日语词库数据访问器。</param>
        /// <param name="englishAccessor">英语词库数据访问器。</param>
        public LexiconLookupService(
            JapaneseLexiconDataAccessor japaneseAccessor,
            EnglishLexiconDataAccessor englishAccessor)
        {
            this.japaneseAccessor = japaneseAccessor
                ?? throw new ArgumentNullException(nameof(japaneseAccessor));
            this.englishAccessor = englishAccessor
                ?? throw new ArgumentNullException(nameof(englishAccessor));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LexiconLookupResultDto>> SearchAsync(
            LexiconSearchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var normalizedQuery = NormalizeQuery(request.Language, request.Query);
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedQuery);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.Limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Limit, 50);
            ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
            cancellationToken.ThrowIfCancellationRequested();

            return request.Language switch
            {
                LearningLanguage.Japanese => (await japaneseAccessor.SearchAsync(
                    normalizedQuery,
                    request.Limit,
                    cancellationToken,
                    request.Offset,
                    request.ExactOnly)).Select(JapaneseLexiconMapper.Map).ToArray(),
                LearningLanguage.English => (await englishAccessor.SearchAsync(
                    normalizedQuery,
                    request.Limit,
                    cancellationToken,
                    request.Offset,
                    request.ExactOnly)).Select(EnglishLexiconMapper.Map).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Language), request.Language, "不支持的学习语言。")
            };
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LexiconLookupResultDto>> GetImportGroupAsync(
            LearningLanguage language,
            string sourceSenseId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceSenseId);
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(language),
                    language,
                    "不支持的学习语言。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var normalizedSourceSenseId = sourceSenseId.Trim().Normalize(NormalizationForm.FormC);
            return language switch
            {
                LearningLanguage.Japanese => await MapJapaneseImportGroupAsync(
                    normalizedSourceSenseId,
                    cancellationToken),
                LearningLanguage.English => (await englishAccessor.GetSenseGroupAsync(
                    normalizedSourceSenseId,
                    cancellationToken)).Select(EnglishLexiconMapper.Map).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "不支持的学习语言。")
            };
        }

        private static string NormalizeQuery(LearningLanguage language, string query)
        {
            ArgumentNullException.ThrowIfNull(query);
            var normalized = query.Trim().Normalize(NormalizationForm.FormC);
            return language switch
            {
                LearningLanguage.Japanese => normalized,
                LearningLanguage.English => normalized.ToLowerInvariant(),
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, "不支持的学习语言。")
            };
        }

        private async Task<IReadOnlyList<LexiconLookupResultDto>> MapJapaneseImportGroupAsync(
            string sourceSenseId,
            CancellationToken cancellationToken)
        {
            var value = await japaneseAccessor.GetByEntryIdAsync(sourceSenseId, cancellationToken);
            return value is null
                ? Array.Empty<LexiconLookupResultDto>()
                : new[] { JapaneseLexiconMapper.Map(value) };
        }
    }
}
