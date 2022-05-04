using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace MinecraftBedrockService;

class Program
{
    static async Task Main(string[] args)
    {
        var host = ConfigureHost(args);
        var services = host.Services;

        var logger = services.GetRequiredService<ILogger<Program>>();
        var serviceConfig = services.GetRequiredService<IOptions<ServiceConfig>>();

        if (serviceConfig.Value.CreateService)
        {
            var serviceHelper = services.GetRequiredService<ServiceHelper>();
            await serviceHelper.CreateServiceAsync().ConfigureAwait(false);
            return;
        }

        logger.LogInformation("Minecraft Bedrock Service Manager v{version}", typeof(Program).Assembly.GetName().Version);
        logger.LogInformation("Creating service wrapper...");

        if (WindowsServiceHelpers.IsWindowsService())
        {
#if DEBUG
            Debugger.Break();
#endif

            logger.LogInformation("Starting wrapper in {modeType} mode.", "service");
        }
        else
        {
            logger.LogInformation("Starting wrapper in {modeType} mode.", "console");
        }

        await host.StartAsync();
        await host.WaitForShutdownAsync();
        logger.LogInformation("Shutting down.");
    }

    private static IHost ConfigureHost(string[] args)
    {
        var hostBuilder = new HostBuilder()
                        .ConfigureAppConfiguration(config => config
                            .AddCommandLine(args))
                        .ConfigureLogging((ctx, logging) =>
                        {
                            var config = ctx.Configuration.Get<ServerConfig>();
                            var loggerConfiguration = new LoggerConfiguration()
                                .WriteTo.File(Path.Combine(config.WorkingDirectory, config.LogFileName));

                            loggerConfiguration.WriteTo.Console();

                            if (Debugger.IsAttached)
                            {
                                loggerConfiguration.WriteTo.Debug();
                            }

                            logging.AddSerilog(loggerConfiguration.CreateLogger(), true);
                        })
                        .ConfigureServices((ctx, services) =>
                        {
                            services
                                .AddOptions<ServerConfig>().Bind(ctx.Configuration).Services
                                .AddOptions<ServiceConfig>().Bind(ctx.Configuration).Services
                                .AddHostedService(c => c.GetRequiredService<IBackgroundService>())
                                .AddSingleton<IBackgroundService, BackgroundService>()
                                .AddSingleton<IFileProvider>(s => new PhysicalFileProvider(s.GetRequiredService<IOptions<ServerConfig>>().Value.WorkingDirectory))
                                .AddSingleton<IServerManager, ServerManager>()
                                .AddSingleton<IBackupManager, BackupManager>()
                                .AddSingleton<IConfigurationWatcher, ConfigurationWatcher>()
                                .AddSingleton<ServiceHelper>();

                            if (!WindowsServiceHelpers.IsWindowsService())
                            {
                                services.AddHostedService<KeyboardWatcherService>();
                            }
                        })
                        .UseWindowsService()
                        .UseConsoleLifetime();

        return hostBuilder.Build();
    }
}
