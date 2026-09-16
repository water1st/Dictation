using System;

namespace Dictation.LexiconBuilder
{
    /// <summary>
    /// 表示一次日语或英语词库构建所需的输入与隔离输出路径。
    /// </summary>
    internal sealed class BuilderOptions
    {
        private BuilderOptions(
            LexiconSourceKind sourceKind,
            string sourcePath,
            string outputPath,
            string? tomoshiDatabasePath,
            string? chineseWordNetXmlPath,
            string? ecdictDatabasePath)
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
            ChineseWordNetXmlPath = chineseWordNetXmlPath is null
                ? null
                : RequirePath(chineseWordNetXmlPath, nameof(chineseWordNetXmlPath));
            EcdictDatabasePath = ecdictDatabasePath is null
                ? null
                : RequirePath(ecdictDatabasePath, nameof(ecdictDatabasePath));
        }

        /// <summary>
        /// 创建 JMdict 与 Tomoshi 中文释义构建参数。
        /// </summary>
        internal static BuilderOptions ForJapanese(
            string jmDictPath,
            string tomoshiDatabasePath,
            string outputPath)
        {
            return new BuilderOptions(
                LexiconSourceKind.JmDict,
                jmDictPath,
                outputPath,
                tomoshiDatabasePath,
                null,
                null);
        }

        /// <summary>
        /// 创建 WordNet、Chinese Open WordNet 与 ECDICT 构建参数。
        /// </summary>
        internal static BuilderOptions ForEnglish(
            string wordNetDirectory,
            string chineseWordNetXmlPath,
            string ecdictDatabasePath,
            string outputPath)
        {
            return new BuilderOptions(
                LexiconSourceKind.WordNet,
                wordNetDirectory,
                outputPath,
                null,
                chineseWordNetXmlPath,
                ecdictDatabasePath);
        }

        internal LexiconSourceKind SourceKind { get; }

        internal string SourcePath { get; }

        internal string OutputPath { get; }

        internal string? TomoshiDatabasePath { get; }

        internal string? ChineseWordNetXmlPath { get; }

        internal string? EcdictDatabasePath { get; }

        private static string RequirePath(string path, string parameterName)
        {
            return string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentException("路径不能为空。", parameterName)
                : path;
        }
    }
}
