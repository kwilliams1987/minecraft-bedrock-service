using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

namespace MinecraftBedrockService
{
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "App is only designed for Windows platform.")]
    internal class ServerWrapper : ServiceBase
    {
        private readonly IConfiguration configuration;
        private readonly IFileProvider fileProvider;
        private readonly ILogger logger;
        private readonly ManualResetEventSlim resetEvent;

        private Process ServerProcess;

        private IChangeToken whitelistWatcher;
        private IChangeToken permissionsWatcher;
        private IChangeToken propertiesWatcher;

        private long playerCount = 0;

        public bool Running { get; private set; } = false;
        public bool ExitRequested { get; private set; } = false;

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
            resetEvent = new ManualResetEventSlim();

            whitelistWatcher = fileProvider.Watch("whitelist.json");
            whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);

            permissionsWatcher = fileProvider.Watch("permissions.json");
            permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);

            propertiesWatcher = fileProvider.Watch("server.properties");
            propertiesWatcher.RegisterChangeCallback(PropertiesChangedCallback, null);
        }

        public void Start()
        {
            Running = true;

            logger.LogInformation("Starting service wrapper.");

            if (!StartServer())
            {
                Stop();
            }
        }

        public void WaitForExit() => resetEvent.Wait();

        public long CurrentPlayerCount() => Interlocked.Read(ref playerCount);

        protected override void OnStart(string[] args) => new Thread(Start).Start();

        protected override void OnShutdown()
        {
            if (CurrentPlayerCount() != 0)
            {
                SendServerCountdown("Server shutdown in {0}.", 15, 10, 5);
            }

            StopServer();

            Running = false;
            resetEvent.Set();
        }

        protected override void OnStop()
        {
            if (CurrentPlayerCount() != 0)
            {
                SendServerCountdown("Server shutdown in {0}.", 30, 20, 10, 5, 3, 2, 1);
            }

            StopServer();

            Running = false;
            resetEvent.Set();
        }

        private bool StartServer()
        {
            if (ServerProcess != null && !ServerProcess.HasExited)
            {
                logger.LogWarning("Attempted to start server when it is already running.");
                return true;
            }

            var serverConfig = configuration.Get<ServiceConfig>() ?? new ServiceConfig();
            var serverExecutable = fileProvider.GetFileInfo(serverConfig.Executable);

            if (serverExecutable.Exists)
            {
                ServerProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        CreateNoWindow = true,
                        FileName = serverExecutable.PhysicalPath,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };

                try
                {
                    ServerProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
                    ServerProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;

                    logger.LogInformation("Starting {0}.", serverExecutable.PhysicalPath);
                    ServerProcess.Start();

                    logger.LogInformation("Hooking console output.");
                    ServerProcess.BeginOutputReadLine();

                    StartServerHeartbeat();
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start the server.");
                    ServerProcess.Kill();

                    return false;
                }
            }
            else
            {
                logger.LogError("Could not find the {0} executable in working directory {1}.", serverConfig.Executable, serverConfig.WorkingDirectory);
                return false;
            }
        }

        private void StopServer()
        {
            ExitRequested = true;
            try
            {
                if (ServerProcess != null && !ServerProcess.HasExited)
                {
                    logger.LogInformation("Sending stop command to server.");
                    SendServerCommand("stop");
                    ServerProcess.WaitForExit();
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Failed to stop server gracefully, forcing exit.");
                ServerProcess.Kill();
            }
            finally
            {
                Interlocked.Exchange(ref playerCount, 0);
                ServerProcess.Dispose();
                ServerProcess = null;
            }
        }

        private void SendServerCountdown(string messageTemplate, params int[] checkpointSeconds)
        {
            var checkpoints = checkpointSeconds.Select(c => TimeSpan.FromSeconds(c)).OrderByDescending(c => c).ToArray();
            for (var c = 0; c < checkpoints.Length; c++)
            {
                var current = checkpoints.ElementAt(c);
                var next = checkpoints.ElementAtOrDefault(c + 1);

                var delta = current - next;
                SendServerMessage(messageTemplate, current);

                Thread.Sleep(delta);
            }
        }

        private void SendServerMessage(string message, params object[] args) => SendServerCommand($"say {string.Format(message, args)}");

        private void SendServerCommand(string command)
        {
            if (ServerProcess != null && !ServerProcess.HasExited)
            {
                ServerProcess.StandardInput.WriteLine(command);
            }
            else
            {
                logger.LogError("Attempted to send {0} to server but process is not running.");
            }
        }

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                if (e.Data.Contains("Player connected: "))
                {
                    Interlocked.Increment(ref playerCount);
                }

                if (e.Data.Contains("Player disconnected: "))
                {
                    Interlocked.Decrement(ref playerCount);
                }

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

        private void StartServerHeartbeat()
        {
            new Thread(_ =>
            {
                while (ServerProcess != null && ServerProcess.HasExited != true)
                {
                    Thread.Sleep(300);
                }

                if (!ExitRequested)
                {
                    logger.LogError("Server exited unexpectly, restarting");

                    ServerProcess?.Dispose();
                    ServerProcess = null;

                    StartServer();
                }

                ExitRequested = false;
            }).Start();
        }

        private void WhitelistChangedCallback(object state)
        {
            logger.LogInformation("Reloading whitelist.");
            SendServerCommand("whitelist reload");

            whitelistWatcher = fileProvider.Watch("whitelist.json");
            whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);
        }

        private void PermissionsChangedCallback(object state)
        {
            logger.LogInformation("Reloading permissions.");
            SendServerCommand("permission reload");

            permissionsWatcher = fileProvider.Watch("permissions.json");
            permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);
        }

        private void PropertiesChangedCallback(object state)
        {
            logger.LogInformation("Server properties changed, triggering server restart.");

            if (CurrentPlayerCount() != 0)
            {
                SendServerCountdown("Server restart in {0}.", 30, 20, 10, 5, 3, 2, 1);
            }

            SendServerMessage("Restarting server now.");
            StopServer();
            StartServer();

            propertiesWatcher = fileProvider.Watch("server.properties");
            propertiesWatcher.RegisterChangeCallback(PropertiesChangedCallback, null);
        }
    }
}
