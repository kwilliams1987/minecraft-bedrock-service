using System.IO;

namespace MinecraftBedrockService.Configuration;

internal class ServerConfig
{
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();
    public string Executable { get; set; } = "bedrock_server.exe";
    public string LogFileName { get; set; } = "bedrock_service.log";
    public TimeSpan BackupInterval { get; set; } = TimeSpan.FromMinutes(30);
    public string BackupDirectory { get; set; } = "Backups";
}
