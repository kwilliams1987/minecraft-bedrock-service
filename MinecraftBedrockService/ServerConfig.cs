namespace MinecraftBedrockService
{
    internal class ServerConfig
    {
        private const string DefaultExecutable = "bedrock_server.exe";

        public string Executable { get; set; } = DefaultExecutable;
    }
}