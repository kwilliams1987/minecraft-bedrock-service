using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MinecraftBedrockService.Configuration;
using MinecraftBedrockService.Interfaces;
using System;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftBedrockService
{
    internal class BackgroundService : ServiceBase, IBackgroundService
    {
        private readonly IOptions<ServerConfig> _configuration;
        private readonly IFileProvider _workingDirectory;
        private readonly ILogger _logger;
        private readonly IServerManager _serverManager;
        private readonly IBackupManager _backupManager;
        private readonly ManualResetEventSlim _exitGate = new();

        private IChangeToken whitelistWatcher;
        private IChangeToken permissionsWatcher;
        private IChangeToken propertiesWatcher;

        public bool IsRunning => !_exitGate.IsSet;

        public BackgroundService(IOptions<ServerConfig> configuration, IFileProvider fileProvider, ILogger<BackgroundService> logger, IServerManager serverManager, IBackupManager backupManager)
        {
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = true;
            CanStop = true;

            _configuration = configuration;
            _workingDirectory = fileProvider;
            _logger = logger;
            _serverManager = serverManager;
            _backupManager = backupManager;
        }

        ~BackgroundService()
        {
            _exitGate.Set();
        }

        public async void Start()
        {
            _logger.LogInformation("Starting service wrapper.");

            if (await _serverManager.StartServerAsync())
            {
                if (_configuration.Value.BackupInterval > TimeSpan.Zero && !string.IsNullOrWhiteSpace(_configuration.Value.BackupDirectory))
                {
                    _logger.LogInformation("Starting backup monitor.");
                    await _backupManager.StartWatchingAsync();
                }
            }
            else
            {
                _logger.LogError("Failed to start server.");
                Stop();
            }
        }

        protected override void OnStart(string[] args) => new Thread(Start).Start();

        protected override void OnShutdown()
        {
            StartServerShutdown("Server shutdown in {0}.", 15, 10, 5);
            base.OnShutdown();
        }

        protected override void OnStop()
        {
            StartServerShutdown("Server shutdown in {0}.", 30, 20, 10, 5, 3, 2, 1);
            base.OnStop();
        }

        private void StartServerShutdown(string messageTemplate, params int[] checkpointSeconds)
        {
            Task.Run(async () =>
            {
                await SendTimedMessageAsync(messageTemplate, checkpointSeconds);

                await _backupManager.StopWatchingAsync();
                await _backupManager.CancelBackupAsync();
                await _serverManager.StopServerAsync();

                _exitGate.Set();
            });

            _exitGate.Wait();
        }

        private async Task SendTimedMessageAsync(string messageTemplate, params int[] checkpointSeconds)
        {
            if (await _serverManager.GetPlayerCountAsync() > 0 && checkpointSeconds.Any())
            {
                var checkpoints = checkpointSeconds.Select(c => TimeSpan.FromSeconds(c)).OrderByDescending(c => c).ToArray();
                _logger.LogInformation("Sending countdown timer {duration}.", checkpoints.First());

                for (var c = 0; c < checkpoints.Length; c++)
                {
                    var current = checkpoints.ElementAt(c);
                    await SendServerMessage(messageTemplate, current);

                    var next = checkpoints.ElementAtOrDefault(c + 1);
                    var delta = current - next;

                    if (delta > TimeSpan.Zero)
                    {
                        await Task.Delay(delta);
                    }
                }
            }
        }

        private Task SendServerMessage(string message, params object[] args) => SendServerCommand($"say {string.Format(message, args)}");
        private async Task SendServerCommand(string command) => await _serverManager.SendServerCommandAsync(command);

        private async void WhitelistChangedCallback(object state)
        {
            _logger.LogInformation("Reloading whitelist.");
            await SendServerCommand("whitelist reload");

            whitelistWatcher = _workingDirectory.Watch("whitelist.json");
            whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);
        }

        private async void PermissionsChangedCallback(object state)
        {
            _logger.LogInformation("Reloading permissions.");
            await SendServerCommand("permission reload");

            permissionsWatcher = _workingDirectory.Watch("permissions.json");
            permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);
        }

        private async void PropertiesChangedCallback(object state)
        {
            _logger.LogInformation("Server properties changed, triggering server restart.");

            await SendTimedMessageAsync("Server restart in {0}.", 30, 20, 10, 5, 3, 2, 1);
            await SendServerMessage("Restarting server now.");

            await _serverManager.StopServerAsync();
            await _serverManager.StartServerAsync();

            propertiesWatcher = _workingDirectory.Watch("server.properties");
            propertiesWatcher.RegisterChangeCallback(PropertiesChangedCallback, null);
        }
    }
}
