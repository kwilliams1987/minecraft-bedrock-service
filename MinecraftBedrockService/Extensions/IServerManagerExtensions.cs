namespace MinecraftBedrockService.Interfaces;

internal static class IServerManagerExtensions
{
    public static async Task SendTimedMessageAsync(this IServerManager serverManager, string messageTemplate, TimeSpan duration)
    {
        if (await serverManager.GetPlayerCountAsync() > 0 && duration > TimeSpan.Zero)
        {
            var second = TimeSpan.FromSeconds(1);
            var seconds = (int)duration.TotalSeconds;

            while (seconds-- > 0)
            {
                if (seconds % 10 == 0 || seconds < 10)
                {
                    await serverManager.SendServerMessageAsync(messageTemplate, TimeSpan.FromSeconds(seconds));
                }

                await Task.Delay(second);
            }
        }
    }
}
