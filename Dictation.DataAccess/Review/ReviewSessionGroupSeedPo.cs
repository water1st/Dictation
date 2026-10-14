namespace Dictation.DataAccess.Review
{
    /// <summary>表示会话内一个整组及本轮要复习的成员形式。</summary>
    /// <param name="WordGroupId">用户整组标识。</param>
    /// <param name="Forms">待测或到期形式的稳定来源定位。</param>
    public sealed record ReviewSessionGroupSeedPo(string WordGroupId, IReadOnlyList<ReviewSessionFormSeedPo> Forms);
}
