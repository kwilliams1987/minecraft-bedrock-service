using Microsoft.Extensions.Logging;
using MinecraftBedrockService.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftBedrockService
{
    internal class KeyboardWatcher
    {
        private readonly IBackgroundService _backgroundService;
        private readonly IBackupManager _backupManager;
        private readonly ILogger _logger;

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public KeyboardWatcher(IBackgroundService backgroundService, IBackupManager backupManager, ILogger<KeyboardWatcher> logger)
        {
            _backgroundService = backgroundService;
            _backupManager = backupManager;
            _logger = logger;
        }

        public async Task Run()
        {
            _logger.LogInformation("Hold CTRL + X to exit gracefully.");
            _logger.LogInformation("Hold CTRL + B to force a backup, CTRL + N to cancel a backup.");
            _logger.LogWarning("Press CTRL + C to terminate.");

            while (_backgroundService.IsRunning && !_cancellationTokenSource.IsCancellationRequested)
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
                    _logger.LogTrace(ex, "Input wait was interupted.");
                }

                Thread.MemoryBarrier();

                try
                {
                    await Task.Delay(300, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) 
                {
                    break;
                }
            }

            _backgroundService.Stop();
        }
    }
}
