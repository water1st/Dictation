namespace Dictation.Core.Importing
{
    /// <summary>
    /// 指示单个导入项目的暂存或处理结果。
    /// </summary>
    public enum ImportItemDecision
    {
        /// <summary>已暂存并等待确认。</summary>
        Pending,

        /// <summary>与现有词组重复，未再次暂存词组。</summary>
        Duplicate,

        /// <summary>已移入学习库。</summary>
        Learning,

        /// <summary>已移入复习库。</summary>
        Review,

        /// <summary>已丢弃。</summary>
        Discard
    }
}
