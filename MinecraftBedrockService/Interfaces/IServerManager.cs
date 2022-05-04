namespace MinecraftBedrockService.Interfaces;

public interface IServerManager: IObservable<string>
{
    public ServerState CurrentState { get; }

    public Task<bool> StartServerAsync();
    public Task<bool> StopServerAsync(TimeSpan? maxWaitTime = null);
    public Task SendServerCommandAsync(string command);
    public Task<int> GetPlayerCountAsync();
}
