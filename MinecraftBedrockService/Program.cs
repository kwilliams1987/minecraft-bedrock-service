using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftBedrockService.Configuration;
using MinecraftBedrockService.Helpers;
using MinecraftBedrockService.Interfaces;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace MinecraftBedrockService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var serverConfig = configuration.Get<ServerConfig>() ?? new ServerConfig();
            
            var services = new ServiceCollection()
                    .AddOptions<ServerConfig>().Bind(configuration).Services
                    .AddOptions<ServiceConfig>().Bind(configuration).Services
                    .AddLogging(serverConfig)
                    .AddSingleton<IFileProvider>(new PhysicalFileProvider(serverConfig.WorkingDirectory))
                    .AddSingleton<IServerManager, ServerManager>()
                    .AddSingleton<IBackupManager, BackupManager>()
                    .AddSingleton<IConfigurationWatcher, ConfigurationWatcher>()
                    .AddSingleton<IBackgroundService>(c => c.GetService<BackgroundService>())
                    .AddSingleton<BackgroundService>()
                    .AddSingleton<KeyboardWatcher>()
                    .AddSingleton<ServiceHelper>()
                    .BuildServiceProvider();

            var logger = services.GetService<ILogger<Program>>();
            var serviceConfig = services.GetService<IOptions<ServiceConfig>>();

            if (serviceConfig.Value.CreateService)
            {
                var serviceHelper = services.GetRequiredService<ServiceHelper>();
                await serviceHelper.CreateServiceAsync().ConfigureAwait(false);
                return;
            }

            logger.LogInformation("Minecraft Bedrock Service Manager v{version}", typeof(Program).Assembly.GetName().Version);
            logger.LogInformation("Creating service wrapper...");
            var backgroundService = services.GetService<BackgroundService>();

            if (WindowsServiceHelpers.IsWindowsService())
            {
                logger.LogInformation("Starting wrapper in {modeType} mode.", "service");
                ServiceBase.Run(backgroundService);
            }
            else
            {
                var keyboardWatcher = services.GetService<KeyboardWatcher>();
                logger.LogInformation("Starting wrapper in {modeType} mode.", "console");
                
                backgroundService.Start();

                if (backgroundService.IsRunning)
                {
                    await keyboardWatcher.Run();
                }
            }

            logger.LogInformation("Shutting down.");
        }
    }
}
