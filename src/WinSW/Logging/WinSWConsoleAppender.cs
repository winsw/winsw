using System;
using log4net.Appender;
using log4net.Core;

namespace WinSW.Logging
{
    internal sealed class WinSWConsoleAppender : AppenderSkeleton
    {
        protected override void Append(LoggingEvent loggingEvent)
        {
            Console.ResetColor();

            Level level = loggingEvent.Level;
            Console.ForegroundColor =
                level >= Level.Error ? ConsoleColor.Red :
                level >= Level.Warn ? ConsoleColor.Yellow :
                level >= Level.Info ? ConsoleColor.Gray :
                ConsoleColor.DarkGray;
            try
            {
                this.Layout.Format(Console.Out, loggingEvent);
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}
