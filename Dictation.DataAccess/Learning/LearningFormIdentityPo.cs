namespace Dictation.DataAccess.Learning
{
    /// <summary>
    /// 表示可加入学习会话的持久化词形标识及字形范围。
    /// </summary>
    public sealed record LearningFormIdentityPo(
        long MemberId,
        string FormCode,
        string? ScriptCode = null,
        string? KanjiUsageCode = null)
    {
        /// <summary>
        /// 使用成员和词形标识创建不限定字形的词形标识。
        /// </summary>
        public LearningFormIdentityPo(long memberId, string formCode)
            : this(memberId, formCode, null, null)
        {
        }
    }
}
