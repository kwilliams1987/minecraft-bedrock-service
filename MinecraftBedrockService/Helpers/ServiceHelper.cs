﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Management;
using System.Reflection;
using System.Security.Principal;
using System.Text;

namespace MinecraftBedrockService.Helpers;

internal class ServiceHelper
{
    private static PlatformID Platform => Environment.OSVersion.Platform;

    private readonly IOptions<ServiceConfig> _serviceConfig;
    private readonly IOptions<ServerConfig> _serverConfig;
    private readonly ILogger _logger;

    private readonly string _filename;
    private readonly ServerConfig _serverDefaults;

    public ServiceHelper(IOptions<ServiceConfig> serviceConfig, IOptions<ServerConfig> serverConfig, ILogger<ServiceHelper> logger)
    {
        _serviceConfig = serviceConfig;
        _serverConfig = serverConfig;
        _logger = logger;

        _filename = Assembly.GetEntryAssembly()?.Location ?? throw new NotSupportedException(ServiceErrorNoEntryPoint);
        _serverDefaults = new ServerConfig()
        {
            WorkingDirectory = Path.GetDirectoryName(_filename) ?? throw new NotSupportedException(ServiceErrorNoDirectory)
        };
    }

    public async Task CreateServiceAsync()
    {
        var arguments = new StringBuilder();

        void AddIfNotDefault<T>(Expression<Func<ServerConfig, T>> selector) where T : IEquatable<T>
        {
            if (selector.Body is MemberExpression expression)
            {
                var func = selector.Compile();
                var parameter = expression.Member.Name;

                var customValue = func(_serverConfig.Value);
                var defaultValue = func(_serverDefaults);

                if (!customValue.Equals(defaultValue))
                {
                    arguments.Append($@" --{char.ToLower(parameter[0]) + parameter[1..]} \""{customValue}\""");
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        AddIfNotDefault(c => c.BackupDirectory);
        AddIfNotDefault(c => c.BackupInterval);
        AddIfNotDefault(c => c.Executable);
        AddIfNotDefault(c => c.LogFileName);
        AddIfNotDefault(c => c.WorkingDirectory);

        switch (Platform)
        {
            case PlatformID.Win32NT:
                var binPath = $@"\""{_filename}\"" {arguments}".Trim();

                var processInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),  ServiceManagerExecutable),
                    Arguments = string.Format(ServiceManagerArguments, _serviceConfig.Value.ServiceName, binPath, _serviceConfig.Value.Description, _serviceConfig.Value.RunAs),
                    CreateNoWindow = false,
                    UseShellExecute = true
                };

                var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    processInfo.Verb = "RunAs";
                }

                try
                {
                    var process = Process.Start(processInfo);

                    if (process == null)
                    {
                        _logger.LogCritical(ServiceErrorServiceManagerFailed);
                        return;
                    }

                    await process.WaitForExitAsync().ConfigureAwait(false);

                    switch (process.ExitCode)
                    {
                        case 0:
                            _logger.LogInformation(ServiceCreated, _serviceConfig.Value.ServiceName);
                            return;

                        case 1073:
                            _logger.LogError(ServiceAlreadyExists, _serviceConfig.Value.ServiceName);
                            return;

                        default:
                            _logger.LogError(ServiceUnknownExitCode, process.ExitCode);
                            return;
                    }
                }
                catch (Win32Exception ex)
                {
                    switch (ex.ErrorCode)
                    {
                        case -2147467259:
                            _logger.LogWarning(ServiceErrorUACAbort);
                            return;

                        default:
                            _logger.LogError(ServiceUnknownError, ex.Message);
                            return;
                    }
                }

            default:
                _logger.LogError(ServicePlatformNotSupported, Platform);
                return;
        }            
    }

    public static string GetServiceNameAsync()
    {
        switch (Platform)
        {
            case PlatformID.Win32NT:
                // Calling System.ServiceProcess.ServiceBase::ServiceNamea always returns an empty string,
                // see https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=387024

                // So we have to do some more work to find out our service name, this only works if
                // the process contains a single service, if there are more than one services hosted
                // in the process you will have to do something else

                var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service where ProcessId = {Environment.ProcessId}");

                foreach (var result in searcher.Get())
                {
                    var value = result["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                return string.Empty;

            default:
                throw new PlatformNotSupportedException();
        }
    }
}