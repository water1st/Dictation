namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 表示英语词库查询记录中的一个词形。
    /// </summary>
    public sealed class EnglishLexiconLookupFormPo
    {
        /// <summary>
        /// 创建同时保留词形类型与来源类型的英语词形。
        /// </summary>
        /// <param name="surface">词形文本。</param>
        /// <param name="formKind">词形类型代码。</param>
        /// <param name="formSourceKind">词形来源代码。</param>
        public EnglishLexiconLookupFormPo(
            string surface,
            string formKind,
            string formSourceKind)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            FormKind = string.IsNullOrWhiteSpace(formKind)
                ? throw new ArgumentException("词形类型不能为空。", nameof(formKind))
                : formKind;
            FormSourceKind = string.IsNullOrWhiteSpace(formSourceKind)
                ? throw new ArgumentException("词形来源类型不能为空。", nameof(formSourceKind))
                : formSourceKind;
        }

        public string Surface { get; }

        public string FormKind { get; }

        public string FormSourceKind { get; }
    }
}
