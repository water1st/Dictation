namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示一个语言在指定本地日期首次开始及完成的不同学习词组数量。
    /// </summary>
    public sealed record LearningDailyProgressCountsPo(long StartedGroupCount, long CompletedGroupCount);
}
