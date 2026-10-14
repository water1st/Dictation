namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示词组成员查询返回的持久化行。
    /// </summary>
    internal sealed class WordGroupMemberRow
    {
        public long MemberId { get; set; }

        public string WordGroupId { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public string SourceSenseId { get; set; } = string.Empty;

        public string SourceLexemeId { get; set; } = string.Empty;

        public string Headword { get; set; } = string.Empty;

        public string? Reading { get; set; }

        public string PartOfSpeechLabel { get; set; } = string.Empty;

        public string AttributeLabel { get; set; } = string.Empty;

        public int IsPhrase { get; set; }

        public int SortOrder { get; set; }

        public string MeaningLanguage { get; set; } = string.Empty;

        public string MeaningSource { get; set; } = string.Empty;

        public string MeaningPrecision { get; set; } = string.Empty;

        public string? KanjiUsageCode { get; set; }
    }
}
