namespace Dictation.LexiconBuilder
{
    /// <summary>
    /// 汇总日语词库输入指纹、输出路径与各类实体统计。
    /// </summary>
    internal sealed class JapaneseLexiconBuildSummary
    {
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
