using Microsoft.Extensions.Logging;
using MinecraftBedrockService;

namespace System.Diagnostics;

internal static class DataReceivedEventArgsExtensions
{
    public static string? GetMessage(this DataReceivedEventArgs args)
        => args.Data == null ? null : ServerResources.MessageMatch.Replace(args.Data, "");

    public static LogLevel GetLogLevel(this DataReceivedEventArgs args)
    {
        if (args.Data == null)
        {
            return LogLevel.None;
        }

        var logLevelMatch = ServerResources.LogLevelMatch.Match(args.Data);
        if (logLevelMatch.Success)
        {
            switch (logLevelMatch.Groups[1].Value.ToUpperInvariant())
            {
                case "TRCE":
                    return LogLevel.Trace;
                case "DBUG":
                    return LogLevel.Debug;
                case "INFO":
                    return LogLevel.Information;
                case "WARN":
                    return LogLevel.Warning;
                case "FAIL":
                    return LogLevel.Error;
                case "CRIT":
                    return LogLevel.Critical;
            }
        }

        return LogLevel.Warning;
    }
}
