namespace MinecraftBedrockService.Interfaces
{
    internal interface IBackgroundService
    {
        void Start();
        void Stop();
        bool IsRunning { get; }
    }
}