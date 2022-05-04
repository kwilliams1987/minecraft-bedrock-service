using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MinecraftBedrockService;

internal class BackupManager: IBackupManager
{
    private class ServerStateWaiter : IObserver<ServerState>, IDisposable
    {
        private readonly ServerState _undesiredState;
        private readonly IDisposable _watcher;
        private readonly ManualResetEvent _waiter;

        public ServerStateWaiter(IObservable<ServerState> observable, ServerState undesiredState)
        {
            _undesiredState = undesiredState;
            _watcher = observable.Subscribe(this);
            _waiter = new ManualResetEvent(false);
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }

        public void WaitOne() => _waiter.WaitOne();

        void IObserver<ServerState>.OnCompleted() { }

        void IObserver<ServerState>.OnError(Exception error) { }

        void IObserver<ServerState>.OnNext(ServerState value)
        {
            if (value != _undesiredState)
            {
                _waiter.Set();
            }
        }
    }

    private static readonly TimeSpan ProgressGateDelay = TimeSpan.FromMilliseconds(1500);

    private readonly IOptions<ServerConfig> _configuration;
    private readonly ILogger _logger;
    private readonly IServerManager _serverManager;
    private readonly IFileProvider _workingDirectory;
    private readonly ManualResetEventSlim _backupGate = new(true);
    
    private CancellationTokenSource? backupCancellationToken;
    private CancellationTokenSource? watcherCancellationToken;

    public BackupManager(ILogger<BackupManager> logger, IServerManager serverManager, IFileProvider fileProvider, IOptions<ServerConfig> configuration)
    {
        _logger = logger;
        _serverManager = serverManager;
        _workingDirectory = fileProvider;
        _configuration = configuration;
    }

    public async Task<string> CreateBackupAsync()
    {
        var backupFiles = new Dictionary<string, int>();
        var backupProgressGate = new ManualResetEventSlim();
        var temporaryFolder = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());

        if (backupCancellationToken != null)
        {
            _logger.LogWarning(BackupAlreadyInProgress);
            return string.Empty;
        }

        if (_serverManager.CurrentState == ServerState.Starting)
        {
            _logger.LogInformation(BackupWaitingForServerStart);
            using var waiter = new ServerStateWaiter(_serverManager, ServerState.Starting);
            waiter.WaitOne();
        }

        if (_serverManager.CurrentState != ServerState.Running)
        {
            _logger.LogError(BackupWhenServerNotRunning);
            return string.Empty;
        }

        try
        {
            backupCancellationToken = new CancellationTokenSource();
            var backupDir = Path.Combine(_configuration.Value.WorkingDirectory, _configuration.Value.BackupDirectory);
            Directory.CreateDirectory(backupDir);

            // Block exit threads until backup is completed.
            _backupGate.Reset();

            if (Directory.Exists(temporaryFolder))
            {
                Directory.Delete(temporaryFolder, true);
            }

            if (File.Exists(temporaryFolder))
            {
                File.Delete(temporaryFolder);
            }

            Directory.CreateDirectory(temporaryFolder);

            _logger.LogInformation(BackupStarted);
            var backupPath = $"{Path.Combine(_configuration.Value.WorkingDirectory, _configuration.Value.BackupDirectory, DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss"))}.zip";

            var subscription = _serverManager.Subscribe(new OutputObserver(async message =>
            {
                if (message.Contains(ServerResources.BackupManifest))
                {
                    backupProgressGate.Set();

                    var files = message.Split(", ").Select(f => f.Split(':'));
                    foreach (var file in files)
                    {
                        var newLength = int.Parse(file[1]);

                        if (backupFiles.ContainsKey(file[0]))
                        {
                            if (backupFiles[file[0]] != newLength)
                            {
                                _logger.LogWarning(BackupDuplicateFileFound, file[0], backupFiles[file[0]], newLength);
                                backupFiles[file[0]] = newLength;
                            }
                        }
                        else
                        {
                            backupFiles.Add(file[0], newLength);
                        }
                    }

                    backupProgressGate.Set();
                }

                if (message == ServerResources.BackupNotCompleted)
                {
                    await _serverManager.SendServerCommandAsync(ServerCommands.CommandResumeUpdates).ConfigureAwait(false);
                    await _serverManager.SendServerCommandAsync(ServerCommands.CommandHoldUpdates).ConfigureAwait(false);
                }
            }));

            try
            {
                backupFiles.Clear();
                await _serverManager.SendServerCommandAsync(ServerCommands.CommandHoldUpdates);

                while (!backupProgressGate.IsSet && !backupCancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(ProgressGateDelay, backupCancellationToken.Token);
                    await _serverManager.SendServerCommandAsync(ServerCommands.CommandQueryBackup);
                }
            }
            catch (OperationCanceledException) 
            { 
                _logger.LogWarning(BackupCancelled);
                throw;
            }
            finally
            {
                subscription.Dispose();
            }

            backupProgressGate.WaitOne();

            foreach (var file in backupFiles)
            {
                var source = Path.Combine(_configuration.Value.WorkingDirectory, ServerFiles.WorldsDirectory, file.Key);
                var target = Path.Combine(temporaryFolder, file.Key);

                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? throw new NotSupportedException(BackupUnableToCreateDirectory));

                _logger.LogInformation(BackupCopyingFile, file.Key);
                File.Copy(source, target);

                try
                {
                    _logger.LogInformation(BackupTruncatingFile, file.Value);

                    var bytes = new byte[file.Value];
                    using (var reader = File.OpenRead(target))
                    {
                        await reader.ReadAsync(bytes, backupCancellationToken.Token);
                    }

                    File.Delete(target);

                    using var writer = File.OpenWrite(target);
                    await writer.WriteAsync(bytes, backupCancellationToken.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning(BackupCancelled);

                    throw;
                }
            }

            ZipFile.CreateFromDirectory(temporaryFolder, backupPath);
            _logger.LogInformation(BackupComplete, backupPath);

            return backupPath;
        }
        finally
        {
            await _serverManager.SendServerCommandAsync(ServerCommands.CommandResumeUpdates).ConfigureAwait(false);
            Directory.Delete(temporaryFolder, true);
            _backupGate.Set();
            backupCancellationToken = null;
        }
    }

    public Task CancelBackupAsync()
    {
        backupCancellationToken?.Cancel();
        return Task.CompletedTask;
    }

    public async Task StartWatchingAsync()
    {
        watcherCancellationToken = new();

        var backups = _workingDirectory.GetDirectoryContents(_configuration.Value.BackupDirectory);
        var mostRecent = backups.Where(f => !f.IsDirectory && Path.GetExtension(f.Name).ToLowerInvariant() == ".zip")
                            .OrderByDescending(f => f.LastModified)
                            .Select(f => f.LastModified)
                            .FirstOrDefault();

        if (mostRecent == default)
        {
            _logger.LogInformation(BackupInitial);
            await CreateBackupAsync();
            mostRecent = DateTimeOffset.Now;
        }

        var timeSinceLastBackup = DateTimeOffset.Now - mostRecent;
        if (timeSinceLastBackup > _configuration.Value.BackupInterval)
        {
            _logger.LogInformation(BackupCreating);
            await CreateBackupAsync();
        }

        _ = Task.Run(async () =>
        {
            while (backupCancellationToken == null || !watcherCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_configuration.Value.BackupInterval, watcherCancellationToken.Token);

                    if (!watcherCancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(BackupCreating);
                        await CreateBackupAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, BackupFailed);
                }
            }
        }).ContinueWith(t => _logger.LogCritical(t.Exception, ExceptionUnhandled, "Backup Watcher"), TaskContinuationOptions.OnlyOnFaulted);
    }

    public Task StopWatchingAsync()
    {
        watcherCancellationToken?.Cancel();
        _backupGate?.Wait();

        return Task.CompletedTask;
    }
}
