using Microsoft.Extensions.Hosting.WindowsServices;
using MinecraftBedrockService.Configuration;
using MinecraftBedrockService.Helpers;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Extensions.DependencyInjection
{
    internal static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddLogging(this IServiceCollection services, ServerConfig serviceConfig)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var loggerConfiguration = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(serviceConfig.WorkingDirectory, serviceConfig.LogFileName));

            if (WindowsServiceHelpers.IsWindowsService())
            {
                loggerConfiguration.WriteTo.EventLog(ServiceHelper.GetServiceNameAsync(), restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
            }
            else
            {
                loggerConfiguration.WriteTo.Console();
            }

            if (Debugger.IsAttached)
            {
                loggerConfiguration.WriteTo.Debug();
            }

            return services.AddLogging(l => l.AddSerilog(loggerConfiguration.CreateLogger(), true));
        }
    }
}
