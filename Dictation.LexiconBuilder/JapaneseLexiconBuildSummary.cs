namespace Dictation.LexiconBuilder
{
    /// <summary>
    /// 汇总日语词库输入指纹、输出路径与各类实体统计。
    /// </summary>
    internal sealed class JapaneseLexiconBuildSummary
    {
        /// <summary>
        /// 汇总日语词库输入指纹、输出路径与各类实体统计。
        /// </summary>
        /// <param name="sourceEntryCount">来源中读取的原始条目数量。</param>
        /// <param name="entryCount">输出数据库的条目数量。</param>
        /// <param name="senseCount">输出或来源模型的义项数量。</param>
        /// <param name="formCount">输出词形数量。</param>
        /// <param name="glossCount">来源释义数量。</param>
        /// <param name="chineseMeaningCount">写入的中文释义数量。</param>
        /// <param name="missingChineseEntryCount">缺少中文释义的条目数量。</param>
        /// <param name="inputSha256">主来源文件的 SHA-256。</param>
        /// <param name="tomoshiSha256">Tomoshi 来源文件的 SHA-256。</param>
        /// <param name="createdDate">来源记录的创建日期。</param>
        /// <param name="outputPath">生成数据库的目标路径。</param>
        internal JapaneseLexiconBuildSummary(
            int sourceEntryCount,
            int entryCount,
            int senseCount,
            int formCount,
            int glossCount,
            int chineseMeaningCount,
            int missingChineseEntryCount,
            string inputSha256,
            string tomoshiSha256,
            string createdDate,
            string outputPath)
        {
            SourceEntryCount = sourceEntryCount;
            EntryCount = entryCount;
            SenseCount = senseCount;
            FormCount = formCount;
            GlossCount = glossCount;
            ChineseMeaningCount = chineseMeaningCount;
            MissingChineseEntryCount = missingChineseEntryCount;
            InputSha256 = inputSha256;
            TomoshiSha256 = tomoshiSha256;
            CreatedDate = createdDate;
            OutputPath = outputPath;
        }

        internal int SourceEntryCount { get; }

        internal int EntryCount { get; }

        internal int SenseCount { get; }

        internal int FormCount { get; }

        internal int GlossCount { get; }

        internal int ChineseMeaningCount { get; }

        internal int MissingChineseEntryCount { get; }

        internal string InputSha256 { get; }

        internal string TomoshiSha256 { get; }

        internal string CreatedDate { get; }

        internal string OutputPath { get; }
    }
}
