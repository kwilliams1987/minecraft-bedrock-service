namespace MinecraftBedrockService.Interfaces;

public interface IServerManager: IObservable<string>, IObservable<ServerState>
{
    public ServerState CurrentState { get; }

    public Task<bool> StartServerAsync();
    public Task<bool> StopServerAsync(TimeSpan? maxWaitTime = null);
    public Task SendServerCommandAsync(string command);
    public Task SendServerMessageAsync(string template, params object[] arguments);
    public Task<int> GetPlayerCountAsync();
    public Task<Version> GetServerVersionAsync();
}
