using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Linq;

namespace MinecraftBedrockService;

internal class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService, IBackgroundService
{
    private readonly IOptions<ServerConfig> _configuration;
    private readonly IFileProvider _workingDirectory;
    private readonly ILogger _logger;
    private readonly IServerManager _serverManager;
    private readonly IBackupManager _backupManager;
    private readonly IHost _host;
    private readonly ManualResetEventSlim _exitGate = new();

    private IChangeToken whitelistWatcher;
    private IChangeToken permissionsWatcher;
    private IChangeToken propertiesWatcher;

    public BackgroundService(IOptions<ServerConfig> configuration, IFileProvider fileProvider, ILogger<BackgroundService> logger, IServerManager serverManager, IBackupManager backupManager, IHost host)
    {
        _configuration = configuration;
        _workingDirectory = fileProvider;
        _logger = logger;
        _serverManager = serverManager;
        _backupManager = backupManager;
        _host = host;

        whitelistWatcher = _workingDirectory.Watch("whitelist.json");
        permissionsWatcher = _workingDirectory.Watch("permissions.json");
        propertiesWatcher = _workingDirectory.Watch("server.properties");
    }

    ~BackgroundService()
    {
        _exitGate.Set();
    }

    public async override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting service wrapper.");
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await _serverManager.StartServerAsync())
        {
            _logger.LogError("Failed to start server.");
            await _host.StopAsync(CancellationToken.None);

            return;
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        if (_configuration.Value.BackupInterval > TimeSpan.Zero && !string.IsNullOrWhiteSpace(_configuration.Value.BackupDirectory))
        {
            _logger.LogInformation("Starting backup monitor.");
            await _backupManager.StartWatchingAsync();
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        _exitGate.Wait(CancellationToken.None);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await StartServerShutdownAsync("Server shutdown in {0}.", 30, 20, 10, 5, 3, 2, 1);            
        _exitGate.Set();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StartServerShutdownAsync(string messageTemplate, params int[] checkpointSeconds)
    {
        await SendTimedMessageAsync(messageTemplate, checkpointSeconds);

        await _backupManager.StopWatchingAsync();
        await _backupManager.CancelBackupAsync();
        await _serverManager.StopServerAsync();
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
