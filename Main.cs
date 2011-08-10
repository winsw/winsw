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

namespace winsw
{
    public struct SERVICE_STATUS
    {
        public int serviceType;
        public int currentState;
        public int controlsAccepted;
        public int win32ExitCode;
        public int serviceSpecificExitCode;
        public int checkPoint;
        public int waitHint;
    }

    public enum State
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

    public class WrapperService : ServiceBase
    {
        [DllImport("ADVAPI32.DLL", EntryPoint = "SetServiceStatus")]
        private static extern bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);
		
        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern int SetStdHandle(int device, IntPtr handle); 

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
        /// Copy stuff from StreamReader to StreamWriter
        /// </summary>
        private void CopyStream(Stream i, Stream o)
        {
            byte[] buf = new byte[1024];
            while (true)
            {
                int sz = i.Read(buf, 0, buf.Length);
                if (sz == 0) break;
                o.Write(buf, 0, sz);
                o.Flush();
            }
            i.Close();
            o.Close();
        }

        private void AgeFilename(string baseName, string ext, int count)
        {
            for (int j = count-1; j >= 0; j--)
            {
                string dst = baseName + "." + (j + 1) + ext;
                string src = (j == 0 ? baseName + ext : baseName + "." + (j + 0) + ext);
                if (File.Exists(dst))
                    File.Delete(dst);
                if (File.Exists(src))
                    File.Move(src, dst);
            }
        }

        /// <summary>
        /// Works like the CopyStream method but does a log rotation.
        /// </summary>
        private void CopyStreamWithRotation(Stream data, string baseName, string ext, int threshold, int count)
        {
            byte[] buf = new byte[1024];
            FileStream w = new FileStream(baseName + ext, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            long sz = new FileInfo(baseName + ext).Length;

            while (true)
            {
                int len = data.Read(buf, 0, buf.Length);
                if (len == 0) break;    // EOF
                if (sz + len < threshold)
                {// typical case. write the whole thing into the current file
                    w.Write(buf, 0, len);
                    sz += len;
                }
                else
                {
                    // rotate at the line boundary
                    int s = 0;
                    for (int i = 0; i < len; i++)
                    {
                        if (buf[i] != 0x0A) continue;
                        if (sz + i < threshold) continue;

                        // at the line boundary and exceeded the rotation unit.
                        // time to rotate.
                        w.Write(buf, s, i + 1);
                        w.Close();
                        s = i + 1;

                        try
                        {
                            AgeFilename(baseName, ext, count);
                        }
                        catch (IOException e)
                        {
                            LogEvent("Failed to rotate log: " + e.Message);
                        }

                        // even if the log rotation fails, create a new one, or else
                        // we'll infinitely try to rotate.
                        w = new FileStream(baseName + ext, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        sz = new FileInfo(baseName + ext).Length;
                    }
                }

                w.Flush();
            }
            data.Close();
            w.Close();
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

                        MoveFile(tokens[0], tokens[1]);
                    }
                }
            }
            finally
            {
                File.Delete(file);
            }

        }

        private void MoveFile(string sourceFileName, string destFileName)
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

            string baseName = descriptor.BaseName;
            string errorLogfilename = Path.Combine(logDirectory, baseName + ".err.log");
            string outputLogfilename = Path.Combine(logDirectory, baseName + ".out.log");

            LogMode logfileMode = descriptor.LogFileMode;


            if (logfileMode.mode == LogBehavior.Rotate)
            {
                string logName = Path.Combine(logDirectory, baseName);

                WriteEvent("LogMode: Rotate when size reaches " + logfileMode.logSize + " bytes, and save " + logfileMode.numLogs + " files.");

                StartThread(delegate() { CopyStreamWithRotation(process.StandardOutput.BaseStream, logName, ".out.log", logfileMode.logSize, logfileMode.numLogs); });
                StartThread(delegate() { CopyStreamWithRotation(process.StandardError.BaseStream, logName, ".err.log", logfileMode.logSize, logfileMode.numLogs); });
                return;
            }

            System.IO.FileMode fileMode = FileMode.Append;

            if (logfileMode.mode == LogBehavior.Reset)
            {
                WriteEvent("LogMode: Reset and overwrite existing log.");

                fileMode = FileMode.Create;
            }
            else if (logfileMode.mode == LogBehavior.Roll)
            {
                WriteEvent("LogMode: Roll the logfile over from run to run and save " + logfileMode.numLogs + " files.");

                try
                {
                    string logName = Path.Combine(logDirectory, baseName);

                    AgeFilename(logName, ".out.log", logfileMode.numLogs);
                    AgeFilename(logName, ".err.log", logfileMode.numLogs);
                }
                catch (IOException e)
                {
                    LogEvent("Failed to age log files: " + e.Message);
                    WriteEvent("Failed to age log files: " + e.Message);

                    // If log rollover fails, force the new logfiles to replace any that might exist.
                    fileMode = FileMode.Create;
                }
            }
            else
            {
                WriteEvent("LogMode: Append to existing log.");
            }

            StartThread(delegate() { CopyStream(process.StandardOutput.BaseStream, new FileStream(outputLogfilename, fileMode, FileAccess.Write, FileShare.ReadWrite)); });
            StartThread(delegate() { CopyStream(process.StandardError.BaseStream, new FileStream(errorLogfilename, fileMode, FileAccess.Write, FileShare.ReadWrite)); });
        }

        private void LogEvent(String message)
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

        private void LogEvent(String message, EventLogEntryType type)
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

        public static string ReturnPassword(string username)
        {
            Console.WriteLine("Enter password for domain account. [" + username + "]");

            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    password += info.KeyChar;
                }
                else if (!string.IsNullOrEmpty(password))
                {
                    password = password.Substring(0, password.Length - 1);
                }

                info = Console.ReadKey(true);
            }
            //for (int i = 0; i < password.Length; i++)
            //    Console.Write("*");
            return password;
        }

        protected override void OnStart(string[] args)
        {
            string logDirectory = descriptor.LogDirectory;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            envs = descriptor.EnvironmentVariables;
            foreach (string key in envs.Keys)
            {
                WriteEvent("envar " + key + '=' + envs[key]);
            }

            // handle mapping network drives
            foreach (NetDrive d in descriptor.NetDrives)
            {
                WriteEvent("Mounting share " + d.shareName + " as local drive " + d.localDrive);
                try
                {
                    d.MapDrive();
                }
                catch (Exception)
                {
                    WriteEvent("Failed to mount share " + d.shareName + " as local drive " + d.localDrive);
                    // but just keep going
                }
            }

            // Set the application's working directory.
            System.IO.Directory.SetCurrentDirectory(descriptor.WorkingDirectory);
            WriteEvent("Current directory: " + System.IO.Directory.GetCurrentDirectory());

            HandleFileCopies();

            // handle downloads
            foreach (Download d in descriptor.Downloads)
            {
                LogEvent("Downloading: " + d.From + " to " + d.To);
                try
                {
                    d.Perform();
                }
                catch (Exception e)
                {
                    WriteEvent("Failed to download " + d.From + " to " + d.To, e);
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
                startarguments = descriptor.Arguments + " " + startarguments;
            }

            WriteEvent("Starting " + descriptor.Executable + ' ' + startarguments);
            StartProcess(process, startarguments, descriptor.Executable);

            // send stdout and stderr to its respective output file.
            HandleLogfiles();

            process.StandardInput.Close(); // nothing for you to read!
        }

        protected override void OnShutdown()
        {
            // WriteEvent("OnShutdown");

            try
            {
                this.systemShuttingdown = true;
                StopIt();

                foreach (NetDrive d in descriptor.NetDrives)
                {
                    WriteEvent("Unmounting local drive " + d.localDrive);
                    try
                    {
                        d.UnMapDrive();
                    }
                    catch (Exception e)
                    {
                        LogEvent("Failed to unmount local drive " + d.localDrive + "\n" + e.Message);
                        WriteEvent("Failed to unmount local drive " + d.localDrive);
                        // but just keep going
                    }
                }

            }
            catch (Exception ex)
            {
                WriteEvent("Shutdown exception", ex);
            }
        }

        protected override void OnStop()
        {
            // WriteEvent("OnStop");

            try
            {
                StopIt();

                // arbitrary sleep, unmap was failing from time to time.
                Thread.Sleep(1000);

                // unmap network drives
                foreach (NetDrive d in descriptor.NetDrives)
                {
                    WriteEvent("Unmounting local drive " + d.localDrive);
                    try
                    {
                        d.UnMapDrive();
                    }
                    catch (Exception e)
                    {
                        LogEvent("Failed to unmount local drive " + d.localDrive + "\n" + e.Message);
                        WriteEvent("Failed to unmount local drive " + d.localDrive);
                        // but just keep going
                    }
                }
            }
            catch (Exception ex)
            {
                WriteEvent("Stop exception", ex);
            }
        }

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
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // already terminated
                }
            }
            else
            {
                SignalShutdownPending();

                stoparguments = descriptor.Arguments + " " + stoparguments;

                Process stopProcess = new Process();
                String executable = descriptor.StopExecutable;

                if (executable == null)
                {
                    executable = descriptor.Executable;
                }

                LogEvent("Starting " + executable + ' ' + stoparguments);
                WriteEvent("Starting " + executable + ' ' + stoparguments);

                StartProcess(stopProcess, stoparguments, executable);

                WriteEvent("WaitForProcessToExit " + process.Id + "+" + stopProcess.Id);
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

        private void WaitForProcessToExit(Process process)
        {
            SignalShutdownPending();

            try
            {
                WriteEvent("WaitForProcessToExit [start]");

                while (!process.WaitForExit(descriptor.SleepTime))
                {
                    SignalShutdownPending();
                    WriteEvent("WaitForProcessToExit [repeat]");
                }
            }
            catch (InvalidOperationException)
            {
                // already terminated
            }

            WriteEvent("WaitForProcessToExit [finished]");
        }

        private void SignalShutdownPending()
        {
            IntPtr handle = this.ServiceHandle;
            wrapperServiceStatus.checkPoint++;
            wrapperServiceStatus.waitHint = descriptor.WaitHint;
            WriteEvent("SignalShutdownPending " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            wrapperServiceStatus.currentState = (int)State.SERVICE_STOP_PENDING;
            SetServiceStatus(handle, ref wrapperServiceStatus);
        }

        private void SignalShutdownComplete()
        {
            IntPtr handle = this.ServiceHandle;
            wrapperServiceStatus.checkPoint++;
            WriteEvent("SignalShutdownComplete " + wrapperServiceStatus.checkPoint + ":" + wrapperServiceStatus.waitHint);
            wrapperServiceStatus.currentState = (int)State.SERVICE_STOPPED;
            SetServiceStatus(handle, ref wrapperServiceStatus);
        }

        private void StartProcess(Process process, string arguments, String executable)
        {
            var ps = process.StartInfo;
            ps.FileName = executable;
            ps.Arguments = arguments;
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
                        LogEvent("Child process [" + msg + "] terminated with " + process.ExitCode, EventLogEntryType.Warning);
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
                    SetStdHandle(-11, handle); // set stdout
                    SetStdHandle(-12, handle); // set stder

                    args = args.GetRange(2, args.Count - 2);
                }

                args[0] = args[0].ToLower();
                if (args[0] == "install")
                {
                    if (s != null)
                    {
                        Console.WriteLine("The " + d.Id + " service already exists. (" + (s.Started ? "Running" : "Stopped") + ")");
                        return;
                    }

                    svc.Create(
                        d.Id,
                        d.Caption,
                        "\""+ServiceDescriptor.ExecutablePath+"\"",
                        WMI.ServiceType.OwnProcess,
                        ErrorControl.UserNotified,
                        d.StartMode,
                        d.Interactive,
                        d.DomainUser, 
                        ((d.DomainUser == null) ? null : ReturnPassword(d.DomainUser)),
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
                }
                else if (args[0] == "uninstall")
                {
                    if (s == null)
                    {
                        Console.WriteLine("The " + d.Id + " service does not exist.");
                        return; // there's no such service, so consider it already uninstalled
                    }

                    if (s.Started)
                    {
                        Console.WriteLine("The " + d.Id + " service is still running.  It must be stopped before it can be uninstalled.");
                        return;
                    }

                    try
                    {
                        s.Delete();
                    }
                    catch (WmiException e)
                    {
                        if (e.ErrorCode == ReturnValue.ServiceMarkedForDeletion)
                        {
                            Console.WriteLine("The " + d.Id + " service has been marked for deletion.");
                            return; // it's already uninstalled, so consider it a success
                        }
                        throw e;
                    }
                }
                else if (args[0] == "start")
                {
                    if (s == null)
                    {
                        Console.WriteLine("The " + d.Id + " service does not exist.");
                        return;
                    }

                    if (s.Started)
                    {
                        Console.WriteLine("The " + d.Id + " service is already Running.");
                        return;
                    }

                    //if (s == null) ThrowNoSuchService();
                    s.StartService();
                }
                else if (args[0] == "stop")
                {
                    if (s == null)
                    {
                        Console.WriteLine("The " + d.Id + " service does not exist.");
                        return;
                    }

                    if (!s.Started)
                    {
                        Console.WriteLine("The " + d.Id + " service is already Stopped.");
                        return;
                    }

                    //if (s == null) ThrowNoSuchService();
                    s.StopService();
                }
                else if (args[0] == "restart")
                {
                    if (s == null)
                    {
                        Console.WriteLine("The " + d.Id + " service does not exist.");
                        return;
                    }
                    
                    // if (s == null) ThrowNoSuchService();

                    if (s.Started)
                        s.StopService();

                    while (s.Started)
                    {
                        Thread.Sleep(1000);
                        s = svc.Select(d.Id);
                    }

                    s.StartService();
                }
                else if (args[0] == "status")
                {
                    if (s == null)
                        Console.WriteLine("The " + d.Id + " service does not exist.");
                    else if (s.Started)
                        Console.WriteLine("The " + d.Id + " service is Running.");
                    else
                        Console.WriteLine("The " + d.Id + " service is Stopped.");
                }
                else if (args[0] == "test")
                {
                    WrapperService wsvc = new WrapperService();
                    wsvc.OnStart(args.ToArray());
                    Thread.Sleep(20000);
                    wsvc.OnStop();
                }
                else if ((args[0] == "help") || (args[0] == "?"))
                {
                    Console.WriteLine("Options include: start, restart, stop, status, install, uninstall, and test.");
                }
                return;
            }
            ServiceBase.Run(new WrapperService());
        }
    }
}
