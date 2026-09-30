using Dictation.Core.Configuration;
using Dictation.Core.Lexicon;
using System.Text;

namespace Dictation.Core.Importing
{
    /// <summary>
    /// 组合已有离线词库检索能力，为批量文本导入准备候选。
    /// </summary>
    internal sealed class TextImportPreparationService : ITextImportPreparation
    {
        private readonly ILexiconSearch lexiconSearch;

        /// <summary>
        /// 创建只查询词库、不写导入收件箱的文本准备服务。
        /// </summary>
        /// <param name="lexiconSearch">已有的日英语离线正向查词能力。</param>
        public TextImportPreparationService(ILexiconSearch lexiconSearch)
        {
            this.lexiconSearch = lexiconSearch ?? throw new ArgumentNullException(nameof(lexiconSearch));
        }

        /// <inheritdoc />
        public async Task<TextImportPreparationDto> PrepareAsync(
            LearningLanguage language,
            string text,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "不支持的学习语言。");
            }

            ArgumentNullException.ThrowIfNull(text);
            cancellationToken.ThrowIfCancellationRequested();

            var lines = new List<TextImportLineDto>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            using var reader = new StringReader(text);
            var lineNumber = 0;
            while (reader.ReadLine() is { } rawLine)
            {
                lineNumber++;
                cancellationToken.ThrowIfCancellationRequested();
                var sourceText = rawLine.Trim().Normalize(NormalizationForm.FormC);
                if (sourceText.Length == 0 || sourceText.StartsWith('#'))
                {
                    continue;
                }

                var query = language == LearningLanguage.English
                    ? sourceText.ToLowerInvariant()
                    : sourceText;
                if (!seen.Add(query))
                {
                    continue;
                }

                var results = await lexiconSearch.SearchAsync(
                    new LexiconSearchRequestDto(language, query, 50),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var candidates = results
                    .Where(result => result.Language == language && IsExactMatch(result, query, language))
                    .DistinctBy(result => result.SourceSenseId, StringComparer.Ordinal)
                    .ToArray();
                lines.Add(new TextImportLineDto(lineNumber, sourceText, candidates));
            }

            return new TextImportPreparationDto(language, lines);
        }

        private static bool IsExactMatch(
            LexiconLookupResultDto result,
            string query,
            LearningLanguage language)
        {
            var comparison = language == LearningLanguage.English
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(result.Headword.Normalize(NormalizationForm.FormC), query, comparison)
                || result.Forms.Any(form => string.Equals(
                    form.Surface.Normalize(NormalizationForm.FormC),
                    query,
                    comparison));
        }
    }
}
