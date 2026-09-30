namespace Dictation.Core.Review
{
    /// <summary>风险选择产生的业务结果：一个整组及其中本轮待测／到期成员形式。</summary>
    /// <param name="WordGroupId">本轮按整组计数的用户词条标识。</param>
    /// <param name="Forms">仅包含启用且有适用答案的待练形式。</param>
    internal sealed record ReviewSessionGroupSelection(string WordGroupId, IReadOnlyList<ReviewFormMemoryDto> Forms);
}
