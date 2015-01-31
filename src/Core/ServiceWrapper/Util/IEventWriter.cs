using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace winsw.Util
{
    public interface IEventWriter
    {
        void LogEvent(String message);
        void LogEvent(String message, EventLogEntryType type);
    }
}
