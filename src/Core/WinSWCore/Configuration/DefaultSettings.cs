using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using WMI;

namespace winsw.Configuration
{
    /// <summary>
    /// Default WinSW settings
    /// </summary>
    public sealed class DefaultWinSWSettings : IWinSWConfiguration
    {
        public string Id => null;
        public string Caption => null;
        public string Description => null;
        public string Executable => null;
        public bool HideWindow => false;

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
        public bool AllowServiceAcountLogonRight => false;
        public string ServiceAccountPassword => null;
        public string ServiceAccountUser => "NULL\\NULL";
        public List<Native.SC_ACTION> FailureActions => new List<Native.SC_ACTION>();
        public TimeSpan ResetFailureAfter => TimeSpan.FromDays(1);

        // Executable management
        public string Arguments => string.Empty;
        public string Startarguments => null;
        public string StopExecutable => null;
        public string Stoparguments => null;
        public string WorkingDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public ProcessPriorityClass Priority => ProcessPriorityClass.Normal;
        public TimeSpan StopTimeout => TimeSpan.FromSeconds(15);
        public bool StopParentProcessFirst => false;

        // Service management
        public StartMode StartMode => StartMode.Automatic;
        public bool DelayedAutoStart => false;
        public string[] ServiceDependencies => new string[0];
        public TimeSpan WaitHint => TimeSpan.FromSeconds(15);
        public TimeSpan SleepTime => TimeSpan.FromSeconds(1);
        public bool Interactive => false;

        // Logging
        public string LogDirectory => Path.GetDirectoryName(ExecutablePath);
        public string LogMode => "append";

        public bool OutFileDisabled => false;
        public bool ErrFileDisabled => false;
        public string OutFilePattern => ".out.log";
        public string ErrFilePattern => ".err.log";

        // Environment
        public List<Download> Downloads => new List<Download>();
        public Dictionary<string, string> EnvironmentVariables => new Dictionary<string, string>();

        // Misc
        public bool BeepOnShutdown => false;

        // Extensions
        public XmlNode ExtensionsConfiguration => null;
    }
}
