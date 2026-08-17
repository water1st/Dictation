using System;

namespace Dictation.LexiconBuilder
{
    internal enum LexiconSourceKind
    {
        JmDict = 0,
        WordNet = 1
    }

    internal sealed class BuilderOptions
    {
        internal BuilderOptions(LexiconSourceKind sourceKind, string sourcePath, string outputPath)
        {
            if (!Enum.IsDefined(sourceKind))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
            }

            SourceKind = sourceKind;
            SourcePath = RequirePath(sourcePath, nameof(sourcePath));
            OutputPath = RequirePath(outputPath, nameof(outputPath));
        }

        internal LexiconSourceKind SourceKind { get; }

        internal string SourcePath { get; }

        internal string OutputPath { get; }

        private static string RequirePath(string path, string parameterName)
        {
            return string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentException("路径不能为空。", parameterName)
                : path;
        }
    }

    internal sealed class JapaneseLexiconBuildSummary
    {
        internal JapaneseLexiconBuildSummary(
            int sourceEntryCount,
            int entryCount,
            int senseCount,
            int formCount,
            int glossCount,
            string inputSha256,
            string createdDate,
            string outputPath)
        {
            SourceEntryCount = sourceEntryCount;
            EntryCount = entryCount;
            SenseCount = senseCount;
            FormCount = formCount;
            GlossCount = glossCount;
            InputSha256 = inputSha256;
            CreatedDate = createdDate;
            OutputPath = outputPath;
        }

        internal int SourceEntryCount { get; }

        internal int EntryCount { get; }

        internal int SenseCount { get; }

        internal int FormCount { get; }

        internal int GlossCount { get; }

        internal string InputSha256 { get; }

        internal string CreatedDate { get; }

        internal string OutputPath { get; }
    }
}
