namespace MinecraftBedrockService.Interfaces;

public interface IConfigurationWatcher : IObservable<ConfigurationFileType>
{
}

public enum ConfigurationFileType
{
    Whitelist,
    Permissions,
    ServerProperties
}