namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示词组词形查询返回的持久化行。
    /// </summary>
    internal sealed class WordGroupFormRow
    {
        public long MemberId { get; set; }

        public int FormIndex { get; set; }

        public string Surface { get; set; } = string.Empty;

        public string? Reading { get; set; }

        public string FormCode { get; set; } = string.Empty;

        public string? ScriptCode { get; set; }

        public string? RoleCode { get; set; }

        public string FormLabel { get; set; } = string.Empty;

        public string DetailLabel { get; set; } = string.Empty;

        public int IsCommon { get; set; }

        public int IsPrimary { get; set; }
    }
}
