using Dictation.DataAccess;
using Dictation.Services;
using Dictation.Services.SingleInstance;
using Dictation.Services.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dictation.Presentation
{
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

            var homeWindow = host.Services.GetRequiredService<HomeWindow>();
            Action activationHandler = homeWindow.RestoreAndActivate;
            var hostStarted = false;
            try
            {
                instanceCoordinator.ActivationRequested += activationHandler;
                _ = instanceCoordinator.StartListeningAsync();

                host.StartAsync().GetAwaiter().GetResult();
                hostStarted = true;
                host.Services.GetRequiredService<StartupInitializer>()
                    .InitializeAsync()
                    .GetAwaiter()
                    .GetResult();

                var viewModel = host.Services.GetRequiredService<ViewModels.HomeViewModel>();
                viewModel.InitializeAsync().GetAwaiter().GetResult();

                var application = host.Services.GetRequiredService<App>();
                application.Run(homeWindow);
            }
            finally
            {
                instanceCoordinator.ActivationRequested -= activationHandler;
                if (hostStarted)
                {
                    host.StopAsync().GetAwaiter().GetResult();
                }
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services
                        .AddDictationDataAccess(AppContext.BaseDirectory)
                        .AddDictationServices()
                        .AddLocalTtsHost(Path.Combine(AppContext.BaseDirectory, "Dictation.TtsHost.exe"))
                        .AddDictationPresentation()
                        .AddDictationCore();
                });
        }
    }
}
