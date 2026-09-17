using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation
{
    /// <summary>
    /// 注册 WPF 窗口、页面状态及导航和弹层能力别名。
    /// </summary>
    public static class DictationPresentationServiceCollectionExtensions
    {
        /// <summary>
        /// 在现有容器注册单例宿主与能力别名，并按页面作用域注册 ViewModel。
        /// </summary>
        /// <param name="services">应用唯一依赖注入容器的注册集合。</param>
        /// <returns>完成注册的原服务集合。</returns>
        public static IServiceCollection AddDictationPresentation(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<App>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<NavigationService>();
            services.AddSingleton<IHomeNavigation>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<IBackNavigation>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<IImportInboxDetailNavigation>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<INavigationHost>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<OverlayService>();
            services.AddSingleton<IOverlayRequester>(serviceProvider =>
                serviceProvider.GetRequiredService<OverlayService>());
            services.AddSingleton<IOverlayHost>(serviceProvider =>
                serviceProvider.GetRequiredService<OverlayService>());
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
