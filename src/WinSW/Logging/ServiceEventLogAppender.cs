using System.Diagnostics;
using log4net.Appender;
using log4net.Core;

namespace WinSW.Logging
{
    /// <summary>
    /// Implementes service Event log appender for log4j.
    /// The implementation presumes that service gets initialized after the logging.
    /// </summary>
    internal sealed class ServiceEventLogAppender : AppenderSkeleton
    {
        private readonly WrapperServiceEventLogProvider provider;

        internal ServiceEventLogAppender(WrapperServiceEventLogProvider provider)
        {
            this.provider = provider;
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var eventLog = this.provider.Locate();

            if (eventLog is not null)
            {
                eventLog.WriteEntry(loggingEvent.RenderedMessage, ToEventLogEntryType(loggingEvent.Level));
                return;
            }

            try
            {
                using var backupLog = new EventLog("Application", ".", "Windows Service Wrapper");
                backupLog.WriteEntry(loggingEvent.RenderedMessage, ToEventLogEntryType(loggingEvent.Level));
            }
            catch
            {
            }
        }

        private static EventLogEntryType ToEventLogEntryType(Level level)
        {
            if (level.Value >= Level.Error.Value)
            {
                return EventLogEntryType.Error;
            }

            if (level.Value >= Level.Warn.Value)
            {
                return EventLogEntryType.Warning;
            }

            // All other events will be posted as information
            return EventLogEntryType.Information;
        }
    }
}
