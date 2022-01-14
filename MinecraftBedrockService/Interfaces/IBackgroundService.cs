namespace MinecraftBedrockService.Interfaces
{
    internal interface IBackgroundService
    {
        void Start();
        void WaitForExit();
        bool IsRunning { get; }
    }
}