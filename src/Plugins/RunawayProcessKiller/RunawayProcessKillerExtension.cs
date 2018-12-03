using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using log4net;
using winsw.Extensions;
using winsw.Util;

namespace winsw.Plugins.RunawayProcessKiller
{
    public class RunawayProcessKillerExtension : AbstractWinSWExtension
    {
        /// <summary>
        /// Absolute path to the PID file, which stores ID of the previously launched process.
        /// </summary>
        public string Pidfile { get; private set; }

        /// <summary>
        /// Defines the process termination timeout in milliseconds.
        /// This timeout will be applied multiple times for each child process.
        /// </summary>
        public TimeSpan StopTimeout { get; private set; }

        /// <summary>
        /// If true, the parent process will be terminated first if the runaway process gets terminated.
        /// </summary>
        public bool StopParentProcessFirst { get; private set; }

        /// <summary>
        /// If true, the runaway process will be checked for the WinSW environment variable before termination.
        /// This option is not documented AND not supposed to be used by users.
        /// </summary>
        public bool CheckWinSWEnvironmentVariable { get; private set; }

        public override string DisplayName => "Runaway Process Killer";

        private string ServiceId { get; set; }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(RunawayProcessKillerExtension));

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public RunawayProcessKillerExtension()
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            // Default initializer
        }

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public RunawayProcessKillerExtension(string pidfile, int stopTimeoutMs = 5000, bool stopParentFirst = false, bool checkWinSWEnvironmentVariable = true)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.Pidfile = pidfile;
            this.StopTimeout = TimeSpan.FromMilliseconds(stopTimeoutMs);
            this.StopParentProcessFirst = stopParentFirst;
            this.CheckWinSWEnvironmentVariable = checkWinSWEnvironmentVariable;
        }

        public override void Configure(ServiceDescriptor descriptor, XmlNode node)
        {
            // We expect the upper logic to process any errors
            // TODO: a better parser API for types would be useful
            Pidfile = XmlHelper.SingleElement(node, "pidfile", false)!;
            StopTimeout = TimeSpan.FromMilliseconds(int.Parse(XmlHelper.SingleElement(node, "stopTimeout", false)!));
            StopParentProcessFirst = bool.Parse(XmlHelper.SingleElement(node, "stopParentFirst", false)!);
            ServiceId = descriptor.Id;
            // TODO: Consider making it documented
            var checkWinSWEnvironmentVariable = XmlHelper.SingleElement(node, "checkWinSWEnvironmentVariable", true);
            CheckWinSWEnvironmentVariable = checkWinSWEnvironmentVariable != null ? bool.Parse(checkWinSWEnvironmentVariable) : true;
        }

        /// <summary>
        /// This method checks if the PID file is stored on the disk and then terminates runaway processes if they exist.
        /// </summary>
        /// <param name="logger">Unused logger</param>
        public override void OnWrapperStarted()
        {
            // Read PID file from the disk
            int pid;
            if (File.Exists(Pidfile))
            {
                string pidstring;
                try
                {
                    pidstring = File.ReadAllText(Pidfile);
                }
                catch (Exception ex)
                {
                    Logger.Error("Cannot read PID file from " + Pidfile, ex);
                    return;
                }

                try
                {
                    pid = int.Parse(pidstring);
                }
                catch (FormatException e)
                {
                    Logger.Error("Invalid PID file number in '" + Pidfile + "'. The runaway process won't be checked", e);
                    return;
                }
            }
            else
            {
                Logger.Warn("The requested PID file '" + Pidfile + "' does not exist. The runaway process won't be checked");
                return;
            }

            // Now check the process
            Logger.DebugFormat("Checking the potentially runaway process with PID={0}", pid);
            Process proc;
            try
            {
                proc = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                Logger.Debug("No runaway process with PID=" + pid + ". The process has been already stopped.");
                return;
            }

            // Ensure the process references the service
            string? affiliatedServiceId;
            // TODO: This method is not ideal since it works only for vars explicitly mentioned in the start info
            // No Windows 10- compatible solution for EnvVars retrieval, see https://blog.gapotchenko.com/eazfuscator.net/reading-environment-variables
            StringDictionary previousProcessEnvVars = proc.StartInfo.EnvironmentVariables;
            string expectedEnvVarName = WinSWSystem.ENVVAR_NAME_SERVICE_ID;
            if (previousProcessEnvVars.ContainsKey(expectedEnvVarName))
            {
                // StringDictionary is case-insensitive, hence it will fetch variable definitions in any case
                affiliatedServiceId = previousProcessEnvVars[expectedEnvVarName];
            }
            else if (CheckWinSWEnvironmentVariable)
            {
                Logger.Warn("The process " + pid + " has no " + expectedEnvVarName + " environment variable defined. "
                    + "The process has not been started by WinSW, hence it won't be terminated.");
                if (Logger.IsDebugEnabled)
                {
                    // TODO replace by String.Join() in .NET 4
                    string[] keys = new string[previousProcessEnvVars.Count];
                    previousProcessEnvVars.Keys.CopyTo(keys, 0);
                    Logger.DebugFormat("Env vars of the process with PID={0}: {1}", new object[] { pid, string.Join(",", keys) });
                }

                return;
            }
            else
            {
                // We just skip this check
                affiliatedServiceId = null;
            }

            // Check the service ID value
            if (CheckWinSWEnvironmentVariable && !ServiceId.Equals(affiliatedServiceId))
            {
                Logger.Warn("The process " + pid + " has been started by Windows service with ID='" + affiliatedServiceId + "'. "
                    + "It is another service (current service id is '" + ServiceId + "'), hence the process won't be terminated.");
                return;
            }

            // Kill the runaway process
            StringBuilder bldr = new StringBuilder("Stopping the runaway process (pid=");
            bldr.Append(pid);
            bldr.Append(") and its children. Environment was ");
            if (!CheckWinSWEnvironmentVariable)
            {
                bldr.Append("not ");
            }

            bldr.Append("checked, affiliated service ID: ");
            bldr.Append(affiliatedServiceId ?? "undefined");
            bldr.Append(", process to kill: ");
            bldr.Append(proc);

            Logger.Warn(bldr.ToString());
            ProcessHelper.StopProcessAndChildren(pid, this.StopTimeout, this.StopParentProcessFirst);
        }

        /// <summary>
        /// Records the started process PID for the future use in OnStart() after the restart.
        /// </summary>
        /// <param name="process"></param>
        public override void OnProcessStarted(Process process)
        {
            Logger.Info("Recording PID of the started process:" + process.Id + ". PID file destination is " + Pidfile);
            try
            {
                File.WriteAllText(Pidfile, process.Id.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error("Cannot update the PID file " + Pidfile, ex);
            }
        }
    }
}
