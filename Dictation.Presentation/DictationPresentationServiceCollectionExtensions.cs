using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation
{
    public static class DictationPresentationServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationPresentation(this IServiceCollection services)
        {
            services.AddSingleton<MainWindow>();
            services.AddSingleton<DictationWindow>();
            services.AddSingleton<ResultWindow>();
            services.AddSingleton<ReviewWindow>();

            services.AddTransient<WindowFactory>();

            return services;
        }
    }
}
