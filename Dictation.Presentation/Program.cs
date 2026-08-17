using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Services;
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
                var applicationPaths = host.Services.GetRequiredService<ApplicationPaths>();
                applicationPaths.EnsureRuntimeDirectories();

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
