using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation
{
    public static class DictationPresentationServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationPresentation(this IServiceCollection services)
        {
            services.AddTransient<MainWindow>();
            services.AddTransient<DictationWindow>();
            services.AddTransient<ResultWindow>();
            services.AddTransient<ReviewWindow>();

            services.AddTransient<WindowFactory>();

            return services;
        }
    }
}
