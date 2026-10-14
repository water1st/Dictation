namespace Dictation.Core.Importing
{
    /// <summary>
    /// 描述一次导入选择及锚定义项可选的中文释义覆盖。
    /// </summary>
    /// <param name="SourceText">触发选择的原始词形。</param>
    /// <param name="SourceSenseId">所选锚定义项的稳定标识。</param>
    /// <param name="ChineseMeaningOverride">用户为锚定义项提供的中文释义；为 <see langword="null"/> 时保留词库释义。</param>
    public sealed record ImportSelectionDto(
        string SourceText,
        string SourceSenseId,
        IReadOnlyList<string>? ChineseMeaningOverride = null);
}
