using Dictation.DataAccess;
using Dictation.Services;
using Dictation.Services.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dictation.Presentation
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();
            try
            {
                host.StartAsync().GetAwaiter().GetResult();
                host.Services.GetRequiredService<StartupInitializer>()
                    .InitializeAsync()
                    .GetAwaiter()
                    .GetResult();

                var viewModel = host.Services.GetRequiredService<ViewModels.HomeViewModel>();
                viewModel.InitializeAsync().GetAwaiter().GetResult();

                var application = host.Services.GetRequiredService<App>();
                var homeWindow = host.Services.GetRequiredService<HomeWindow>();
                application.Run(homeWindow);
            }
            finally
            {
                host.StopAsync().GetAwaiter().GetResult();
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
                        .AddDictationPresentation()
                        .AddDictationCore();
                });
        }
    }
}
