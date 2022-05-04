namespace MinecraftBedrockService.Interfaces;

public interface IConfigurationWatcher
{
    public delegate void OnWhitelistChanged();
    public delegate void OnPermissionsChanged();
    public delegate void OnServerPropertiesChanged();

    public event OnWhitelistChanged WhitelistChanged;
    public event OnPermissionsChanged PermissionsChanged;
    public event OnServerPropertiesChanged ServerPropertiesChanged;
}
