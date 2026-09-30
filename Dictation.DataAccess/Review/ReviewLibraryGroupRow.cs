namespace Dictation.DataAccess.Review
{
    /// <summary>承接只读复习词组查询的数据库列，仅供该数据访问组件映射使用。</summary>
    internal sealed class ReviewLibraryGroupRow
    {
        public string WordGroupId { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string AnchorSourceSenseId { get; set; } = string.Empty;
        public string Placement { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
    }
}
