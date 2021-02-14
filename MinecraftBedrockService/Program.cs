using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;

namespace MinecraftBedrockService
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "App is only designed for Windows platform.")]
    class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var serviceConfig = configuration.Get<ServiceConfig>() ?? new ServiceConfig();

            var fileProvider = new PhysicalFileProvider(serviceConfig.WorkingDirectory);
            var loggerConfiguration = new LoggerConfiguration()
                .WriteTo.File(fileProvider.GetFileInfo(serviceConfig.LogFileName).PhysicalPath);

            if (Environment.UserInteractive)
            {
                loggerConfiguration.WriteTo.Console();
            }

            if (Debugger.IsAttached)
            {
                loggerConfiguration.WriteTo.Debug();
            }

            var services = new ServiceCollection()
                    .AddLogging(l => l.AddSerilog(loggerConfiguration.CreateLogger(), true))
                    .AddSingleton<IConfiguration>(configuration)
                    .AddSingleton<IFileProvider>(fileProvider)
                    .BuildServiceProvider();

            var serverWrapper = new ServerWrapper(services);

            if (Environment.UserInteractive)
            {
                var logger = services.GetService<ILogger<Program>>();

                logger.LogInformation("Starting wrapper in console mode.");
                serverWrapper.Start();
                if (serverWrapper.Running)
                {
                    logger.LogInformation("Server running, press X to terminate.");

                    var key = new ConsoleKeyInfo();
                    while (key.Modifiers != ConsoleModifiers.Control || key.Key != ConsoleKey.X)
                    {
                        key = Console.ReadKey(true);
                    }

                    serverWrapper.Stop();
                }
            }
            else
            {
                ServiceBase.Run(new ServerWrapper(services));
            }
        }
    }
}
