using Dictation.Core.Configuration;
using Dictation.Core.Lexicon;

namespace Dictation.Core.Learning
{
    /// <summary>从现有离线词库解析英语示例动词的启用范式，供两类计划组合使用。</summary>
    internal sealed class EnglishExampleParadigmResolver
    {
        private readonly ILexiconImportGroupResolver lexicon;

        /// <summary>注入唯一词库的整组解析能力。</summary>
        /// <param name="lexicon">不依赖具体词库实现的整组解析能力。</param>
        public EnglishExampleParadigmResolver(ILexiconImportGroupResolver lexicon)
        {
            this.lexicon = lexicon ?? throw new ArgumentNullException(nameof(lexicon));
        }

        /// <summary>为每个启用词形选择该义项自身的优先、常用形式，缺失时拒绝保存。</summary>
        /// <param name="sourceSenseId">示例动词的来源义项标识。</param>
        /// <param name="enabledForms">当前计划的已校验词形代码。</param>
        /// <param name="cancellationToken">取消词库解析的令牌。</param>
        /// <returns>可用于题面标签且不泄露目标答案的示例范式。</returns>
        internal async Task<EnglishExampleParadigmDto> ResolveAsync(string? sourceSenseId, IReadOnlyList<string> enabledForms, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedId = PracticePlanOptionValidator.NormalizeText(sourceSenseId, "英语示例词义项");
            var group = await lexicon.GetImportGroupAsync(LearningLanguage.English, normalizedId, cancellationToken);
            var member = group.SingleOrDefault(item => string.Equals(item.SourceSenseId, normalizedId, StringComparison.Ordinal));
            if (member is null || member.PartOfSpeechLabel != "动词" || member.IsPhrase)
            {
                throw new ArgumentException("英语示例词必须是词库中存在的单词动词。", nameof(sourceSenseId));
            }
            var forms = new List<EnglishExampleFormDto>(enabledForms.Count);
            foreach (var formCode in enabledForms)
            {
                var form = member.Forms.Where(item => string.Equals(item.FormCode, formCode, StringComparison.Ordinal))
                    .OrderByDescending(item => item.IsPrimary).ThenByDescending(item => item.IsCommon).FirstOrDefault();
                if (form is null)
                {
                    throw new ArgumentException("英语示例词缺少已启用的词形。", nameof(sourceSenseId));
                }
                forms.Add(new EnglishExampleFormDto(formCode, form.Surface));
            }
            return new EnglishExampleParadigmDto(normalizedId, member.Headword, forms);
        }
    }
}
