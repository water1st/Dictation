using System;

namespace Dictation.LexiconBuilder.WordNet
{
    internal sealed class EnglishLexiconBuildSummary
    {
        internal EnglishLexiconBuildSummary(
            int sourceFileCount,
            int lexemeCount,
            int senseCount,
            int synsetCount,
            int definitionCount,
            int exampleCount,
            int formCount,
            int derivationCount,
            int unclassifiedSourceFormCount,
            string sourceFingerprintSha256,
            string outputPath)
        {
            SourceFileCount = sourceFileCount;
            LexemeCount = lexemeCount;
            SenseCount = senseCount;
            SynsetCount = synsetCount;
            DefinitionCount = definitionCount;
            ExampleCount = exampleCount;
            FormCount = formCount;
            DerivationCount = derivationCount;
            UnclassifiedSourceFormCount = unclassifiedSourceFormCount;
            SourceFingerprintSha256 = string.IsNullOrWhiteSpace(sourceFingerprintSha256)
                ? throw new ArgumentException("来源 fingerprint 不能为空。", nameof(sourceFingerprintSha256))
                : sourceFingerprintSha256;
            OutputPath = string.IsNullOrWhiteSpace(outputPath)
                ? throw new ArgumentException("输出路径不能为空。", nameof(outputPath))
                : outputPath;
        }

        internal int SourceFileCount { get; }

        internal int LexemeCount { get; }

        internal int SenseCount { get; }

        internal int SynsetCount { get; }

        internal int DefinitionCount { get; }

        internal int ExampleCount { get; }

        internal int FormCount { get; }

        internal int DerivationCount { get; }

        internal int UnclassifiedSourceFormCount { get; }

        internal string SourceFingerprintSha256 { get; }

        internal string OutputPath { get; }
    }
}
