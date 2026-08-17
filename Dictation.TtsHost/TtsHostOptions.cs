using Dictation.Services.Tts;

namespace Dictation.TtsHost
{
    public sealed record TtsHostOptions(TtsHostSession Session)
    {
        public static bool TryParse(string[]? args, out TtsHostOptions? options)
        {
            options = null;
            if (args is null || args.Length != 2 || !string.Equals(args[0], "--pipe", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                options = new TtsHostOptions(new TtsHostSession(args[1]));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
