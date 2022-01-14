using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftBedrockService.Interfaces;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftBedrockService
{
    internal class ServerManager : IServerManager
    {
        public event IServerManager.OnMessageReceived MessageReceived;

        private readonly IOptions<ServiceConfig> _configuration;
        private readonly IFileProvider _workingDirectory;
        private readonly ILogger _logger;

        private readonly ManualResetEventSlim _serverGate = new();
        private readonly ManualResetEventSlim _startGate = new();

        private Process serverProcess;
        private int playerCount = 0;

        public bool ServerIsRunning => serverProcess != null && serverProcess.HasExited == false;
        public bool ExitRequested { get; private set; } = false;

        public ServerManager(IOptions<ServiceConfig> configuration, IFileProvider workingDirectory, ILogger<ServerManager> logger)
        {
            _configuration = configuration;
            _workingDirectory = workingDirectory;
            _logger = logger;
        }

        public async Task SendServerCommandAsync(string command)
        {
            try
            {
                if (ServerIsRunning)
                {
                    _logger.LogTrace("Sending {command} command to server.", command);
                    await serverProcess.StandardInput.WriteLineAsync(command);
                }
                else
                {
                    _logger.LogError("Attempted to send {command} to server but process is not running.", command);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error when sending {command} to the server.", command);
            }
        }

        public Task<bool> StartServerAsync()
        {
            if (ServerIsRunning)
            {
                _logger.LogWarning("Attempted to start server when it is already running.");
                return Task.FromResult(true);
            }

            var serverExecutable = _workingDirectory.GetFileInfo(_configuration.Value.Executable);
            serverProcess = Process.GetProcesses().FirstOrDefault(p =>
            {
                try
                {
                    return p.MainModule.FileName == serverExecutable.PhysicalPath;
                }
                catch (Win32Exception)
                {
                    return false;
                }
            });

            if (serverProcess != null)
            {
                _logger.LogError("A server process with ID {processId} was already found for this instance. The server manager cannot continue.", serverProcess.Id);

                serverProcess = null;
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
                            CreateNoWindow = true,
                            FileName = serverExecutable.PhysicalPath,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }
                    };

                    serverProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
                    serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;
                    _logger.LogInformation("Starting {path}.", serverExecutable.PhysicalPath);
                    serverProcess.Start();

                    _startGate.Reset();
                    _logger.LogInformation("Hooking console output for Process ID {processId}.", serverProcess.Id);
                    serverProcess.BeginOutputReadLine();
                    serverProcess.BeginErrorReadLine();

                    StartServerHeartbeat();

                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start the server.");
                    serverProcess?.Kill();

                    return Task.FromResult(false);
                }
            }
            else
            {
                _logger.LogError("Could not find the {path} executable in working directory {directory}.", _configuration.Value.Executable, _configuration.Value.WorkingDirectory);
                return Task.FromResult(false);
            }
        }

        public async Task<bool> StopServerAsync(TimeSpan? maxWaitTime = null)
        {
            ExitRequested = true;

            try
            {
                if (ServerIsRunning)
                {
                    await SendServerCommandAsync("stop");

                    if (maxWaitTime == null)
                    {
                        await serverProcess.WaitForExitAsync();
                    }
                    else
                    {
                        var source = new CancellationTokenSource(maxWaitTime.Value);
                        await serverProcess.WaitForExitAsync(source.Token);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop server gracefully, forcing exit.");
                serverProcess?.Kill();

                return false;
            }
            finally
            {
                Interlocked.Exchange(ref playerCount, 0);
                serverProcess?.Dispose();
                serverProcess = null;
                _serverGate.Set();
            }
        }

        public Task<int> GetPlayerCountAsync() => Task.FromResult(playerCount);

        public void WaitForStart(CancellationToken? cancellationToken = null)
        {
            try
            {
                _startGate.Wait(cancellationToken ?? CancellationToken.None);
            }
            catch (OperationCanceledException) { }
        }
        
        private void StartServerHeartbeat() => new Thread(async _ =>
        {
            await serverProcess?.WaitForExitAsync();

            if (!ExitRequested)
            {
                _logger.LogError("Server exited unexpectly, restarting.");

                serverProcess?.Dispose();
                serverProcess = null;

                await StartServerAsync();
            }

            ExitRequested = false;
        }).Start();

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            var message = e.GetMessage();

            if (!string.IsNullOrWhiteSpace(message))
            {
                if (message.Contains("Player connected: "))
                {
                    Interlocked.Increment(ref playerCount);
                    _logger.LogInformation("{message} ({playerCount} players online)", message, playerCount);
                }

                else if (message.Contains("Player disconnected: "))
                {
                    Interlocked.Decrement(ref playerCount);
                    _logger.LogInformation("{message} ({playerCount} players online)", message, playerCount);
                }

                else if (message.EndsWith("Server started."))
                {
                    _startGate.Set();
                    _logger.LogTrace("{message}", message);
                }

                else
                {
                    _logger.Log(e.GetLogLevel(), "{message}", message);
                }
            }

            MessageReceived?.Invoke(this, message);
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            var message = e.GetMessage();

            if (!string.IsNullOrWhiteSpace(message))
            {
                _logger.LogCritical("{message}", message);
            }
        }
    }
}
