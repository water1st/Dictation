namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示创建会话时要持久化的一个词组及启用词形。
    /// </summary>
    public sealed record LearningSessionGroupSeedPo(
        string WordGroupId,
        IReadOnlyList<LearningFormIdentityPo> Forms);
}
