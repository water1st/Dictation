using Dictation.DataAccess;
using Dictation.Core;
using Dictation.Core.SingleInstance;
using Dictation.Core.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dictation.Presentation
{
    /// <summary>
    /// 创建唯一 Generic Host，完成单实例、业务初始化和 WPF 启动。
    /// </summary>
    internal static class Program
    {
        private static readonly TimeSpan SecondaryActivationTimeout = TimeSpan.FromMilliseconds(750);

        [STAThread]
        private static void Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();
            var instanceCoordinator = host.Services.GetRequiredService<SingleInstanceCoordinator>();
            if (!instanceCoordinator.TryAcquirePrimaryInstance())
            {
                instanceCoordinator.TrySignalPrimaryInstanceAsync(SecondaryActivationTimeout)
                    .GetAwaiter()
                    .GetResult();
                return;
            }

            var application = host.Services.GetRequiredService<App>();
            var homeWindow = default(HomeWindow);
            Action? activationHandler = null;
            var hostStarted = false;
            try
            {
                host.StartAsync().GetAwaiter().GetResult();
                hostStarted = true;
                host.Services.GetRequiredService<StartupInitializer>()
                    .InitializeAsync()
                    .GetAwaiter()
                    .GetResult();

                var viewModel = host.Services.GetRequiredService<ViewModels.HomeViewModel>();
                viewModel.InitializeAsync().GetAwaiter().GetResult();
                var navigationService = host.Services.GetRequiredService<Navigation.NavigationService>();
                navigationService.InitializeHome(viewModel);
                homeWindow = host.Services.GetRequiredService<HomeWindow>();
                activationHandler = homeWindow.RestoreAndActivate;
                instanceCoordinator.ActivationRequested += activationHandler;
                _ = instanceCoordinator.StartListeningAsync();

                application.Run(homeWindow);
            }
            finally
            {
                if (activationHandler is not null)
                {
                    instanceCoordinator.ActivationRequested -= activationHandler;
                }
                if (hostStarted)
                {
                    host.StopAsync().GetAwaiter().GetResult();
                }
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.SetBasePath(AppContext.BaseDirectory);
                    configuration.AddJsonFile(source =>
                    {
                        source.Path = Path.Combine("CONFIG", "settings.json");
                        source.Optional = true;
                        source.ReloadOnChange = false;
                        source.OnLoadException = context => context.Ignore = true;
                    });
                })
                .ConfigureServices((_, services) =>
                {
                    services
                        .AddDictationDataAccess(AppContext.BaseDirectory)
                        .AddDictationCore()
                        .AddLocalTtsHost(Path.Combine(AppContext.BaseDirectory, "Dictation.TtsHost.exe"))
                        .AddDictationPresentation();
                });
        }
    }
}
