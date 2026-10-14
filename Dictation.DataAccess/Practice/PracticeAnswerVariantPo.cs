namespace Dictation.DataAccess.Practice
{
    /// <summary>
    /// 表示可接受答案词形的持久化字面与发音信息。
    /// </summary>
    public sealed record PracticeAnswerVariantPo(
        string Surface,
        string? Reading,
        string? ScriptCode,
        bool IsCommon,
        bool IsPrimary);
}
