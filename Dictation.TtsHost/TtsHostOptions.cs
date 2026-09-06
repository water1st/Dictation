using System.Globalization;
using Dictation.Core.Tts;

namespace Dictation.TtsHost
{
    public sealed record TtsHostOptions(TtsHostSession Session, int ParentProcessId)
    {
        public static bool TryParse(string[]? args, out TtsHostOptions? options)
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
