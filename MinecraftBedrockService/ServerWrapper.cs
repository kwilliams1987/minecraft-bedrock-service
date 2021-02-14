using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace MinecraftBedrockService
{
    internal class ServerWrapper : ServiceBase
    {
        private readonly IConfiguration configuration;
        private readonly IFileProvider fileProvider;
        private readonly ILogger logger;

        private Process ServerProcess;

        private IChangeToken whitelistWatcher;
        private IChangeToken permissionsWatcher;

        public bool Running { get; private set; } = false;

        public ServerWrapper(IServiceProvider services)
        {
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = true;
            CanStop = true;

            configuration = services.GetRequiredService<IConfiguration>();
            fileProvider = services.GetRequiredService<IFileProvider>();
            logger = services.GetRequiredService<ILogger<ServerWrapper>>();

            whitelistWatcher = fileProvider.Watch("whitelist.json");
            whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);

            permissionsWatcher = fileProvider.Watch("permissions.json");
            permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);
        }

        private void WhitelistChangedCallback(object state)
        {
            logger.LogInformation("Reloading whitelist.");
            ServerProcess.StandardInput.WriteLine("whitelist reload");

            whitelistWatcher = fileProvider.Watch("whitelist.json");
            whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);
        }

        private void PermissionsChangedCallback(object state)
        {
            logger.LogInformation("Reloading permissions.");
            ServerProcess.StandardInput.WriteLine("permission reload");

            permissionsWatcher = fileProvider.Watch("permissions.json");
            permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);
        }

        private void ConfigWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            Running = true;

            logger.LogInformation("Starting service wrapper.");
            var serverConfig = configuration.Get<ServerConfig>() ?? new ServerConfig();
            var serverExecutable = fileProvider.GetFileInfo(serverConfig.Executable);

            if (!serverExecutable.Exists)
            {
                logger.LogError("Could not find the {0} executable.", serverConfig.Executable);
                Stop();
                return;
            }

            ServerProcess = Process.Start(new ProcessStartInfo
            {
                FileName = serverExecutable.PhysicalPath,
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false                
            });

            ServerProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
            ServerProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;
        }

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                logger.LogInformation(e.Data);
            }
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                logger.LogError(e.Data);
            }
        }

        protected override void OnStart(string[] args) => new Thread(Start).Start();

        protected override void OnShutdown() => OnStop();

        protected override void OnStop()
        {
            if (ServerProcess != null && !ServerProcess.HasExited)
            {
                ServerProcess.StandardInput.WriteLine("stop");
                ServerProcess.WaitForExit();
            }

            Running = false;
        }
    }
}
