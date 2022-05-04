using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinecraftBedrockService;

internal class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService, IBackgroundService
{
    private readonly IOptions<ServerConfig> _configuration;
    private readonly ILogger _logger;
    private readonly IServerManager _serverManager;
    private readonly IBackupManager _backupManager;
    private readonly IHost _host;
    private readonly ManualResetEventSlim _exitGate = new();

    public BackgroundService(IOptions<ServerConfig> configuration, ILogger<BackgroundService> logger, IServerManager serverManager, IBackupManager backupManager, IHost host)
    {
        _configuration = configuration;
        _logger = logger;
        _serverManager = serverManager;
        _backupManager = backupManager;
        _host = host;
    }

    ~BackgroundService()
    {
        _exitGate.Set();
    }

    public async override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(ServiceWrapperStarting);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await _serverManager.StartServerAsync())
        {
            _logger.LogError(ServerStartFailed);
            await _host.StopAsync(CancellationToken.None);

            return;
        }

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        if (_configuration.Value.BackupInterval > TimeSpan.Zero && !string.IsNullOrWhiteSpace(_configuration.Value.BackupDirectory))
        {
            _logger.LogInformation(BackupManagerStarting);
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
        _logger.LogInformation(ServiceWrapperStopping);

        await _backupManager.StopWatchingAsync();
        await _backupManager.CancelBackupAsync();

        await _serverManager.SendTimedMessageAsync(ServerShutdownTimer, TimeSpan.FromSeconds(30));
        await _serverManager.StopServerAsync(TimeSpan.FromSeconds(30));
        _exitGate.Set();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
