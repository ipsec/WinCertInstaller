using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace WinCertInstaller.Logging
{
    public class CleanConsoleFormatter : ConsoleFormatter
    {
        public CleanConsoleFormatter() : base("CleanLayout") { }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            
            if (string.IsNullOrEmpty(message)) return;

            // Define cores baseadas no nível de log
            var color = logEntry.LogLevel switch
            {
                LogLevel.Error => "\u001b[31m", // Red
                LogLevel.Warning => "\u001b[33m", // Yellow
                LogLevel.Information => "", // Default
                _ => ""
            };

            var reset = "\u001b[0m";

            if (logEntry.LogLevel >= LogLevel.Warning)
            {
                textWriter.WriteLine($"{color}{logEntry.LogLevel.ToString().ToUpper()}: {message}{reset}");
            }
            else
            {
                textWriter.WriteLine(message);
            }

            if (logEntry.Exception != null)
            {
                textWriter.WriteLine(logEntry.Exception.ToString());
            }
        }
    }
}
