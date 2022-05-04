using System.Text.RegularExpressions;

namespace MinecraftBedrockService;

internal static class ServiceResources
{
    public const string ObservableTypeHandlerError = "Error in {type} change handler.";
    public const string ExceptionUnhandled = "Unhandled exception in {component}.";

    public const string ApplicationTitle = "Minecraft Bedrock Service Manager v{version}";
    public const string CreatingServiceWrapper = "Creating service wrapper...";
    public const string StartingWrapper = "Starting wrapper in {modeType} mode.";
    public const string WrapperTypeService = "service";
    public const string WrapperTypeConsole = "console";
    public const string ShuttingDown = "Shutting down.";

    public const string ServerSendingCommand = "Sending {command} command to server.";
    public const string ServerSendCommandFailedNotRunning = "Attempted to send {command} to server but process is not running.";
    public const string ServerSendCommandFailed = "Error when sending {command} to the server.";
    public const string ServerCannotStartWhenRunning = "Attempted to start server when it is already running.";
    public const string ServerProcessAlreadyExists = "A server process with ID {processId} was already found for this instance. The server manager cannot continue.";
    public const string ServerStartingExecutable = "Starting {path}.";
    public const string ServerHookingProcessOutput = "Hooking console output for Process ID {processId}.";
    public const string ServerExitedUnexpectedly = "Server exited unexpectly, restarting.";
    public const string ServerStartFailed = "Failed to start the server.";
    public const string ServerExecutableMissing = "Could not find the {path} executable in working directory {directory}.";
    public const string ServerTerminatingProcess = "Failed to stop server gracefully, forcing exit.";
    public const string ServerPlayerCountUpdated = "{message} ({playerCount} players online)";
    public const string ServerMessagePassthrough = "{message}";
    public const string ServerReloadingFile = "Reloading {filename}.";
    public const string ServerPropertiesChanged = "Server properties changed, triggering server restart.";
    public const string ServerShutdownTimer = "Server shutdown in {0}.";
    public const string ServerRestartTimer = "Server restart in {0}.";
    public const string ServerRestartMessage = "Restarting server now.";
    public const string ServerStateChanged = "Server state changed to: {state}.";

    public const string KeyboardShortcutCtrlX = "Hold CTRL + X to exit gracefully.";
    public const string KeyboardShortcutCtrlBCtrlN = "Hold CTRL + B to force a backup, CTRL + N to cancel a backup.";
    public const string KeyboardInputInterupted = "Input wait was interupted.";

    public const string ConfigurationWatcherFileChanged = "{fileName} file changed.";

    public const string BackupManagerStarting = "Starting backup manager.";
    public const string BackupAlreadyInProgress = "A backup is already in progress.";
    public const string BackupWaitingForServerStart = "Waiting for server to start.";
    public const string BackupWhenServerNotRunning = "Cannot start backup, the server is not running.";
    public const string BackupStarted = "Starting backup.";
    public const string BackupDuplicateFileFound = "File {file} was already found in file collection with length of {oldLength}. Replacing with {newLength}.";
    public const string BackupCancelled = "Backup was cancelled.";
    public const string BackupUnableToCreateDirectory = "Unable to create temporary directory.";
    public const string BackupCopyingFile = "Creating shadow copy of {filename}.";
    public const string BackupTruncatingFile = "Truncating shadow copy to {length} bytes.";
    public const string BackupComplete = "Backup completed: {path}.";
    public const string BackupInitial = "Creating initial backup.";
    public const string BackupCreating = "Creating new backup.";
    public const string BackupFailed = "The backup failed.";

    public const string ServiceWrapperStarting = "Starting service wrapper.";
    public const string ServiceWrapperStopping = "Stopping services.";

    public const string ServiceManagerExecutable = "sc.exe";
    public const string ServiceManagerArguments = @"create ""{0}"" binPath= ""{1}"" type= own start= delayed-auto DisplayName= ""{2}"" depend= ""Tcpip/Dhcp/Dnscache"" obj= ""{3}""";

    public const string ServiceErrorNoEntryPoint = "Current Assembly has no entry point.";
    public const string ServiceErrorNoDirectory = "Could not find assembly directory.";
    public const string ServiceErrorServiceManagerFailed = $"Unable to start the {ServiceManagerExecutable} process.";
    public const string ServiceCreated = "Created service with name {serviceName}";
    public const string ServiceAlreadyExists = "Service {serviceName} already exists.";
    public const string ServiceUnknownExitCode = "Unknown exit code: {exitCode}";
    public const string ServiceErrorUACAbort = "UAC dialog was cancelled.";
    public const string ServiceUnknownError = "Unexpected error: {message}";
    public const string ServicePlatformNotSupported = "Cannot install service on {platform} platform.";
}

internal static class ServerResources
{
    public const string BackupNotCompleted = "A previous save has not been completed.";
    public const string BackupManifest = "/db/MANIFEST";

    public const string LogFileNoLogFileDetected = "NO LOG FILE! - [] setting up server logging...";
    public const string LogFilePlayerConnected = "Player connected: ";
    public const string LogFilePlayerDisconnected = "Player disconnected: ";
    public const string LogFileServerStarted = "Server started.";
    public const string LogFileVersionNumber = "Version ";

    public static readonly Regex MessageMatch = new("^(NO LOG FILE! - )?\\[.* ?[A-Za-z]+\\] ");
    public static readonly Regex LogLevelMatch = new("^(?:NO LOG FILE! - )?\\[(?:.* )?([A-Za-z]+)\\] ");
}

internal static class ServerCommands
{
    public const string StopServer = "stop";

    public const string CommandResumeUpdates = "save resume";
    public const string CommandHoldUpdates = "save hold";
    public const string CommandQueryBackup = "save query";

    public const string ReloadWhitelist = "whitelist reload";
    public const string ReloadPermissions = "permission reload";

    public const string SendMessage = "say {0}";
}

internal static class ServerFiles
{
    public const string Whitelist = "whitelist.json";
    public const string Permissions = "permissions.json";
    public const string ServerProperties = "server.properties";
    public const string WorldsDirectory = "worlds";
}