using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
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
            services.AddSingleton<NavigationService>();
            services.AddSingleton<OverlayService>();
            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<HomeWindow>();
            services.AddSingleton<ThemeManager>();
            services.AddSingleton<ISystemThemeProvider, WindowsSystemThemeProvider>();

            services.AddScoped<LexiconLookupViewModel>();
            services.AddScoped<ImportInboxListViewModel>();
            services.AddScoped<ImportInboxDetailViewModel>();
            services.AddScoped<LearningPlanViewModel>();

            return services;
        }
    }
}
