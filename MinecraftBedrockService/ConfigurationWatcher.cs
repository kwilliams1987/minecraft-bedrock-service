using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using MinecraftBedrockService.Interfaces;

namespace MinecraftBedrockService
{
    public class ConfigurationWatcher : IConfigurationWatcher
    {
        private const string WhitelistFile = "whitelist.json";
        private const string PermissionsFile = "permissions.json";
        private const string ServerPropertiesFile = "server.properties";

        public event IConfigurationWatcher.OnWhitelistChanged WhitelistChanged;
        public event IConfigurationWatcher.OnPermissionsChanged PermissionsChanged;
        public event IConfigurationWatcher.OnServerPropertiesChanged ServerPropertiesChanged;

        private readonly IFileProvider _workingDirectory;
        private readonly ILogger _logger;

        private IChangeToken whitelistWatcher;
        private IChangeToken permissionsWatcher;
        private IChangeToken propertiesWatcher;

        public ConfigurationWatcher(IFileProvider workingDirectory, ILogger<ConfigurationWatcher> logger)
        {
            _workingDirectory = workingDirectory;
            _logger = logger;

            whitelistWatcher = _workingDirectory.Watch(WhitelistFile);
            whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);

            permissionsWatcher = _workingDirectory.Watch(PermissionsFile);
            permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);

            propertiesWatcher = _workingDirectory.Watch(ServerPropertiesFile);
            propertiesWatcher.RegisterChangeCallback(PropertiesChangedCallback, null);
        }

        private void WhitelistChangedCallback(object state)
        {
            _logger.LogInformation("{0} file changed.", "Whitelist");
            WhitelistChanged?.Invoke();

            whitelistWatcher = _workingDirectory.Watch(WhitelistFile);
            whitelistWatcher.RegisterChangeCallback(WhitelistChangedCallback, null);
        }

        private void PermissionsChangedCallback(object state)
        {
            _logger.LogInformation("{0} file changed.", "Permissions");
            PermissionsChanged?.Invoke();

            permissionsWatcher = _workingDirectory.Watch(PermissionsFile);
            permissionsWatcher.RegisterChangeCallback(PermissionsChangedCallback, null);
        }

        private void PropertiesChangedCallback(object state)
        {
            _logger.LogInformation("{0} file changed.", "Server Properties");
            ServerPropertiesChanged?.Invoke();

            propertiesWatcher = _workingDirectory.Watch(ServerPropertiesFile);
            propertiesWatcher.RegisterChangeCallback(PropertiesChangedCallback, null);
        }
    }
}
