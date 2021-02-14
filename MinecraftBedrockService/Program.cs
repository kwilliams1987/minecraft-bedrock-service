using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace MinecraftBedrockService
{
    class Program
    {
        static void Main(string[] args)
        {
            var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .AddJsonFile(fileProvider, "settings.json", true, false)
                .Build();

            var loggerConfiguration = new LoggerConfiguration()
                .WriteTo.File(fileProvider.GetFileInfo("server_wrapper.log").PhysicalPath);

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
