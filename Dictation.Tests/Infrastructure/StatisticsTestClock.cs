namespace Dictation.Tests.Infrastructure
{
    /// <summary>固定统计用本地日期，并允许测试跨午夜及重启后的日期推进。</summary>
    internal sealed class StatisticsTestClock : TimeProvider
    {
        internal DateTimeOffset Value { get; set; } = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Value;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("统计测试UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
    }
}
