using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Xml;

namespace WinSW.Configuration
{
    /// <summary>
    /// Default WinSW settings
    /// </summary>
    public abstract class ServiceConfig
    {
        public abstract string FullPath { get; }

        public abstract string Name { get; }

        public virtual string DisplayName => string.Empty;

        public virtual string Description => string.Empty;

        public abstract string Executable { get; }

        public virtual string ExecutablePath => Process.GetCurrentProcess().MainModule!.FileName!;

        public virtual bool HideWindow => false;

        // Installation
        public virtual bool AllowServiceAcountLogonRight => false;

        public virtual string? ServiceAccountPassword => null;

        public virtual string? ServiceAccountUserName => null;

        public virtual Native.SC_ACTION[] FailureActions => Array.Empty<Native.SC_ACTION>();

        public virtual TimeSpan ResetFailureAfter => TimeSpan.FromDays(1);

        // Executable management
        public virtual string Arguments => string.Empty;

        public virtual string? StartArguments => null;

        public virtual string? StopExecutable => null;

        public virtual string? StopArguments => null;

        public virtual string WorkingDirectory => Path.GetDirectoryName(this.FullPath)!;

        public virtual ProcessPriorityClass Priority => ProcessPriorityClass.Normal;

        public virtual TimeSpan StopTimeout => TimeSpan.FromSeconds(15);

        // Service management
        public virtual ServiceStartMode StartMode => ServiceStartMode.Automatic;

        public virtual string[] ServiceDependencies => Array.Empty<string>();

        public virtual bool Interactive => false;

        public virtual bool DelayedAutoStart => false;

        public virtual bool Preshutdown => false;

        // Logging
        public virtual string LogDirectory => Path.GetDirectoryName(this.FullPath)!;

        public virtual string LogMode => "append";

        public virtual bool OutFileDisabled => false;

        public virtual bool ErrFileDisabled => false;

        public virtual string OutFilePattern => ".out.log";

        public virtual string ErrFilePattern => ".err.log";

        // Environment
        public virtual List<Download> Downloads => new(0);

        public virtual Dictionary<string, string> EnvironmentVariables => new(0);

        // Misc
        public virtual bool BeepOnShutdown => false;

        public virtual List<SharedDirectoryMapperConfig> SharedDirectories => new(0);

        // Extensions
        public virtual XmlNode? ExtensionsConfiguration => null;
    }
}
