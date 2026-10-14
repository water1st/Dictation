namespace Dictation.Core.Kana
{
    /// <summary>描述可供首页和日语打卡读取的独立每日进度。</summary>
    /// <param name="Enabled">是否启用该计划。</param>
    /// <param name="CompletedToday">本地今天完成的独立启用项目数。</param>
    /// <param name="Target">实际可练习目标，不为填数抽取未到期项目。</param>
    /// <param name="Available">当前还未完成的适用项目数。</param>
    /// <param name="HasActiveSession">是否有需要优先恢复的轮次。</param>
    public sealed record KanaProgressDto(bool Enabled, int CompletedToday, int Target, int Available, bool HasActiveSession);
}
