namespace Dictation.DataAccess.Review
{
    /// <summary>仅携带本轮词形成绩写入列，不把题面元数据作为数据库写入输入。</summary>
    public sealed record ReviewFormOutcomePo(string WordGroupId, long MemberId, string FormCode,
        int CorrectStreak, bool IsMastered, int NextEligibleOrdinal, int MeaningWritingCount, int DictationCount,
        int LatinCount, int KanaCount, int KanjiCount, int CorrectCount, int ErrorCount);
}
