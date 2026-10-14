using System;

namespace Dictation.LexiconBuilder
{
    /// <summary>
    /// 提供词库构建工具的进程入口。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return LexiconBuilderCli.Run(args, Console.Out, Console.Error);
        }
    }
}
