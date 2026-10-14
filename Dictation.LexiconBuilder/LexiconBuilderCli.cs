using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Dictation.LexiconBuilder.JmDict;
using Dictation.LexiconBuilder.WordNet;

namespace Dictation.LexiconBuilder
{
    /// <summary>
    /// 解析词库构建命令行并输出稳定的构建统计或错误信息。
    /// </summary>
    internal static class LexiconBuilderCli
    {
        /// <summary>
        /// 执行一次词库构建，并返回适合命令行调用方判断的退出码。
        /// </summary>
        /// <param name="args">词库来源、辅助来源与输出路径参数。</param>
        /// <param name="output">成功统计输出。</param>
        /// <param name="error">参数、输入或构建错误输出。</param>
        /// <returns>成功为 0，构建失败为 1，参数或输入错误为 2。</returns>
        internal static int Run(string[] args, TextWriter output, TextWriter error)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            try
            {
                var options = ParseArguments(args);
                var stopwatch = Stopwatch.StartNew();
                if (options.SourceKind == LexiconSourceKind.JmDict)
                {
                    var summary = new JapaneseLexiconDatabaseBuilder().Build(options);
                    stopwatch.Stop();
                    output.WriteLine(
                        $"词库构建完成：source_entries={summary.SourceEntryCount} entries={summary.EntryCount} senses={summary.SenseCount} forms={summary.FormCount} source_glosses={summary.GlossCount} chinese_meanings={summary.ChineseMeaningCount} missing_chinese_entries={summary.MissingChineseEntryCount} output={summary.OutputPath} elapsed_ms={stopwatch.ElapsedMilliseconds}");
                }
                else
                {
                    var summary = new EnglishLexiconDatabaseBuilder().Build(options);
                    stopwatch.Stop();
                    output.WriteLine(
                        $"词库构建完成：source=wordnet source_files={summary.SourceFileCount} lexemes={summary.LexemeCount} senses={summary.SenseCount} synsets={summary.SynsetCount} definitions={summary.DefinitionCount} examples={summary.ExampleCount} forms={summary.FormCount} derivations={summary.DerivationCount} learning_senses={summary.LearningSenseCount} exact={summary.ExactLearningSenseCount} broad={summary.BroadLearningSenseCount} missing={summary.MissingLearningSenseCount} unclassified_source_forms={summary.UnclassifiedSourceFormCount} source_fingerprint_sha256={summary.SourceFingerprintSha256} output={summary.OutputPath} elapsed_ms={stopwatch.ElapsedMilliseconds}");
                }

                return 0;
            }
            catch (ArgumentException exception)
            {
                error.WriteLine($"参数错误：{exception.Message}");
                return 2;
            }
            catch (FileNotFoundException exception)
            {
                error.WriteLine($"输入错误：{exception.Message}");
                return 2;
            }
            catch (DirectoryNotFoundException exception)
            {
                error.WriteLine($"输入错误：{exception.Message}");
                return 2;
            }
            catch (InvalidDataException exception)
            {
                error.WriteLine($"输入错误：{exception.Message}");
                return 2;
            }
            catch (LexiconBuildException exception)
            {
                error.WriteLine($"构建失败：{exception.Message}");
                return 1;
            }
            catch (Exception exception)
            {
                error.WriteLine($"构建失败：{exception.Message}");
                return 1;
            }
        }

        private static BuilderOptions ParseArguments(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                throw new ArgumentException("必须提供 --jmdict 或 --wordnet 之一以及 --output。", nameof(args));
            }

            LexiconSourceKind? sourceKind = null;
            string? sourcePath = null;
            string? tomoshiDatabasePath = null;
            string? chineseWordNetXmlPath = null;
            string? ecdictDatabasePath = null;
            string? outputPath = null;
            for (var index = 0; index < args.Count; index++)
            {
                var option = args[index];
                if (option is not "--jmdict" and not "--wordnet" and not "--tomoshi" and not "--cow" and not "--ecdict" and not "--output")
                {
                    throw new ArgumentException($"不支持的参数：{option}。", nameof(args));
                }

                if (index + 1 >= args.Count
                    || string.IsNullOrWhiteSpace(args[index + 1])
                    || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"参数 {option} 缺少值。", nameof(args));
                }

                var value = args[++index];
                if (option is "--jmdict" or "--wordnet")
                {
                    if (sourceKind is not null)
                    {
                        throw new ArgumentException("--jmdict 和 --wordnet 必须且只能指定一个，来源参数不能重复。", nameof(args));
                    }

                    sourceKind = option == "--jmdict"
                        ? LexiconSourceKind.JmDict
                        : LexiconSourceKind.WordNet;
                    sourcePath = value;
                }
                else if (option == "--tomoshi")
                {
                    if (tomoshiDatabasePath is not null)
                    {
                        throw new ArgumentException("--tomoshi 不能重复。", nameof(args));
                    }

                    tomoshiDatabasePath = value;
                }
                else if (option == "--cow")
                {
                    if (chineseWordNetXmlPath is not null)
                    {
                        throw new ArgumentException("--cow 不能重复。", nameof(args));
                    }

                    chineseWordNetXmlPath = value;
                }
                else if (option == "--ecdict")
                {
                    if (ecdictDatabasePath is not null)
                    {
                        throw new ArgumentException("--ecdict 不能重复。", nameof(args));
                    }

                    ecdictDatabasePath = value;
                }
                else
                {
                    if (outputPath is not null)
                    {
                        throw new ArgumentException("--output 不能重复。", nameof(args));
                    }

                    outputPath = value;
                }
            }

            if (sourceKind is null || sourcePath is null || outputPath is null)
            {
                throw new ArgumentException("必须提供 --jmdict 或 --wordnet 之一以及 --output。", nameof(args));
            }

            if (sourceKind == LexiconSourceKind.JmDict)
            {
                if (tomoshiDatabasePath is null)
                {
                    throw new ArgumentException("--jmdict 必须同时提供且只提供一个 --tomoshi。", nameof(args));
                }

                if (chineseWordNetXmlPath is not null || ecdictDatabasePath is not null)
                {
                    throw new ArgumentException("--cow 和 --ecdict 只能与 --wordnet 一起使用。", nameof(args));
                }

                return BuilderOptions.ForJapanese(sourcePath, tomoshiDatabasePath, outputPath);
            }

            if (tomoshiDatabasePath is not null)
            {
                throw new ArgumentException("--tomoshi 只能与 --jmdict 一起使用。", nameof(args));
            }

            if (chineseWordNetXmlPath is null || ecdictDatabasePath is null)
            {
                throw new ArgumentException("--wordnet 必须同时提供且只提供一个 --cow 和 --ecdict。", nameof(args));
            }

            return BuilderOptions.ForEnglish(sourcePath, chineseWordNetXmlPath, ecdictDatabasePath, outputPath);
        }
    }
}
