﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using WMI;

namespace WinSW.Configuration
{
    public interface IWinSWConfiguration
    {
        // TODO: Document the parameters && refactor
        string Id { get; }

        string Caption { get; }

        string Description { get; }

        string Executable { get; }

        string ExecutablePath { get; }

        bool HideWindow { get; }

        // Installation
        Native.SC_ACTION[] FailureActions { get; }

        TimeSpan ResetFailureAfter { get; }

        // Executable management
        string Arguments { get; }

        string? StartArguments { get; }

        string? StopExecutable { get; }

        string? StopArguments { get; }

        string WorkingDirectory { get; }

        ProcessPriorityClass Priority { get; }

        TimeSpan StopTimeout { get; }

        bool StopParentProcessFirst { get; }

        // Service management
        StartMode StartMode { get; }

        string[] ServiceDependencies { get; }

        TimeSpan WaitHint { get; }

        TimeSpan SleepTime { get; }

        bool Interactive { get; }

        /// <summary>
        /// Destination for logging.
        /// If undefined, a default one should be used.
        /// </summary>
        string LogDirectory { get; }

        // TODO: replace by enum
        string LogMode { get; }

        Log Log { get; }

        // Environment
        List<Download> Downloads { get; }

        Dictionary<string, string> EnvironmentVariables { get; }

        // Misc
        bool BeepOnShutdown { get; }

        // Extensions
        XmlNode? ExtensionsConfiguration { get; }

        List<YamlExtensionConfiguration>? YamlExtensionsConfiguration { get; }

        List<string> ExtensionIds { get; }

        // Service Account
        ServiceAccount ServiceAccount { get; }

        string BaseName { get; }

        string BasePath { get; }

        bool DelayedAutoStart { get; }

        string? SecurityDescriptor { get; }
    }
}
