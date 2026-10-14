namespace Dictation.Core.Importing
{
    /// <summary>
    /// 指示导入批次是否仍包含待处理项目。
    /// </summary>
    public enum ImportBatchStatus
    {
        /// <summary>仍有项目等待确认。</summary>
        Pending,

        /// <summary>批次中的项目已全部处理。</summary>
        Completed
    }
}
