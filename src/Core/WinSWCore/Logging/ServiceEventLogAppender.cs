using System.Diagnostics;
using log4net.Appender;
using log4net.Core;

namespace winsw.Logging
{
    /// <summary>
    /// Implementes service Event log appender for log4j.
    /// The implementation presumes that service gets initialized after the logging.
    /// </summary>
    public class ServiceEventLogAppender : AppenderSkeleton
    {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public IServiceEventLogProvider provider { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        override protected void Append(LoggingEvent loggingEvent)
        {
            EventLog? eventLog = provider.locate();

            // We write the event iff the provider is ready
            eventLog?.WriteEntry(loggingEvent.RenderedMessage, toEventLogEntryType(loggingEvent.Level));
        }

        private static EventLogEntryType toEventLogEntryType(Level level)
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
