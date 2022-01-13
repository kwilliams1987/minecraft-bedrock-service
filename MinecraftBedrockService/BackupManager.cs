using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftBedrockService.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftBedrockService
{
    internal class BackupManager: IBackupManager
    {
        private static readonly TimeSpan ProgressGateDelay = TimeSpan.FromMilliseconds(1500);

        private readonly IOptions<ServiceConfig> _configuration;
        private readonly ILogger _logger;
        private readonly IServerManager _serverManager;
        private readonly IFileProvider _workingDirectory;
        private readonly ManualResetEventSlim _backupGate = new(true);
        
        private CancellationTokenSource backupCancellationToken;
        private CancellationTokenSource watcherCancellationToken;

        public BackupManager(ILogger<BackupManager> logger, IServerManager serverManager, IFileProvider fileProvider, IOptions<ServiceConfig> configuration)
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

            void OnDataRecieved(object sender, string message)
            {
                if (message?.Contains("/db/MANIFEST") == true)
                {
                    var files = message?.Split(", ").Select(f => f.Split(':'));
                    foreach (var file in files)
                    {
                        var newLength = int.Parse(file[1]);

                        if (backupFiles.ContainsKey(file[0]))
                        {
                            if (backupFiles[file[0]] != newLength)
                            {
                                _logger.LogWarning("File {file} was already found in file collection with length of {oldLength}. Replacing with {newLength}.", file[0], backupFiles[file[0]], newLength);
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
            }

            if (backupCancellationToken != null)
            {
                _logger.LogWarning("A backup is already in progress.");
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

                _logger.LogInformation("Starting backup.");
                var backupPath = $"{Path.Combine(_configuration.Value.WorkingDirectory, _configuration.Value.BackupDirectory, DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss"))}.zip";

                _serverManager.MessageReceived += OnDataRecieved;
                try
                {
                    backupFiles.Clear();
                    await _serverManager.SendServerCommandAsync("save hold");

                    while (!backupProgressGate.IsSet && !backupCancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(ProgressGateDelay, backupCancellationToken.Token);
                        await _serverManager.SendServerCommandAsync("save query");
                    }
                }
                catch (OperationCanceledException) 
                { 
                    _logger.LogWarning("Backup was cancelled.");
                    throw;
                }
                finally
                {
                    _serverManager.MessageReceived -= OnDataRecieved;
                }

                foreach (var file in backupFiles)
                {
                    var source = Path.Combine(_configuration.Value.WorkingDirectory, "worlds", file.Key);
                    var target = Path.Combine(temporaryFolder, file.Key);

                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    _logger.LogInformation("Creating shadow copy of {filename}.", file.Key);
                    File.Copy(source, target);

                    try
                    {
                        _logger.LogInformation("Truncating shadow copy to {length} bytes.", file.Value);

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
                        _logger.LogWarning("Backup was cancelled.");

                        throw;
                    }
                }

                ZipFile.CreateFromDirectory(temporaryFolder, backupPath);
                _logger.LogInformation("Backup completed: {path}.", backupPath);

                return backupPath;
            }
            finally
            {
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
                _logger.LogInformation("Creating initial backup.");
                await CreateBackupAsync();
                mostRecent = DateTimeOffset.Now;
            }

            var timeSinceLastBackup = DateTimeOffset.Now - mostRecent;
            if (timeSinceLastBackup > _configuration.Value.BackupInterval)
            {
                _logger.LogInformation("Creating new backup.");
                await CreateBackupAsync();
            }

            while (backupCancellationToken == null || !watcherCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_configuration.Value.BackupInterval, watcherCancellationToken.Token);

                    _logger.LogInformation("Creating new backup.");
                    await CreateBackupAsync();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "The backup failed.");
                }
            }
        }

        public Task StopWatchingAsync()
        {
            watcherCancellationToken?.Cancel();
            _backupGate?.Wait();

            return Task.CompletedTask;
        }
    }
}
