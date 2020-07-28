using System.Diagnostics;

namespace WinSW.Logging
{
    internal interface IServiceEventLog
    {
        void WriteEntry(string message, EventLogEntryType type);
    }
}
