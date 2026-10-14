namespace Dictation.DataAccess.Mistakes
{
    /// <summary>保存错误历史联表查询的一条独立成员观察及当前中文快照，不包含记忆算法。</summary>
    public sealed class WrongAnswerObservationPo
    {
        public string WordGroupId { get; init; } = string.Empty;
        public string SourceSenseId { get; init; } = string.Empty;
        public string FormCode { get; init; } = string.Empty;
        public string Headword { get; init; } = string.Empty;
        public string FormLabel { get; init; } = string.Empty;
        public string Meanings { get; init; } = string.Empty;
        public string Placement { get; init; } = string.Empty;
        public string? DueAtUtc { get; init; }
        public string Mode { get; init; } = string.Empty;
        public string Channel { get; init; } = string.Empty;
        public string OutputMode { get; init; } = string.Empty;
        public string SubmittedText { get; init; } = string.Empty;
        public string AnsweredAtUtc { get; init; } = string.Empty;
        public string ExpectedAnswer { get; init; } = string.Empty;
        public string ExpectedSpokenText { get; init; } = string.Empty;
    }
}
