using System;

namespace MinecraftBedrockService
{
    internal class ServiceConfig
    {
        public string WorkingDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
        public string Executable { get; set; } = "bedrock_server.exe";
        public string LogFileName { get; set; } = "bedrock_service.log";
    }
}