namespace Dictation.DataAccess.Importing
{
    /// <summary>
    /// 表示词组释义查询返回的持久化行。
    /// </summary>
    internal sealed class WordGroupMeaningRow
    {
        public long MemberId { get; set; }

        public int MeaningIndex { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
