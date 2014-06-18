using System;
using System.Collections.Generic;
using System.Text;
using winsw.util;
using System.Diagnostics;

namespace winswTests.util
{
    class TestLogger : IEventWriter
    {
        public void LogEvent(String message)
        {
            System.Console.WriteLine(message);
        }

        public void LogEvent(String message, EventLogEntryType type)
        {
            System.Console.WriteLine("[" + type + "]" + message);
        }
    }
}
