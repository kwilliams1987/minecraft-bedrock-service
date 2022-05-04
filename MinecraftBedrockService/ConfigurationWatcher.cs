using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace MinecraftBedrockService;

public class ConfigurationWatcher : IConfigurationWatcher
{
    private readonly List<IObserver<ConfigurationFileType>> _fileObservers = new();
    private readonly IFileProvider _workingDirectory;
    private readonly ILogger _logger;

    private IChangeToken whitelistWatcher;
    private IChangeToken permissionsWatcher;
    private IChangeToken propertiesWatcher;

    public ConfigurationWatcher(IFileProvider workingDirectory, ILogger<ConfigurationWatcher> logger)
    {
        _workingDirectory = workingDirectory;
        _logger = logger;

        whitelistWatcher = _workingDirectory.Watch(ServerFiles.Whitelist);
        whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);

        permissionsWatcher = _workingDirectory.Watch(ServerFiles.Permissions);
        permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);

        propertiesWatcher = _workingDirectory.Watch(ServerFiles.ServerProperties);
        propertiesWatcher.RegisterChangeCallback(PropertiesChangedCallback, null);
    }

    public IDisposable Subscribe(IObserver<ConfigurationFileType> observer) => new Observer<ConfigurationFileType>(_fileObservers, observer);

    private void WhitelistChangedCallback(object state)
    {
        _logger.LogInformation(ConfigurationWatcherFileChanged, ServerFiles.Whitelist);
        _fileObservers.OnNext(ConfigurationFileType.Whitelist);

        whitelistWatcher = _workingDirectory.Watch(ServerFiles.Whitelist);
        whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);
    }

    private void PermissionsChangedCallback(object state)
    {
        _logger.LogInformation(ConfigurationWatcherFileChanged, ServerFiles.Permissions);
        _fileObservers.OnNext(ConfigurationFileType.Permissions);

        permissionsWatcher = _workingDirectory.Watch(ServerFiles.Permissions);
        permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);
    }

    private void PropertiesChangedCallback(object state)
    {
        _logger.LogInformation(ConfigurationWatcherFileChanged, ServerFiles.ServerProperties);
        _fileObservers.OnNext(ConfigurationFileType.ServerProperties);

        propertiesWatcher = _workingDirectory.Watch(ServerFiles.ServerProperties);
        propertiesWatcher.RegisterChangeCallback(PropertiesChangedCallback, null);
    }
}
