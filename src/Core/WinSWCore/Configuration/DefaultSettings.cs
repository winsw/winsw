using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using WMI;

namespace winsw.Configuration
{
    /// <summary>
    /// Default WinSW settings
    /// </summary>
    public sealed class DefaultWinSWSettings : IWinSWConfiguration
    {
        public string Id { get { return null; } }
        public string Caption { get { return null; } }
        public string Description { get { return null; } }
        public string Executable { get { return null; } }

        public string ExecutablePath
        {
            get
            {
                // this returns the executable name as given by the calling process, so
                // it needs to be absolutized.
                string p = Environment.GetCommandLineArgs()[0];
                return Path.GetFullPath(p);
            }
        }

        // Installation
        public bool AllowServiceAcountLogonRight { get { return false; } }
        public string ServiceAccountPassword { get { return null; } }
        public string ServiceAccountUser { get { return "NULL\\NULL"; } }
        public List<winsw.Native.SC_ACTION> FailureActions { get { return new List<winsw.Native.SC_ACTION>(); } }
        public TimeSpan ResetFailureAfter { get { return TimeSpan.FromDays(1); } }

        // Executable management
        public string Arguments { get { return ""; } }
        public string Startarguments { get { return null; } }
        public string StopExecutable { get { return null; } }
        public string Stoparguments { get { return null; } }
        public string WorkingDirectory { get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); } }
        public ProcessPriorityClass Priority { get { return ProcessPriorityClass.Normal; } }
        public TimeSpan StopTimeout { get { return TimeSpan.FromSeconds(15); } }
        public bool StopParentProcessFirst { get { return false; } }

        // Service management
        public StartMode StartMode { get { return StartMode.Automatic; } }
        public bool DelayedStart { get { return false; } }
        public string[] ServiceDependencies { get { return new string[0]; } }
        public TimeSpan WaitHint { get { return TimeSpan.FromSeconds(15); } }
        public TimeSpan SleepTime { get { return TimeSpan.FromSeconds(1); } }
        public bool Interactive { get { return false; } }

        // Logging
        public string LogDirectory { get { return Path.GetDirectoryName(ExecutablePath); } }
        public string LogMode { get { return "append"; } }

        // Environment
        public List<Download> Downloads { get { return new List<Download>(); } }
        public Dictionary<string, string> EnvironmentVariables { get { return new Dictionary<string, string>(); } }

        // Misc
        public bool BeepOnShutdown { get { return false; } }

        // Extensions
        public XmlNode ExtensionsConfiguration { get {return null; } }
    }
}
