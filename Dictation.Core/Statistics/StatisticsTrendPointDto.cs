namespace Dictation.Core.Statistics
{
    /// <summary>表示本地日期上的预测点，不携带图表样式。</summary>
    /// <param name="Date">预测所属本地日期。</param>
    /// <param name="Value">保持率为 0–1，到期组数为非负整数。</param>
    public sealed record StatisticsTrendPointDto(DateOnly Date, double Value);
}
