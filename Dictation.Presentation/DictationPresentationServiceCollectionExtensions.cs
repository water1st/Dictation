using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Importing;
using Dictation.Presentation.Practice;
using Dictation.Core.Tts;
using Dictation.Core.Configuration;
using Dictation.Presentation.Mcp;
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
            services.AddSingleton<LandingViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddSingleton<NavigationService>();
            services.AddSingleton<IWorkspaceNavigation>(provider => provider.GetRequiredService<NavigationService>());
            services.AddSingleton<ISettingsNavigation>(provider => provider.GetRequiredService<NavigationService>());
            services.AddSingleton<IMcpSettingsNavigation>(provider => provider.GetRequiredService<NavigationService>());
            services.AddSingleton<IStatisticsNavigation>(provider => provider.GetRequiredService<NavigationService>());
            services.AddScoped<StatisticsViewModel>();
            services.AddSingleton<IHomeNavigation>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<IKanaSessionNavigation>(serviceProvider => serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<IWrongAnswerNavigation>(serviceProvider =>
                serviceProvider.GetRequiredService<NavigationService>());
            services.AddSingleton<ILearningSessionNavigation>(serviceProvider =>
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
            services.AddSingleton<Dictation.Presentation.Updating.IUpdateLauncher, Dictation.Presentation.Updating.UpdateLauncher>();
            services.AddSingleton<UpdateViewModel>();
            services.AddSingleton<ThemeManager>();
            services.AddSingleton<ISystemThemeProvider, WindowsSystemThemeProvider>();
            services.AddSingleton<IAudioPlayer, MemoryWavAudioPlayer>();

            services.AddScoped<LexiconLookupViewModel>();
            services.AddScoped<McpSettingsViewModel>(provider => new McpSettingsViewModel(
                provider.GetRequiredService<McpSettingsService>(), provider.GetRequiredService<IMcpHostControl>(),
                provider.GetRequiredService<IMcpStatusReader>(), provider.GetRequiredService<CodexMcpRegistrationService>(),
                Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } codexDirectory
                    ? codexDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"), "config.toml")));
            services.AddScoped<TextImportViewModel>();
            services.AddScoped<ReviewLibraryViewModel>();
            services.AddScoped<WrongAnswerListViewModel>();
            services.AddScoped<ReviewPlanViewModel>();
            services.AddScoped<KanaPlanViewModel>();
            services.AddScoped<KanaSessionViewModel>();
            services.AddSingleton<Utf8TextFileReader>();
            services.AddSingleton<StartupTextImport>();
            services.AddScoped<ImportInboxListViewModel>();
            services.AddScoped<ImportInboxDetailViewModel>();
            services.AddScoped<LearningPlanViewModel>();
            services.AddScoped<LearningPracticeWorkflow>();
            services.AddScoped<ReviewPracticeWorkflow>();
            services.AddScoped<WrongAnswerPracticeWorkflow>();
            services.AddScoped<PracticeSessionViewModel>(provider => new PracticeSessionViewModel(
                provider.GetRequiredService<LearningPracticeWorkflow>(), provider.GetRequiredService<TtsService>(),
                provider.GetRequiredService<IAudioPlayer>()));
            services.AddKeyedScoped<PracticeSessionViewModel>(PracticeSessionMode.Review, (provider, _) => new PracticeSessionViewModel(
                provider.GetRequiredService<ReviewPracticeWorkflow>(), provider.GetRequiredService<TtsService>(),
                provider.GetRequiredService<IAudioPlayer>()));
            services.AddKeyedScoped<PracticeSessionViewModel>(PracticeSessionMode.WrongAnswers, (provider, _) => new PracticeSessionViewModel(
                provider.GetRequiredService<WrongAnswerPracticeWorkflow>(), provider.GetRequiredService<TtsService>(),
                provider.GetRequiredService<IAudioPlayer>()));

            return services;
        }
    }
}
