using Dictation.Core.Tts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dictation.Core
{
    public static class TtsHostRegistrationExtensions
    {
        public static IServiceCollection AddLocalTtsHost(
            this IServiceCollection services,
            string executablePath)
        {
            ArgumentNullException.ThrowIfNull(services);
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("TTS Host 可执行文件路径不能为空。", nameof(executablePath));
            }

            services.AddSingleton(new TtsHostSession($"Dictation.Tts.{Guid.NewGuid():N}"));
            services.AddSingleton<NamedPipeTtsProvider>();
            services.AddSingleton<ITtsProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<NamedPipeTtsProvider>());
            services.AddSingleton<TtsHostLifecycle>(serviceProvider =>
                new TtsHostLifecycle(
                    executablePath,
                    serviceProvider.GetRequiredService<TtsHostSession>()));
            services.AddSingleton<IHostedService>(serviceProvider =>
                serviceProvider.GetRequiredService<TtsHostLifecycle>());
            return services;
        }
    }
}
