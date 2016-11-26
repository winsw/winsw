using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using winsw.Extensions;
using winsw.Util;
using log4net;

namespace winsw.Plugins.RunawayProcessKiller
{
    public class RunawayProcessKillerExtension : AbstractWinSWExtension
    {
        /// <summary>
        /// Absolute path to the PID file, which stores ID of the previously launched process.
        /// </summary>
        public String Pidfile { get; private set; }

        /// <summary>
        /// Defines the process termination timeout in milliseconds.
        /// This timeout will be applied multiple times for each child process.
        /// </summary>
        public TimeSpan StopTimeout { get; private set; }

        /// <summary>
        /// If true, the parent process will be terminated first if the runaway process gets terminated.
        /// </summary>
        public bool StopParentProcessFirst { get; private set; }

        public override String DisplayName { get { return "Runaway Process Killer"; } }

        private static readonly ILog Logger = LogManager.GetLogger(typeof(RunawayProcessKillerExtension));

        public RunawayProcessKillerExtension()
        {
            // Default initializer
        }

        public RunawayProcessKillerExtension(String pidfile)
        {
            this.Pidfile = pidfile;
        }

        public override void Configure(ServiceDescriptor descriptor, XmlNode node, IEventWriter logger)
        {
            // We expect the upper logic to process any errors
            // TODO: a better parser API for types would be useful
            Pidfile = XmlHelper.SingleElement(node, "pidfile", false);
            StopTimeout = TimeSpan.FromMilliseconds(Int32.Parse(XmlHelper.SingleElement(node, "stopTimeout", false)));
            StopParentProcessFirst = Boolean.Parse(XmlHelper.SingleElement(node, "stopParentFirst", false));
        }

        /// <summary>
        /// This method checks if the PID file is stored on the disk and then terminates runaway processes if they exist.
        /// </summary>
        /// <param name="logger">Unused logger</param>
        public override void OnStart(IEventWriter logger)
        {
            // Read PID file from the disk
            int pid;
            if (System.IO.File.Exists(Pidfile)) {
                string pidstring;
                try
                {
                    pidstring = System.IO.File.ReadAllText(Pidfile);
                }
                catch (Exception ex)
                {
                    Logger.Error("Cannot read PID file from " + Pidfile, ex);
                    return;
                }
                try
                {
                    pid = Int32.Parse(pidstring);
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
            Process proc;
            try
            {
                proc = Process.GetProcessById(pid);
            }
            catch (ArgumentException ex)
            {
                Logger.Debug("No runaway process with PID=" + pid + ". The process has been already stopped.");
                return;
            }

            // Kill the runaway process
            ProcessHelper.StopProcessAndChildren(pid, this.StopTimeout, this.StopParentProcessFirst);
        }

        /// <summary>
        /// Records the started process PID for the future use in OnStart() after the restart.
        /// </summary>
        /// <param name="process"></param>
        public override void OnProcessStarted(System.Diagnostics.Process process)
        {
            Logger.Info("Recording PID of the started process:" + process.Id + ". PID file destination is " + Pidfile);
            try
            {
                System.IO.File.WriteAllText(Pidfile, process.Id.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error("Cannot update the PID file " + Pidfile, ex);
            }
        }
    }
}
