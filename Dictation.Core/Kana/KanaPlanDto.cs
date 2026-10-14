namespace Dictation.Core.Kana
{
    /// <summary>描述独立五十音计划，两组选项各至少保留一项。</summary>
    /// <param name="Enabled">是否启用每日五十音目标。</param>
    /// <param name="Hiragana">是否练习平假名。</param>
    /// <param name="Katakana">是否练习片假名。</param>
    /// <param name="Clear">是否练习清音。</param>
    /// <param name="Voiced">是否练习浊音与半浊音。</param>
    /// <param name="Contracted">是否练习拗音。</param>
    /// <param name="ReviewAll">是否每日全量覆盖启用项目。</param>
    /// <param name="DailyTarget">非全量模式每日最大数量，范围1–214。</param>
    public sealed record KanaPlanDto(bool Enabled, bool Hiragana, bool Katakana, bool Clear, bool Voiced,
        bool Contracted, bool ReviewAll, int DailyTarget);
}
