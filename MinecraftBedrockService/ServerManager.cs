using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace MinecraftBedrockService;

internal class ServerManager : IServerManager, IObserver<ConfigurationFileType>, IObserver<ServerState>, IDisposable
{
    private readonly List<IObserver<string>> _logObservers = new();
    private readonly List<IObserver<ServerState>> _stateObservers = new();
    private readonly IOptions<ServerConfig> _configuration;
    private readonly IFileProvider _workingDirectory;
    private readonly IDisposable? _configurationWatcher;
    private readonly IDisposable? _stateWatcher;
    private readonly ILogger _logger;

    private Process? serverProcess;
    private Version? version;
    private int playerCount = 0;
    private ServerState serverState = ServerState.Created;

    public ServerState CurrentState
    {
        get => serverState;
        private set
        {
            if (value != serverState)
            {
                serverState = value;
                _stateObservers.OnNext(CurrentState);
            }
        }
    }

    public ServerManager(IOptions<ServerConfig> configuration, IFileProvider workingDirectory, IConfigurationWatcher configurationWatcher, ILogger<ServerManager> logger)
    {
        _configuration = configuration;
        _workingDirectory = workingDirectory;
        _logger = logger;

        _configurationWatcher = configurationWatcher.Subscribe(this);
        _stateWatcher = Subscribe(this);
    }

    public void Dispose()
    {
        _configurationWatcher?.Dispose();
        _stateWatcher?.Dispose();

        GC.SuppressFinalize(this);
    }

    public async Task SendServerCommandAsync(string command)
    {
        try
        {
            if (CurrentState == ServerState.Running)
            {
                _logger.LogTrace(ServerSendingCommand, command);
                await serverProcess!.StandardInput.WriteLineAsync(command);
            }
            else
            {
                _logger.LogError(ServerSendCommandFailedNotRunning, command);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, ServerSendCommandFailed, command);
        }
    }

    public Task<bool> StartServerAsync()
    {
        if (CurrentState == ServerState.Starting || CurrentState == ServerState.Running)
        {
            _logger.LogWarning(ServerCannotStartWhenRunning);
            return Task.FromResult(true);
        }

        CurrentState = ServerState.Starting;

        var serverExecutable = _workingDirectory.GetFileInfo(_configuration.Value.Executable);
        serverProcess = Process.GetProcesses().FirstOrDefault(p =>
        {
            try
            {
                return p.MainModule?.FileName == serverExecutable.PhysicalPath;
            }
            catch (Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });

        if (serverProcess != null)
        {
            _logger.LogError(ServerProcessAlreadyExists, serverProcess.Id);

            serverProcess = null;

            CurrentState = ServerState.Faulted;
            return Task.FromResult(false);
        }

        if (serverExecutable.Exists)
        {
            try
            {
                serverProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
#if DEBUG
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Normal,
#else
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
#endif
                        FileName = serverExecutable.PhysicalPath,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };

                serverProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
                serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;
                _logger.LogInformation(ServerStartingExecutable, serverExecutable.PhysicalPath);
                serverProcess.Start();

                _logger.LogInformation(ServerHookingProcessOutput, serverProcess.Id);
                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();

                _ = Task.Run(async () =>
                {
                    await (serverProcess?.WaitForExitAsync().ConfigureAwait(false) ?? Task.CompletedTask.ConfigureAwait(false));

                    if (CurrentState != ServerState.Stopping && CurrentState != ServerState.Stopped)
                    {
                        _logger.LogError(ServerExitedUnexpectedly);
                        CurrentState = ServerState.Faulted;

                        serverProcess?.Dispose();
                        serverProcess = null;

                        await StartServerAsync().ConfigureAwait(false);
                    }
                }).ContinueWith(t => _logger.LogCritical(t.Exception, ExceptionUnhandled, "Server Heartbeat"), TaskContinuationOptions.OnlyOnFaulted);
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ServerStartFailed);
                serverProcess?.Kill();

                CurrentState = ServerState.Faulted;
                return Task.FromResult(false);
            }
        }
        else
        {
            _logger.LogError(ServerExecutableMissing, _configuration.Value.Executable, _configuration.Value.WorkingDirectory);

            CurrentState = ServerState.Faulted;
            return Task.FromResult(false);
        }
    }

    public async Task<bool> StopServerAsync(TimeSpan? maxWaitTime = null)
    {
        var success = false;
        try
        {
            if (CurrentState == ServerState.Running && serverProcess != null)
            {
                CurrentState = ServerState.Stopping;
                await SendServerCommandAsync(ServerCommands.StopServer).ConfigureAwait(false);

                if (maxWaitTime == null)
                {
                    await serverProcess.WaitForExitAsync().ConfigureAwait(false);
                }
                else
                {
                    var source = new CancellationTokenSource(maxWaitTime.Value);
                    await serverProcess.WaitForExitAsync(source.Token).ConfigureAwait(false);
                }
            }

            success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ServerTerminatingProcess);
            serverProcess?.Kill();
            success = false;
        }
        finally
        {
            if (success)
            {
                Interlocked.Exchange(ref playerCount, 0);
                serverProcess?.Dispose();
                serverProcess = null;
            }

            CurrentState = success ? ServerState.Stopped : ServerState.Faulted;
        }

        return success;
    }

    public Task<int> GetPlayerCountAsync() => Task.FromResult(playerCount);

    public Task<Version> GetServerVersionAsync() => Task.FromResult(version ?? new Version(0, 0, 0, 0));

    public IDisposable Subscribe(IObserver<string> observer) => new Observer<string>(_logObservers, observer);
    public IDisposable Subscribe(IObserver<ServerState> observer) => new Observer<ServerState>(_stateObservers, observer);

    public Task SendServerMessageAsync(string message, params object[] args) => SendServerCommandAsync(string.Format(ServerCommands.SendMessage, string.Format(message, args)));
    
    private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        var message = e.GetMessage();

        if (!string.IsNullOrWhiteSpace(message))
        {
            if (message == ServerResources.LogFileNoLogFileDetected)
            {
                // Ignored because logging is handled by this process.
                return;
            }

            else if (message.Contains(ServerResources.LogFilePlayerConnected))
            {
                Interlocked.Increment(ref playerCount);
                _logger.LogInformation(ServerPlayerCountUpdated, message, playerCount);
            }

            else if (message.Contains(ServerResources.LogFilePlayerDisconnected))
            {
                Interlocked.Decrement(ref playerCount);
                _logger.LogInformation(ServerPlayerCountUpdated, message, playerCount);
            }

            else if (message.EndsWith(ServerResources.LogFileServerStarted))
            {
                CurrentState = ServerState.Running;
                _logger.LogTrace(ServerMessagePassthrough, message);
            }

            else if (message.StartsWith(ServerResources.LogFileVersionNumber))
            {
                version = new Version(message[8..]);
                _logger.LogInformation(ServerMessagePassthrough, message);
            }

            else
            {
                _logger.Log(e.GetLogLevel(), ServerMessagePassthrough, message);
            }

            _logObservers.OnNext(message);
        }
    }

    private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        var message = e.GetMessage();

        if (!string.IsNullOrWhiteSpace(message))
        {
            _logger.LogCritical(ServerMessagePassthrough, message);
        }
    }

    async void IObserver<ConfigurationFileType>.OnNext(ConfigurationFileType value)
    {
        switch (value)
        {
            case ConfigurationFileType.Whitelist:
                _logger.LogInformation(ServerReloadingFile, value);
                await SendServerCommandAsync(ServerCommands.ReloadWhitelist);
                break;

            case ConfigurationFileType.Permissions:
                _logger.LogInformation(ServerReloadingFile, value);
                await SendServerCommandAsync(ServerCommands.ReloadPermissions);
                break;

            case ConfigurationFileType.ServerProperties:
                _logger.LogInformation(ServerPropertiesChanged);

                await this.SendTimedMessageAsync(ServerRestartTimer, TimeSpan.FromSeconds(30));
                await SendServerMessageAsync(ServerRestartMessage);

                await StopServerAsync();
                await StartServerAsync();
                break;
        }
    }

    void IObserver<ConfigurationFileType>.OnCompleted() { }
    void IObserver<ConfigurationFileType>.OnError(Exception error) 
    {
        _logger.LogError(error, ObservableTypeHandlerError, nameof(ConfigurationFileType));
    }

    void IObserver<ServerState>.OnNext(ServerState value)
    {
        _logger.LogInformation(ServerStateChanged, value);
    }
    void IObserver<ServerState>.OnCompleted() { }
    void IObserver<ServerState>.OnError(Exception error)
    {
        _logger.LogError(error, ObservableTypeHandlerError, nameof(ServerState));
    }
}
