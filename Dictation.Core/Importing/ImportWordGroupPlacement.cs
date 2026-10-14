namespace Dictation.Core.Importing
{
    /// <summary>
    /// 指示词组当前所属的业务区域。
    /// </summary>
    public enum ImportWordGroupPlacement
    {
        /// <summary>位于导入收件箱。</summary>
        Inbox,

        /// <summary>位于学习库。</summary>
        Learning,

        /// <summary>位于复习库。</summary>
        Review
    }
}
