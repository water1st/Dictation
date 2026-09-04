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
            : this(sourceKind, sourcePath, outputPath, null)
        {
        }

        private BuilderOptions(
            LexiconSourceKind sourceKind,
            string sourcePath,
            string outputPath,
            string? tomoshiDatabasePath)
        {
            if (!Enum.IsDefined(sourceKind))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
            }

            SourceKind = sourceKind;
            SourcePath = RequirePath(sourcePath, nameof(sourcePath));
            OutputPath = RequirePath(outputPath, nameof(outputPath));
            TomoshiDatabasePath = tomoshiDatabasePath is null
                ? null
                : RequirePath(tomoshiDatabasePath, nameof(tomoshiDatabasePath));
        }

        internal static BuilderOptions ForJapanese(
            string jmDictPath,
            string tomoshiDatabasePath,
            string outputPath)
        {
            return new BuilderOptions(
                LexiconSourceKind.JmDict,
                jmDictPath,
                outputPath,
                tomoshiDatabasePath);
        }

        internal LexiconSourceKind SourceKind { get; }

        internal string SourcePath { get; }

        internal string OutputPath { get; }

        internal string? TomoshiDatabasePath { get; }

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
