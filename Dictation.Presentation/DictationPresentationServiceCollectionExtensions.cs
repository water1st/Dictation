using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation
{
    public static class DictationPresentationServiceCollectionExtensions
    {
        public static IServiceCollection AddDictationPresentation(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<App>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<HomeWindow>();
            services.AddSingleton<ThemeManager>();
            services.AddSingleton<ISystemThemeProvider, WindowsSystemThemeProvider>();

            services.AddTransient<LexiconLookupViewModel>();
            services.AddTransient<LexiconLookupWindow>();
            services.AddSingleton<LexiconLookupWindowFactory>();

            services.AddSingleton<MainWindow>();
            services.AddTransient<DictationWindow>();
            services.AddTransient<ResultWindow>();
            services.AddTransient<ReviewWindow>();

            services.AddTransient<WindowFactory>();

            return services;
        }
    }
}
