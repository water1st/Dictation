using System;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 记录英语词库构建数量、来源哈希与输出位置，供校验和发布追踪。
    /// </summary>
    internal sealed class EnglishLexiconBuildSummary
    {
        /// <summary>
        /// 记录英语词库构建数量、来源哈希与输出位置，供校验和发布追踪。
        /// </summary>
        /// <param name="sourceFileCount">参与构建的来源文件数量。</param>
        /// <param name="lexemeCount">词元数量。</param>
        /// <param name="senseCount">输出或来源模型的义项数量。</param>
        /// <param name="synsetCount">同义词集数量。</param>
        /// <param name="definitionCount">来源定义数量。</param>
        /// <param name="exampleCount">来源例句数量。</param>
        /// <param name="formCount">输出词形数量。</param>
        /// <param name="derivationCount">来源派生关联数量。</param>
        /// <param name="learningSenseCount">聚合后的学习义项数量。</param>
        /// <param name="exactLearningSenseCount">具有精确中文释义的学习义项数量。</param>
        /// <param name="broadLearningSenseCount">仅有宽泛中文释义的学习义项数量。</param>
        /// <param name="missingLearningSenseCount">缺少中文释义的学习义项数量。</param>
        /// <param name="unclassifiedSourceFormCount">尚未归类的来源词形数量。</param>
        /// <param name="sourceFingerprintSha256">WordNet 来源集合的稳定 SHA-256 指纹。</param>
        /// <param name="cowSha256">中文 WordNet 文件 SHA-256。</param>
        /// <param name="ecdictSha256">ECDICT 文件 SHA-256。</param>
        /// <param name="outputPath">生成数据库的目标路径。</param>
        internal EnglishLexiconBuildSummary(
            int sourceFileCount,
            int lexemeCount,
            int senseCount,
            int synsetCount,
            int definitionCount,
            int exampleCount,
            int formCount,
            int derivationCount,
            int learningSenseCount,
            int exactLearningSenseCount,
            int broadLearningSenseCount,
            int missingLearningSenseCount,
            int unclassifiedSourceFormCount,
            string sourceFingerprintSha256,
            string cowSha256,
            string ecdictSha256,
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
            LearningSenseCount = learningSenseCount;
            ExactLearningSenseCount = exactLearningSenseCount;
            BroadLearningSenseCount = broadLearningSenseCount;
            MissingLearningSenseCount = missingLearningSenseCount;
            UnclassifiedSourceFormCount = unclassifiedSourceFormCount;
            SourceFingerprintSha256 = string.IsNullOrWhiteSpace(sourceFingerprintSha256)
                ? throw new ArgumentException("来源 fingerprint 不能为空。", nameof(sourceFingerprintSha256))
                : sourceFingerprintSha256;
            CowSha256 = RequireText(cowSha256, nameof(cowSha256));
            EcdictSha256 = RequireText(ecdictSha256, nameof(ecdictSha256));
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

        internal int LearningSenseCount { get; }

        internal int ExactLearningSenseCount { get; }

        internal int BroadLearningSenseCount { get; }

        internal int MissingLearningSenseCount { get; }

        internal int UnclassifiedSourceFormCount { get; }

        internal string SourceFingerprintSha256 { get; }

        internal string CowSha256 { get; }

        internal string EcdictSha256 { get; }

        internal string OutputPath { get; }

        private static string RequireText(string value, string parameterName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("哈希不能为空。", parameterName)
                : value;
        }
    }
}
