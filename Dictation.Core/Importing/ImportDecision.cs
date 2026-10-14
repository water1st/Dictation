namespace Dictation.Core.Importing
{
    /// <summary>
    /// 指示待处理导入项目的目标去向。
    /// </summary>
    public enum ImportDecision
    {
        /// <summary>移入学习库。</summary>
        Learning,

        /// <summary>移入复习库。</summary>
        Review,

        /// <summary>丢弃导入项目及其待处理词组。</summary>
        Discard
    }
}
