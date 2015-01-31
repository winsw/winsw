using System;
using System.Diagnostics;
using winsw.Util;

namespace winswTests.util
{
    class TestLogger : IEventWriter
    {
        public void LogEvent(String message)
        {
            Console.WriteLine(message);
        }

        public void LogEvent(String message, EventLogEntryType type)
        {
            Console.WriteLine("[" + type + "]" + message);
        }
    }
}
