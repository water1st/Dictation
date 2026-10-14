using System;

namespace Dictation.LexiconBuilder
{
    /// <summary>
    /// 表示一次日语或英语词库构建所需的输入与隔离输出路径。
    /// </summary>
    internal sealed class BuilderOptions
    {
        /// <summary>
        /// 表示一次日语或英语词库构建所需的输入与隔离输出路径。
        /// </summary>
        /// <param name="sourceKind">选择日语 JMdict 或英语 WordNet 来源。</param>
        /// <param name="sourcePath">主词库来源文件或目录路径。</param>
        /// <param name="outputPath">生成数据库的目标路径。</param>
        /// <param name="tomoshiDatabasePath">可选 Tomoshi 中文来源数据库路径。</param>
        /// <param name="chineseWordNetXmlPath">可选中文 WordNet XML 来源路径。</param>
        /// <param name="ecdictDatabasePath">可选 ECDICT 来源数据库路径。</param>
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
        /// <param name="jmDictPath">JMdict 压缩输入文件路径。</param>
        /// <param name="tomoshiDatabasePath">Tomoshi 中文释义数据库路径。</param>
        /// <param name="outputPath">不得覆盖输入资源的词库输出路径。</param>
        /// <returns>来源类型固定为 JMdict 的构建参数。</returns>
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
        /// <param name="wordNetDirectory">Open English WordNet YAML 根目录。</param>
        /// <param name="chineseWordNetXmlPath">Chinese Open WordNet XML 路径。</param>
        /// <param name="ecdictDatabasePath">ECDICT SQLite 数据库路径。</param>
        /// <param name="outputPath">不得覆盖输入资源的词库输出路径。</param>
        /// <returns>来源类型固定为 WordNet 的构建参数。</returns>
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
