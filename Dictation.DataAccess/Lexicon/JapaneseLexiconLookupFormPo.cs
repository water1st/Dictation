namespace Dictation.DataAccess.Lexicon
{
    /// <summary>
    /// 表示日语词库查询记录中的一个词形。
    /// </summary>
    public sealed class JapaneseLexiconLookupFormPo
    {
        /// <summary>
        /// 创建包含读音、文字类型和词汇角色的日语词形。
        /// </summary>
        /// <param name="surface">词形文本。</param>
        /// <param name="reading">词形读音。</param>
        /// <param name="formKind">词形类型代码。</param>
        /// <param name="scriptKind">文字类型代码。</param>
        /// <param name="lexicalRole">词汇角色代码。</param>
        /// <param name="isCommon">是否常用。</param>
        /// <param name="isPrimary">是否为主词形。</param>
        public JapaneseLexiconLookupFormPo(
            string surface,
            string reading,
            string formKind,
            string scriptKind,
            string lexicalRole,
            bool isCommon,
            bool isPrimary)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            Reading = string.IsNullOrWhiteSpace(reading)
                ? throw new ArgumentException("读音不能为空。", nameof(reading))
                : reading;
            FormKind = string.IsNullOrWhiteSpace(formKind)
                ? throw new ArgumentException("词形类型不能为空。", nameof(formKind))
                : formKind;
            ScriptKind = string.IsNullOrWhiteSpace(scriptKind)
                ? throw new ArgumentException("文字类型不能为空。", nameof(scriptKind))
                : scriptKind;
            LexicalRole = string.IsNullOrWhiteSpace(lexicalRole)
                ? throw new ArgumentException("词汇角色不能为空。", nameof(lexicalRole))
                : lexicalRole;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string Reading { get; }

        public string FormKind { get; }

        public string ScriptKind { get; }

        public string LexicalRole { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }
}
