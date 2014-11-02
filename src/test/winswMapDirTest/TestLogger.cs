using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using winsw.Utils;

namespace winswMapDirTest
{
    class TestLogger : IEventWriter
    {
        public static readonly TestLogger Instance = new TestLogger();

        public void LogEvent(String message)
        {
            Console.WriteLine(message);
        }

        public void LogEvent(String message, EventLogEntryType type)
        {
            LogEvent(type + ": " + message);
        }
    }
}
