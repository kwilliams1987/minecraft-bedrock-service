using System.Threading;
using System.Threading.Tasks;

namespace MinecraftBedrockService.Interfaces
{
    public interface IBackupManager
    {
        Task<string> CreateBackupAsync();
        Task CancelBackupAsync();
        Task StartWatchingAsync();
        Task StopWatchingAsync();
    }
}
