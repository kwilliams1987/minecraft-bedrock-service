using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management;
using System.ServiceProcess;
using System.Threading;

namespace MinecraftBedrockService
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "App is only designed for Windows platform.")]
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
                .WriteTo.File(fileProvider.GetFileInfo(serviceConfig.LogFileName).PhysicalPath);

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
                    .AddLogging(l => l.AddSerilog(loggerConfiguration.CreateLogger(), true))
                    .AddSingleton<IConfiguration>(configuration)
                    .AddSingleton<IFileProvider>(fileProvider)
                    .BuildServiceProvider();

            var logger = services.GetService<ILogger<Program>>();

            logger.LogInformation("Creating service wrapper...");
            var serverWrapper = new ServerWrapper(services);

            if (runningAsService)
            {
                logger.LogInformation("Starting wrapper in {0} mode.", "service");
                ServiceBase.Run(serverWrapper);
            }
            else
            {
                logger.LogInformation("Starting wrapper in {0} mode.", "console");

                new Thread(serverWrapper.Start).Start();
                new Thread(_ =>
                {
                    var key = new ConsoleKeyInfo();
                    logger.LogInformation("Press CTRL + X to exit gracefully.");
                    logger.LogWarning("Press CTRL + C to terminate.");
                    while (key.Modifiers != ConsoleModifiers.Control || key.Key != ConsoleKey.X)
                    {
                        key = Console.ReadKey(true);
                    }

                    serverWrapper.Stop();
                }).Start();
            }

            serverWrapper.WaitForExit();
            logger.LogInformation("Shutting down.");
        }

        protected static string GetServiceName()
        {
            // Calling System.ServiceProcess.ServiceBase::ServiceNamea allways returns
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
