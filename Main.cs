using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Net;
using WMI;
using System.Xml;
using System.Threading;
using Microsoft.Win32;
using System.Management;

namespace winsw
{
    public class WrapperService : ServiceBase, EventLogger
    {
        private SERVICE_STATUS wrapperServiceStatus;

        private Process process = new Process();
        private ServiceDescriptor descriptor;
        private Dictionary<string, string> envs;

        /// <summary>
        /// Indicates to the watch dog thread that we are going to terminate the process,
        /// so don't try to kill us when the child exits.
        /// </summary>
        private bool orderlyShutdown;
        private bool systemShuttingdown;

        public WrapperService()
        {
            this.descriptor = new ServiceDescriptor();
            this.ServiceName = descriptor.Id;
            this.CanShutdown = true;
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
            this.systemShuttingdown = false;
        }

        /// <summary>
        /// Process the file copy instructions, so that we can replace files that are always in use while
        /// the service runs.
        /// </summary>
        private void HandleFileCopies()
        {
            var file = descriptor.BasePath + ".copies";
            if (!File.Exists(file))
                return; // nothing to handle

            try
            {
                using (var tr = new StreamReader(file,Encoding.UTF8))
                {
                    string line;
                    while ((line = tr.ReadLine()) != null)
                    {
                        LogEvent("Handling copy: " + line);
                        string[] tokens = line.Split('>');
                        if (tokens.Length > 2)
                        {
                            LogEvent("Too many delimiters in " + line);
                            continue;
                        }

                        CopyFile(tokens[0], tokens[1]);
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }

        }

        /// <summary>
        /// File replacement.
        /// </summary>
        private void CopyFile(string sourceFileName, string destFileName)
        {
            try
            {
                File.Delete(destFileName);
                File.Move(sourceFileName, destFileName);
            }
            catch (IOException e)
            {
                LogEvent("Failed to copy :" + sourceFileName + " to " + destFileName + " because " + e.Message);
            }
        }

        /// <summary>
        /// Starts a thread that protects the execution with a try/catch block.
        /// It appears that in .NET, unhandled exception in any thread causes the app to terminate
        /// http://msdn.microsoft.com/en-us/library/ms228965.aspx
        /// </summary>
        private void StartThread(ThreadStart main)
        {
            new Thread(delegate() {
                try
                {
                    main();
                }
                catch (Exception e)
                {
                    WriteEvent("Thread failed unexpectedly",e);
                }
            }).Start();
        }

        /// <summary>
        /// Handle the creation of the logfiles based on the optional logmode setting.
        /// </summary>
        private void HandleLogfiles()
        {
            string logDirectory = descriptor.LogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            LogHandler logAppender = descriptor.LogHandler;
            logAppender.EventLogger = this;
            logAppender.log(process.StandardOutput.BaseStream, process.StandardError.BaseStream);
        }

        public void LogEvent(String message)
        {
            if (systemShuttingdown)
            {
                /* NOP - cannot call EventLog because of shutdown. */
            }
            else
            {
                EventLog.WriteEntry(message);
            }
        }

        public void LogEvent(String message, EventLogEntryType type)
        {
            if (systemShuttingdown)
            {
                /* NOP - cannot call EventLog because of shutdown. */
            }
            else
            {
                EventLog.WriteEntry(message, type);
            }
        }

        private void WriteEvent(Exception exception)
        {
            WriteEvent(exception.Message + "\nStacktrace:" + exception.StackTrace);
        }

        private void WriteEvent(String message, Exception exception)
        {
            WriteEvent(message + "\nMessage:" + exception.Message + "\nStacktrace:" + exception.StackTrace);
        }

        private void WriteEvent(String message)
        {
            string logfilename = Path.Combine(descriptor.LogDirectory, descriptor.BaseName + ".wrapper.log");
            StreamWriter log = new StreamWriter(logfilename, true);

            log.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message);
            log.Flush();
            log.Close();
        }

        protected override void OnStart(string[] _)
        {
            envs = descriptor.EnvironmentVariables;
            foreach (string key in envs.Keys)
            {
                LogEvent("envar " + key + '=' + envs[key]);
            }

            HandleFileCopies();

            // handle downloads
            foreach (Download d in descriptor.Downloads)
            {
                LogEvent("Downloading: " + d.From+ " to "+d.To);
                try
                {
                    d.Perform();
                }
                catch (Exception e)
                {
                    LogEvent("Failed to download " + d.From + " to " + d.To + "\n" + e.Message);
                    WriteEvent("Failed to download " + d.From +" to "+d.To, e);
                    // but just keep going
                }
            }

            string startarguments = descriptor.Startarguments;

            if (startarguments == null)
            {
                startarguments = descriptor.Arguments;
            }
            else
            {
                startarguments += " " + descriptor.Arguments;
            }

            LogEvent("Starting " + descriptor.Executable + ' ' + startarguments);
            WriteEvent("Starting " + descriptor.Executable + ' ' + startarguments);

            StartProcess(process, startarguments, descriptor.Executable);

            // send stdout and stderr to its respective output file.
            HandleLogfiles();

            process.StandardInput.Close(); // nothing for you to read!
        }

        protected override void OnShutdown()
        {
//            WriteEvent("OnShutdown");

            try
            {
                this.systemShuttingdown = true;
                StopIt();
            }
            catch (Exception ex)
            {
                WriteEvent("Shutdown exception", ex);
            }
        }

        protected override void OnStop()
        {
//            WriteEvent("OnStop");

            try
            {
                StopIt();
            }
            catch (Exception ex)
            {
                WriteEvent("Stop exception", ex);
            }
        }

        /// <summary>
        /// Called when we are told by Windows SCM to exit.
        /// </summary>
        private void StopIt()
        {
            string stoparguments = descriptor.Stoparguments;
            LogEvent("Stopping " + descriptor.Id);
            WriteEvent("Stopping " + descriptor.Id);
            orderlyShutdown = true;

            if (stoparguments == null)
            {
                try
                {
                    WriteEvent("ProcessKill " + process.Id);
                    StopProcessAndChildren(process.Id);
                }
                catch (InvalidOperationException)
                {
                    // already terminated
                }
            }
            else
            {
                SignalShutdownPending();

                stoparguments += " " + descriptor.Arguments;

                Process stopProcess = new Process();
                String executable = descriptor.StopExecutable;

                if (executable == null)
                {
                    executable = descriptor.Executable;
                }

                StartProcess(stopProcess, stoparguments, executable);

                WriteEvent("WaitForProcessToExit "+process.Id+"+"+stopProcess.Id);
                WaitForProcessToExit(process);
                WaitForProcessToExit(stopProcess);
                SignalShutdownComplete();
            }

            if (systemShuttingdown && descriptor.BeepOnShutdown) 
            {
                Console.Beep();
            }

            WriteEvent("Finished " + descriptor.Id);
        }

        private void StopProcessAndChildren(int pid)
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            foreach (var mo in searcher.Get())
            {
                StopProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }

            var proc = Process.GetProcessById(pid);
            WriteEvent("Send SIGINT " + pid);
            bool successful = SigIntHelper.SendSIGINTToProcess(proc,descriptor.StopTimeout);
            if (successful)
            {
                WriteEvent("SIGINT to" + pid + " successful");
            }
            else
            {
                try
                {
                    WriteEvent("SIGINT to " + pid + " failed - Killing as fallback");
                    proc.Kill();
                }
                catch (ArgumentException)
                {
                    // Process already exited.
                }
            }
        }

        private void WaitForProcessToExit(Process process)
        {
            SignalShutdownPending();

            try
            {
//                WriteEvent("WaitForProcessToExit [start]");

                while (!process.WaitForExit(descriptor.SleepTime.Milliseconds))
                {
                    SignalShutdownPending();
//                    WriteEvent("WaitForProcessToExit [repeat]");
                }
            }
            catch (InvalidOperationException)
            {
                // already terminated
            }

//            WriteEvent("WaitForProcessToExit [finished]");
        }

        private void SignalShutdownPending()
        {
            IntPtr handle = this.ServiceHandle;
            wrapperServiceStatus.checkPoint++;
            wrapperServiceStatus.waitHint = descriptor.WaitHint.Milliseconds;
//            WriteEvent("SignalShutdownPending " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            wrapperServiceStatus.currentState = (int)State.SERVICE_STOP_PENDING;
            Advapi32.SetServiceStatus(handle, ref wrapperServiceStatus);
        }

        private void SignalShutdownComplete()
        {
            IntPtr handle = this.ServiceHandle;
            wrapperServiceStatus.checkPoint++;
//            WriteEvent("SignalShutdownComplete " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            wrapperServiceStatus.currentState = (int)State.SERVICE_STOPPED;
            Advapi32.SetServiceStatus(handle, ref wrapperServiceStatus);
        }

        private void StartProcess(Process process, string arguments, String executable)
        {
            var ps = process.StartInfo;
            ps.FileName = executable;
            ps.Arguments = arguments;
            ps.WorkingDirectory = descriptor.WorkingDirectory;
            ps.CreateNoWindow = false;
            ps.UseShellExecute = false;
            ps.RedirectStandardInput = true; // this creates a pipe for stdin to the new process, instead of having it inherit our stdin.
            ps.RedirectStandardOutput = true;
            ps.RedirectStandardError = true;

            foreach (string key in envs.Keys)
                System.Environment.SetEnvironmentVariable(key, envs[key]);
                // ps.EnvironmentVariables[key] = envs[key]; // bugged (lower cases all variable names due to StringDictionary being used, see http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=326163)

            process.Start();
            WriteEvent("Started " + process.Id);

            var priority = descriptor.Priority;
            if (priority != ProcessPriorityClass.Normal)
                process.PriorityClass = priority;

            // monitor the completion of the process
            StartThread(delegate()
            {
                string msg = process.Id + " - " + process.StartInfo.FileName + " " + process.StartInfo.Arguments;
                process.WaitForExit();

                try
                {
                    if (orderlyShutdown)
                    {
                        LogEvent("Child process [" + msg + "] terminated with " + process.ExitCode, EventLogEntryType.Information);
                    }
                    else
                    {
                        LogEvent("Child process [" + msg + "] finished with " + process.ExitCode, EventLogEntryType.Warning);
                        // if we finished orderly, report that to SCM.
                        // by not reporting unclean shutdown, we let Windows SCM to decide if it wants to
                        // restart the service automatically
                        if (process.ExitCode == 0)
                            SignalShutdownComplete();
                        Environment.Exit(process.ExitCode);
                    }
                }
                catch (InvalidOperationException ioe)
                {
                    LogEvent("WaitForExit " + ioe.Message);
                }

                try
                {
                    process.Dispose();
                }
                catch (InvalidOperationException ioe)
                {
                    LogEvent("Dispose " + ioe.Message);
                }
            });
        }

        public static int Main(string[] args)
        {
            try
            {
                Run(args);
                return 0;
            }
            catch (WmiException e)
            {
                Console.Error.WriteLine(e);
                return (int)e.ErrorCode;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return -1;
            }
        }

        private static void ThrowNoSuchService()
        {
            throw new WmiException(ReturnValue.NoSuchService);
        }

        public static void Run(string[] _args)
        {
            if (_args.Length > 0)
            {
                var d = new ServiceDescriptor();
                Win32Services svc = new WmiRoot().GetCollection<Win32Services>();
                Win32Service s = svc.Select(d.Id);

                var args = new List<string>(Array.AsReadOnly(_args));
                if (args[0] == "/redirect")
                {
                    // Redirect output
                    // One might ask why we support this when the caller 
                    // can redirect the output easily. The answer is for supporting UAC.
                    // On UAC-enabled Windows such as Vista, SCM operation requires
                    // elevated privileges, thus winsw.exe needs to be launched
                    // accordingly. This in turn limits what the caller can do,
                    // and among other things it makes it difficult for the caller
                    // to read stdout/stderr. Thus redirection becomes handy.
                    var f = new FileStream(args[1], FileMode.Create);
                    var w = new StreamWriter(f);
                    w.AutoFlush = true;
                    Console.SetOut(w);
                    Console.SetError(w);

                    var handle = f.Handle;
                    Kernel32.SetStdHandle(-11, handle); // set stdout
                    Kernel32.SetStdHandle(-12, handle); // set stder

                    args = args.GetRange(2, args.Count - 2);
                }

                args[0] = args[0].ToLower();
                if (args[0] == "install")
                {
                    string username=null, password=null;
                    if (args.Count > 1 && args[1] == "/p")
                    {
                        // we expected username/password on stdin
                        Console.Write("Username: ");
                        username = Console.ReadLine();
                        Console.Write("Password: ");
                        password = ReadPassword();
                    }
                    else
                    {
                        if (d.HasServiceAccount())
                        {
                            username = d.ServiceAccountUser;
                            password = d.ServiceAccountPassword;
                        }
                    }

                    svc.Create (
                        d.Id,
                        d.Caption,
                        "\"" + d.ExecutablePath + "\"",
                        WMI.ServiceType.OwnProcess,
                        ErrorControl.UserNotified,
                        StartMode.Automatic,
                        d.Interactive,
                        username,
                        password,
                        d.ServiceDependencies);

                    // update the description
                    /* Somehow this doesn't work, even though it doesn't report an error
                    Win32Service s = svc.Select(d.Id);
                    s.Description = d.Description;
                    s.Commit();
                     */

                    // so using a classic method to set the description. Ugly.
                    Registry.LocalMachine.OpenSubKey("System").OpenSubKey("CurrentControlSet").OpenSubKey("Services")
                        .OpenSubKey(d.Id, true).SetValue("Description", d.Description);

                    var actions = d.FailureActions;
                    if (actions.Count > 0)
                    {// set the failure actions
                        using (ServiceManager scm = new ServiceManager())
                        {
                            using (Service sc = scm.Open(d.Id))
                            {
                                sc.ChangeConfig(d.ResetFailureAfter, actions);
                            }
                        }
                    }
                }
                if (args[0] == "uninstall")
                {
                    if (s == null)
                        return; // there's no such service, so consider it already uninstalled
                    try
                    {
                        s.Delete();
                    }
                    catch (WmiException e)
                    {
                        if (e.ErrorCode == ReturnValue.ServiceMarkedForDeletion)
                            return; // it's already uninstalled, so consider it a success
                        throw e;
                    }
                }
                if (args[0] == "start")
                {
                    if (s == null) ThrowNoSuchService();
                    s.StartService();
                }
                if (args[0] == "stop")
                {
                    if (s == null) ThrowNoSuchService();
                    s.StopService();
                }
                if (args[0] == "restart")
                {
                    if (s == null) 
                        ThrowNoSuchService();

                    if(s.Started)
                        s.StopService();

                    while (s.Started)
                    {
                        Thread.Sleep(1000);
                        s = svc.Select(d.Id);
                    }

                    s.StartService();
                }
                if (args[0] == "restart!")
                {
                    // run restart from another process group. see README.md for why this is useful.

                    STARTUPINFO si = new STARTUPINFO();
                    PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                    bool result = Kernel32.CreateProcess(null, d.ExecutablePath+" restart", IntPtr.Zero, IntPtr.Zero, false, 0x200/*CREATE_NEW_PROCESS_GROUP*/, IntPtr.Zero, null, ref si, out pi);
                    if (!result)
                    {
                        throw new Exception("Failed to invoke restart: "+Marshal.GetLastWin32Error());
                    }
                }
                if (args[0] == "status")
                {
                    if (s == null)
                        Console.WriteLine("NonExistent");
                    else if (s.Started)
                        Console.WriteLine("Started");
                    else
                        Console.WriteLine("Stopped");
                }
                if (args[0] == "test")
                {
                    WrapperService wsvc = new WrapperService();
                    wsvc.OnStart(args.ToArray());
                    Thread.Sleep(1000);
                    wsvc.OnStop();
                }
                return;
            }
            ServiceBase.Run(new WrapperService());
        }

        private static string ReadPassword()
        {
            StringBuilder buf = new StringBuilder();
            ConsoleKeyInfo key;
            while (true)
            {
                key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    return buf.ToString();
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    buf.Remove(buf.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else
                {
                    Console.Write('*');
                    buf.Append(key.KeyChar);
                }
            }
        }

    }
}
