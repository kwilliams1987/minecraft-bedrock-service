using System;
using System.Threading;
using System.Threading.Tasks;

namespace MinecraftBedrockService.Interfaces
{
    public interface IServerManager
    {
        public delegate void OnMessageReceived(object sender, string message);
        public event OnMessageReceived MessageReceived;

        bool ExitRequested { get; }

        Task<bool> StartServerAsync();
        Task<bool> StopServerAsync(TimeSpan? maxWaitTime = null);
        Task SendServerCommandAsync(string command);
        Task<int> GetPlayerCountAsync();
    }
}
