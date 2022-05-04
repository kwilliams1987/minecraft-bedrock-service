namespace MinecraftBedrockService.Configuration;

internal class ServiceConfig
{
    public bool CreateService { get; set; } = false;
    public string ServiceName { get; set; } = "MinecraftBedrockServer";
    public string Description { get; set; } = "Minecraft Bedrock Service";
    public string RunAs { get; set; } = "NT Authority\\NetworkService";
}
