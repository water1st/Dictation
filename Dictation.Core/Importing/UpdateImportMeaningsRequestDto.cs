namespace Dictation.Core.Importing
{
    /// <summary>
    /// 描述待处理词组中一个成员的中文释义更新请求。
    /// </summary>
    /// <param name="WordGroupId">待修改词组的稳定标识。</param>
    /// <param name="SourceSenseId">待修改成员的来源义项标识。</param>
    /// <param name="Meanings">替换后的中文释义集合。</param>
    public sealed record UpdateImportMeaningsRequestDto(
        string WordGroupId,
        string SourceSenseId,
        IReadOnlyList<string> Meanings);
}
