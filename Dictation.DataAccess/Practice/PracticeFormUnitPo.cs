namespace Dictation.DataAccess.Practice
{
    /// <summary>
    /// 表示出题快照中的持久化词形成绩与可用答案。
    /// </summary>
    public sealed record PracticeFormUnitPo(
        string WordGroupId,
        long MemberId,
        string FormCode,
        string FormLabel,
        bool IsPhrase,
        string? KanjiUsageCode,
        IReadOnlyList<string> Meanings,
        IReadOnlyList<PracticeAnswerVariantPo> Variants,
        int CorrectStreak,
        bool IsMastered,
        int NextEligibleOrdinal,
        int MeaningWritingCount,
        int DictationCount,
        int LatinCount,
        int KanaCount,
        int KanjiCount,
        int CorrectCount,
        int ErrorCount);
}
