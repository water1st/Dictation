namespace Dictation.DataAccess.Statistics
{
    /// <summary>保存某语言首次练习时的当天目标，不复制用户完成事实。</summary>
    /// <param name="Language">Japanese 或 English。</param>
    /// <param name="LocalDate">yyyy-MM-dd 本地日期。</param>
    /// <param name="LearningTarget">当天有效学习组数。</param>
    /// <param name="ReviewTarget">当天有效复习组数。</param>
    /// <param name="KanaTarget">日语五十音有效项目数。</param>
    public sealed record DailyPlanSnapshotPo(string Language, string LocalDate, long LearningTarget, long ReviewTarget, long KanaTarget);
}
