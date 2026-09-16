using Dictation.Core.Tts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dictation.Core
{
    /// <summary>
    /// 注册本地 TTS 宿主进程及其共享会话、Provider 与生命周期。
    /// </summary>
    public static class TtsHostRegistrationExtensions
    {
        /// <summary>
        /// 将本地 TTS 能力注册到调用方现有的唯一 DI 容器。
        /// </summary>
        /// <param name="services">应用的服务集合。</param>
        /// <param name="executablePath">TTS 宿主可执行文件路径。</param>
        /// <returns>同一个服务集合，便于继续链式注册。</returns>
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
