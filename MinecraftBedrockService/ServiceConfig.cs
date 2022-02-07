using System;
using System.IO;

namespace MinecraftBedrockService
{
    internal class ServiceConfig
    {
        public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();
        public string Executable { get; set; } = "bedrock_server.exe";
        public string LogFileName { get; set; } = "bedrock_service.log";
        public TimeSpan BackupInterval { get; set; } = TimeSpan.Zero;
        public string BackupDirectory { get; set; } = "Backups";
    }
}