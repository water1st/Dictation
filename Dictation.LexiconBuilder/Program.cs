using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Dictation.LexiconBuilder.JmDict;

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
                var summary = new JapaneseLexiconDatabaseBuilder().Build(options);
                stopwatch.Stop();
                output.WriteLine(
                    $"词库构建完成：source_entries={summary.SourceEntryCount} entries={summary.EntryCount} senses={summary.SenseCount} forms={summary.FormCount} glosses={summary.GlossCount} output={summary.OutputPath} elapsed_ms={stopwatch.ElapsedMilliseconds}");
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
                throw new ArgumentException("必须同时提供 --jmdict 和 --output。", nameof(args));
            }

            string? jmdictPath = null;
            string? outputPath = null;
            for (var index = 0; index < args.Count; index++)
            {
                var option = args[index];
                if (option is not "--jmdict" and not "--output")
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
                if (option == "--jmdict")
                {
                    if (jmdictPath is not null)
                    {
                        throw new ArgumentException("--jmdict 不能重复。", nameof(args));
                    }

                    jmdictPath = value;
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

            if (jmdictPath is null || outputPath is null)
            {
                throw new ArgumentException("必须同时提供 --jmdict 和 --output。", nameof(args));
            }

            return new BuilderOptions(jmdictPath, outputPath);
        }
    }
}
