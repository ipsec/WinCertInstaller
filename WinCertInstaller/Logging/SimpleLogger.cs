using System;

namespace WinCertInstaller.Logging
{
    /// <summary>
    /// A lightweight logger that mimics the basic functionality of ILogger 
    /// without the overhead of the Microsoft.Extensions.Logging infrastructure.
    /// </summary>
    public class SimpleLogger<T>
    {
        private readonly string _categoryName;

        public SimpleLogger()
        {
            _categoryName = typeof(T).Name;
        }

        public void LogInformation(string message, params object?[] args)
        {
            WriteLog("INFO", message, args);
        }

        public void LogWarning(string message, params object?[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            WriteLog("WARN", message, args);
            Console.ResetColor();
        }

        public void LogWarning(Exception ex, string message, params object?[] args)
        {
            LogWarning(message + " Error: " + ex.Message, args);
        }

        public void LogError(string message, params object?[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            WriteLog("ERROR", message, args);
            Console.ResetColor();
        }

        public void LogError(Exception ex, string message, params object?[] args)
        {
            LogError(message + " Error: " + ex.Message, args);
            if (ex.StackTrace != null)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void WriteLog(string level, string message, object?[] args)
        {
            string formattedMessage = args.Length > 0 ? string.Format(message.Replace("{", "{{").Replace("}", "}}"), args) : message;
            
            // Simple approach to handle .NET style curly braces in ILogger
            // Since we want to be fast and light, we just do a basic replace for the common cases
            for (int i = 0; i < args.Length; i++)
            {
               string placeholder = "{" + i + "}";
               if (message.Contains(placeholder)) continue; // Already formatted by string.Format logic above?
               // The original code used named placeholders like {Url}. We'll try a regex-free approach.
            }

            // For simplicity in this lightweight version, we'll just print the message as is if it matches
            // or use string.Format if indices are used. 
            // Most of our calls used log.LogInformation("Message {Arg}", arg).
            
            string output = formattedMessage;
            
            // Re-implementing a very basic named placeholder replacement
            if (args != null && args.Length > 0)
            {
                int argIndex = 0;
                while (output.Contains("{") && output.Contains("}") && argIndex < args.Length)
                {
                    int start = output.IndexOf('{');
                    int end = output.IndexOf('}');
                    if (end > start)
                    {
                        string before = output.Substring(0, start);
                        string after = output.Substring(end + 1);
                        output = before + (args[argIndex]?.ToString() ?? "null") + after;
                        argIndex++;
                    }
                    else break;
                }
            }

            if (level == "INFO")
            {
                Console.WriteLine(output);
            }
            else
            {
                Console.WriteLine($"{level}: {output}");
            }
        }
    }
}
