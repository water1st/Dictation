namespace Dictation.Core.Importing
{
    /// <summary>
    /// 表示词组成员的一种可学习词形及其展示元数据。
    /// </summary>
    public sealed class ImportWordGroupFormDto
    {
        /// <summary>
        /// 初始化成员词形业务快照。
        /// </summary>
        /// <param name="surface">词形文本。</param>
        /// <param name="reading">可选读音。</param>
        /// <param name="formCode">词形稳定标识。</param>
        /// <param name="scriptCode">可选书写系统标识。</param>
        /// <param name="roleCode">可选词法角色标识。</param>
        /// <param name="formLabel">面向用户的词形标签。</param>
        /// <param name="detailLabel">面向用户的补充说明。</param>
        /// <param name="isCommon">是否为常见词形。</param>
        /// <param name="isPrimary">是否为主要词形。</param>
        public ImportWordGroupFormDto(
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
            Surface = surface;
            Reading = reading;
            FormCode = string.IsNullOrWhiteSpace(formCode)
                ? throw new ArgumentException("词形稳定标识不能为空。", nameof(formCode))
                : formCode;
            ScriptCode = scriptCode;
            RoleCode = roleCode;
            FormLabel = formLabel;
            DetailLabel = detailLabel;
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
