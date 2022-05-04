﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MinecraftBedrockService;

internal class KeyboardWatcherService: Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IServerManager _serverManager;
    private readonly IBackupManager _backupManager;
    private readonly ILogger _logger;
    private readonly IHost _host;

    public KeyboardWatcherService(IServerManager serverManager, IBackupManager backupManager, ILogger<KeyboardWatcherService> logger, IHost host)
    {
        _serverManager = serverManager;
        _backupManager = backupManager;
        _logger = logger;
        _host = host;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_serverManager.CurrentState != ServerState.Stopped && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(KeyboardShortcutCtrlX);
            _logger.LogInformation(KeyboardShortcutCtrlBCtrlN);
        }

        while (_serverManager.CurrentState != ServerState.Stopped && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Modifiers == ConsoleModifiers.Control)
                    {
                        if (key.Key == ConsoleKey.B)
                        {
                            await _backupManager.CreateBackupAsync();
                        }

                        if (key.Key == ConsoleKey.N)
                        {
                            await _backupManager.CancelBackupAsync();
                        }

                        if (key.Key == ConsoleKey.X)
                        {
                            break;
                        }
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogTrace(ex, KeyboardInputInterupted);
            }

            Thread.MemoryBarrier();
        }

        await _host.StopAsync(CancellationToken.None);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        base.Dispose();
    }
}
