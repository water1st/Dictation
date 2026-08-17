using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Dictation.LexiconBuilder.JmDict;
using Dictation.LexiconBuilder.WordNet;

namespace Dictation.LexiconBuilder
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return LexiconBuilderCli.Run(args, Console.Out, Console.Error);
        }
    }

    internal static class LexiconBuilderCli
    {
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
                        $"词库构建完成：source_entries={summary.SourceEntryCount} entries={summary.EntryCount} senses={summary.SenseCount} forms={summary.FormCount} glosses={summary.GlossCount} output={summary.OutputPath} elapsed_ms={stopwatch.ElapsedMilliseconds}");
                }
                else
                {
                    var summary = new EnglishLexiconDatabaseBuilder().Build(options);
                    stopwatch.Stop();
                    output.WriteLine(
                        $"词库构建完成：source=wordnet source_files={summary.SourceFileCount} lexemes={summary.LexemeCount} senses={summary.SenseCount} synsets={summary.SynsetCount} definitions={summary.DefinitionCount} examples={summary.ExampleCount} forms={summary.FormCount} derivations={summary.DerivationCount} unclassified_source_forms={summary.UnclassifiedSourceFormCount} source_fingerprint_sha256={summary.SourceFingerprintSha256} output={summary.OutputPath} elapsed_ms={stopwatch.ElapsedMilliseconds}");
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
            string? outputPath = null;
            for (var index = 0; index < args.Count; index++)
            {
                var option = args[index];
                if (option is not "--jmdict" and not "--wordnet" and not "--output")
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

            return new BuilderOptions(sourceKind.Value, sourcePath, outputPath);
        }
    }
}
