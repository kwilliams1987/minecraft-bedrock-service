using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace System.Diagnostics
{
    internal static class DataReceivedEventArgsExtensions
    {
        private static readonly Regex MessageMatch = new("^(NO LOG FILE! - )?\\[.* ?[A-Za-z]+\\] ");
        private static readonly Regex LogLevelMatch = new("^(?:NO LOG FILE! - )?\\[(?:.* )?([A-Za-z]+)\\] ");

        public static string GetMessage(this DataReceivedEventArgs args)
            => args.Data == null ? null : MessageMatch.Replace(args.Data, "");

        public static LogLevel GetLogLevel(this DataReceivedEventArgs args)
        {
            if (args.Data == null)
            {
                return LogLevel.None;
            }

            var logLevelMatch = LogLevelMatch.Match(args.Data);
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
}
