using Dictation.Services.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.TtsHost
{
    internal static class Program
    {
        [STAThread]
        public static async Task<int> Main(string[] args)
        {
            if (!TtsHostOptions.TryParse(args, out var options) || options is null)
            {
                return 1;
            }

            try
            {
                using var services = new ServiceCollection()
                    .AddSingleton(options.Session)
                    .AddSingleton<ITtsSynthesisEngine, UnconfiguredTtsSynthesisEngine>()
                    .AddSingleton<TtsPipeServer>()
                    .BuildServiceProvider();
                var server = services.GetRequiredService<TtsPipeServer>();
                await server.RunAsync().ConfigureAwait(false);
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}
