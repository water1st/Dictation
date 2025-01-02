using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Presentation
{
    internal static class Program
    {
        private static IServiceProvider? ServiceProvider;
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Run the application
            ApplicationConfiguration.Initialize();
            using (var scope = ServiceProvider.CreateScope())
            {
                var mainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
                Application.Run(mainWindow);
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Register application services
            services.AddDictationPresentation();
            services.AddDictationCore();

        }

    }
}