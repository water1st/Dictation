namespace Dictation.Core.Lexicon
{
    /// <summary>
    /// 表示词库结果中的一个可学习词形。
    /// </summary>
    public sealed class LexiconLookupFormDto
    {
        /// <summary>
        /// 创建同时保留稳定代码与展示标签的词形。
        /// </summary>
        /// <param name="surface">词形文本。</param>
        /// <param name="reading">词形读音。</param>
        /// <param name="formCode">词形稳定代码。</param>
        /// <param name="scriptCode">文字类型稳定代码。</param>
        /// <param name="roleCode">词汇角色稳定代码。</param>
        /// <param name="formLabel">词形展示标签。</param>
        /// <param name="detailLabel">词形详情展示标签。</param>
        /// <param name="isCommon">是否常用。</param>
        /// <param name="isPrimary">是否为主词形。</param>
        public LexiconLookupFormDto(
            string surface,
            string? reading,
            string formCode,
            string? scriptCode,
            string? roleCode,
            string formLabel,
            string detailLabel,
            bool isCommon,
            bool isPrimary)
        {
            Surface = string.IsNullOrWhiteSpace(surface)
                ? throw new ArgumentException("词形不能为空。", nameof(surface))
                : surface;
            Reading = reading;
            FormCode = string.IsNullOrWhiteSpace(formCode)
                ? throw new ArgumentException("词形稳定标识不能为空。", nameof(formCode))
                : formCode;
            ScriptCode = scriptCode;
            RoleCode = roleCode;
            FormLabel = string.IsNullOrWhiteSpace(formLabel)
                ? throw new ArgumentException("词形标签不能为空。", nameof(formLabel))
                : formLabel;
            DetailLabel = string.IsNullOrWhiteSpace(detailLabel)
                ? throw new ArgumentException("词形详情标签不能为空。", nameof(detailLabel))
                : detailLabel;
            IsCommon = isCommon;
            IsPrimary = isPrimary;
        }

        public string Surface { get; }

        public string? Reading { get; }

        public string FormCode { get; }

        public string? ScriptCode { get; }

        public string? RoleCode { get; }

        public string FormLabel { get; }

        public string DetailLabel { get; }

        public bool IsCommon { get; }

        public bool IsPrimary { get; }
    }
}
