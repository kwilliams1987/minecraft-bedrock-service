using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using MinecraftBedrockService.Interfaces;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.ServiceProcess;
using System.Threading;

namespace MinecraftBedrockService
{
    class Program
    {
        static void Main(string[] args)
        {
            var runningAsService = WindowsServiceHelpers.IsWindowsService();
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var serviceConfig = configuration.Get<ServiceConfig>() ?? new ServiceConfig();

            var fileProvider = new PhysicalFileProvider(serviceConfig.WorkingDirectory);
            var loggerConfiguration = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(serviceConfig.WorkingDirectory, serviceConfig.LogFileName));

            if (runningAsService)
            {
                loggerConfiguration.WriteTo.EventLog(GetServiceName());
            }
            else
            {
                loggerConfiguration.WriteTo.Console();
            }

            if (Debugger.IsAttached)
            {
                loggerConfiguration.WriteTo.Debug();
            }

            var services = new ServiceCollection()
                    .AddOptions<ServiceConfig>().Bind(configuration).Services
                    .AddLogging(l => l.AddSerilog(loggerConfiguration.CreateLogger(), true))
                    .AddSingleton<IConfiguration>(configuration)
                    .AddSingleton<IFileProvider>(fileProvider)
                    .AddSingleton<IServerManager, ServerManager>()
                    .AddSingleton<IBackupManager, BackupManager>()
                    .AddSingleton<IConfigurationWatcher, ConfigurationWatcher>()
                    .AddSingleton<BackgroundService>()
                    .BuildServiceProvider();

            var logger = services.GetService<ILogger<Program>>();

            logger.LogInformation("Creating service wrapper...");
            var backgroundService = services.GetService<BackgroundService>();
            var backupManager = services.GetService<IBackupManager>();

            if (runningAsService)
            {
                logger.LogInformation("Starting wrapper in {modeType} mode.", "service");
                ServiceBase.Run(backgroundService);
            }
            else
            {
                logger.LogInformation("Starting wrapper in {modeType} mode.", "console");

                new Thread(backgroundService.Start).Start();
                new Thread(_ =>
                {
                    var spinWait = new SpinWait();
                    var key = new ConsoleKeyInfo();
                    logger.LogInformation("Press CTRL + X to exit gracefully.");
                    logger.LogInformation("Press CTRL + B to force a backup, CTRL + N to cancel a backup.");
                    logger.LogWarning("Press CTRL + C to terminate.");

                    while (backgroundService.IsRunning)
                    {
                        try
                        {
                            if (Console.KeyAvailable)
                            {
                                key = Console.ReadKey(true);
                                if (key.Modifiers == ConsoleModifiers.Control)
                                {
                                    if (key.Key == ConsoleKey.B)
                                    {
                                        backupManager.CreateBackupAsync();
                                    }

                                    if (key.Key == ConsoleKey.N)
                                    {
                                        backupManager.CancelBackupAsync();
                                    }

                                    if (key.Key == ConsoleKey.X)
                                    {
                                        backgroundService.Stop();
                                        break;
                                    }
                                }                                
                            }
                        }
                        catch (InvalidOperationException ex)
                        {
                            logger.LogTrace(ex, "Input wait was interupted.");
                        }

                        Thread.MemoryBarrier();
                        spinWait.SpinOnce();
                    }
                }).Start();
            }

            backgroundService.WaitForExit();
            logger.LogInformation("Shutting down.");
        }

        protected static string GetServiceName()
        {
            // Calling System.ServiceProcess.ServiceBase::ServiceNamea always returns
            // an empty string,
            // see https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=387024

            // So we have to do some more work to find out our service name, this only works if
            // the process contains a single service, if there are more than one services hosted
            // in the process you will have to do something else
            
            var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service where ProcessId = {Environment.ProcessId}");
            foreach (var result in searcher.Get())
            {
                return result["Name"].ToString();
            }

            return string.Empty;
        }
    }
}
