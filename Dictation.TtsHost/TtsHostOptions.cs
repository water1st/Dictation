using System.Globalization;
using Dictation.Core.Tts;

namespace Dictation.TtsHost
{
    /// <summary>
    /// 表示 TTS 宿主启动所需的管道会话与父进程标识。
    /// </summary>
    internal sealed record TtsHostOptions(TtsHostSession Session, int ParentProcessId)
    {
        /// <summary>
        /// 严格解析宿主命令行；仅接受既定的四段参数格式。
        /// </summary>
        internal static bool TryParse(string[]? args, out TtsHostOptions? options)
        {
            options = null;
            if (args is null
                || args.Length != 4
                || !string.Equals(args[0], "--pipe", StringComparison.Ordinal)
                || !string.Equals(args[2], "--parent-process-id", StringComparison.Ordinal)
                || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parentProcessId)
                || parentProcessId <= 0)
            {
                return false;
            }

            try
            {
                options = new TtsHostOptions(new TtsHostSession(args[1]), parentProcessId);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
