namespace Dictation.Core.Review
{
    /// <summary>定位错误所属整组、独立词元及词形，避免同组派生词彼此混淆。</summary>
    public sealed record ReviewErrorSummaryDto(string WordGroupId, long MemberId, string FormCode, string ExpectedAnswer);
}
