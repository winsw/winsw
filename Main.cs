using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.IO;
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
    
    /// <summary>
    /// In-memory representation of the configuration file.
    /// </summary>
    public class ServiceDescriptor
    {
        private readonly XmlDocument dom = new XmlDocument();

        /// <summary>
        /// Where did we find the configuration file?
        /// 
        /// This string is "c:\abc\def\ghi" when the configuration XML is "c:\abc\def\ghi.xml"
        /// </summary>
        public readonly string BasePath;
        /// <summary>
        /// The file name portion of the configuration file.
        /// 
        /// In the above example, this would be "ghi".
        /// </summary>
        public readonly string BaseName;

        public static string ExecutablePath
        {
            get
            {
                // this returns the executable name as given by the calling process, so
                // it needs to be absolutized.
                string p = Environment.GetCommandLineArgs()[0];
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p);

            }
        }

        public ServiceDescriptor()
        {
            // find co-located configuration xml. We search up to the ancestor directories to simplify debugging,
            // as well as trimming off ".vshost" suffix (which is used during debugging)
            string p = ExecutablePath;
            string baseName = Path.GetFileNameWithoutExtension(p);
            if (baseName.EndsWith(".vshost")) baseName = baseName.Substring(0, baseName.Length - 7);
            while (true)
            {
                p = Path.GetDirectoryName(p);
                if (File.Exists(Path.Combine(p, baseName + ".xml")))
                    break;
            }

            // register the base directory as environment variable so that future expansions can refer to this.
            Environment.SetEnvironmentVariable("BASE", p);

            BaseName = baseName;
            BasePath = Path.Combine(p, BaseName);

            dom.Load(BasePath+".xml");
        }

        private string SingleElement(string tagName)
        {
            var n = dom.SelectSingleNode("//" + tagName);
            if (n == null) throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
            return Environment.ExpandEnvironmentVariables(n.InnerText);
        }

        /// <summary>
        /// Path to the executable.
        /// </summary>
        public string Executable
        {
            get
            {
                return SingleElement("executable");
            }
        }

        /// <summary>
        /// Optionally specify a different Path to an executable to shutdown the service.
        /// </summary>
        public string StopExecutable
        {
            get
            {
                return AppendTags("stopexecutable");
            }
        }

        /// <summary>
        /// Arguments or multiple optional argument elements which overrule the arguments element.
        /// </summary>
        public string Arguments
        {
            get
            {
                string arguments = AppendTags("argument");

                if (arguments == null)
                {
                    var tagName = "arguments";
                    var argumentsNode = dom.SelectSingleNode("//" + tagName);

                    if (argumentsNode == null)
                    {
                        if (AppendTags("startargument") == null)
                        {
                            throw new InvalidDataException("<" + tagName + "> is missing in configuration XML");
                        }
                        else
                        {
                            return "";
                        }
                    }

                    return Environment.ExpandEnvironmentVariables(argumentsNode.InnerText);
                }
                else
                {
                    return arguments;
                }
            }
        }

        /// <summary>
        /// Multiple optional startargument elements.
        /// </summary>
        public string Startarguments
        {
            get
            {
                return AppendTags("startargument");
            }
        }

        /// <summary>
        /// Multiple optional stopargument elements.
        /// </summary>
        public string Stoparguments
        {
            get
            {
                return AppendTags("stopargument");
            }
        }

        /// <summary>
        /// Combines the contents of all the elements of the given name,
        /// or return null if no element exists.
        /// </summary>
        private string AppendTags(string tagName)
        {
            XmlNode argumentNode = dom.SelectSingleNode("//" + tagName);

            if (argumentNode == null)
            {
                return null;
            }
            else
            {
                string arguments = "";

                foreach (XmlNode argument in dom.SelectNodes("//" + tagName))
                {
                    arguments += " " + argument.InnerText;
                }

                return Environment.ExpandEnvironmentVariables(arguments);
            }
        }

        /// <summary>
        /// LogDirectory is the service wrapper executable directory or the optionally specified logpath element.
        /// </summary>
        public string LogDirectory
        {
            get
            {
                XmlNode loggingNode = dom.SelectSingleNode("//logpath");

                if (loggingNode != null)
                {
                    return loggingNode.InnerText;
                }
                else
                {
                    return Path.GetDirectoryName(ExecutablePath);
                }
            }
        }


        /// <summary>
        /// Logmode to 'reset', 'roll' once or 'append' [default] the out.log and err.log files.
        /// </summary>
        public string Logmode
        {
            get
            {
                XmlNode logmodeNode = dom.SelectSingleNode("//logmode");

                if (logmodeNode == null)
                {
                    return "append";
                }
                else
                {
                    return logmodeNode.InnerText;
                }
            }
        }

        /// <summary>
        /// Optionally specified depend services that must start before this service starts.
        /// </summary>
        public string[] ServiceDependencies
        {
            get
            {
                System.Collections.ArrayList serviceDependencies = new System.Collections.ArrayList();

                foreach (XmlNode depend in dom.SelectNodes("//depend"))
                {
                    serviceDependencies.Add(depend.InnerText);
                }

                return (string[])serviceDependencies.ToArray(typeof(string));
            }
        }

        public string Id
        {
            get
            {
                return SingleElement("id");
            }
        }

        public string Caption
        {
            get
            {
                return SingleElement("name");
            }
        }

        public string Description
        {
            get
            {
                return SingleElement("description");
            }
        }

        /// <summary>
        /// True if the service can interact with the desktop.
        /// </summary>
        public bool Interactive
        {
            get
            {
                return dom.SelectSingleNode("//interactive") != null;
            }
        }

        /// <summary>
        /// Environment variable overrides
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables
        {
            get
            {
                Dictionary<string, string> map = new Dictionary<string, string>();
                foreach (XmlNode n in dom.SelectNodes("//env"))
                {
                    string key = n.Attributes["name"].Value;
                    string value = Environment.ExpandEnvironmentVariables(n.Attributes["value"].Value);
                    map[key] = value;

                    Environment.SetEnvironmentVariable(key, value);
                }
                return map;
            }
        }
    }

    public class WrapperService : ServiceBase
    {
        [DllImport("ADVAPI32.DLL", EntryPoint = "SetServiceStatus")]
        private static extern bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);
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
        private void CopyStream(StreamReader i, StreamWriter o)
        {
            char[] buf = new char[1024];
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

            System.IO.FileMode fileMode = FileMode.Append;

            if (descriptor.Logmode == "reset")
            {
                fileMode = FileMode.Create;
            }
            else if (descriptor.Logmode == "roll")
            {
                CopyFile(outputLogfilename, outputLogfilename + ".old");
                CopyFile(errorLogfilename, errorLogfilename + ".old");
            }

            new Thread(delegate() { CopyStream(process.StandardOutput, new StreamWriter(new FileStream(outputLogfilename, fileMode))); }).Start();
            new Thread(delegate() { CopyStream(process.StandardError, new StreamWriter(new FileStream(errorLogfilename, fileMode))); }).Start();
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

        private void WriteEvent(String message)
        {
            string logfilename = Path.Combine(descriptor.LogDirectory, descriptor.BaseName + ".wrapper.log");
            StreamWriter log = new StreamWriter(logfilename, true);

            log.WriteLine(message);
            log.Flush();
            log.Close();
        }

        protected override void OnStart(string[] args)
        {
            envs = descriptor.EnvironmentVariables;
            foreach (string key in envs.Keys)
            {
                LogEvent("envar " + key + '=' + envs[key]);
            }
 
            HandleFileCopies();

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

            StartProcess(process, startarguments, descriptor.Executable);

            // send stdout and stderr to its respective output file.
            HandleLogfiles();

            process.StandardInput.Close(); // nothing for you to read!
        }

        protected override void OnShutdown()
        {
            try
            {
                this.systemShuttingdown = true;
                StopIt();
            }
            catch (Exception ex)
            {
                WriteEvent("Shutdown exception:"+ex.Message);
            }
        }

        protected override void OnStop()
        {
            try
            {
                StopIt();
            }
            catch (Exception ex)
            {
                WriteEvent("Stop exception:" + ex.Message);
            }
        }

        private void StopIt()
        {
            string stoparguments = descriptor.Stoparguments;
            LogEvent("Stopping " + descriptor.Id);
            orderlyShutdown = true;

            if (stoparguments == null)
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // already terminated
                }
            }
            else
            {
                stoparguments += " " + descriptor.Arguments;

                Process stopProcess = new Process();
                String executable = descriptor.StopExecutable;

                if (executable == null)
                {
                    executable = descriptor.Executable;
                }

                StartProcess(stopProcess, stoparguments, executable);

                WaitForProcessToExit(process);
                WaitForProcessToExit(stopProcess);
//                Console.Beep();
            }
        }

        private void WaitForProcessToExit(Process process)
        {
            SignalShutdownPending();

            try
            {
                while (process.WaitForExit(3000))
                {
                    SignalShutdownPending();
                }
            }
            catch (InvalidOperationException)
            {
                // already terminated
            }
        }

        private void SignalShutdownPending()
        {
            IntPtr handle = this.ServiceHandle;
            wrapperServiceStatus.checkPoint++;
            wrapperServiceStatus.waitHint = 5000;
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

            // monitor the completion of the process
            new Thread(delegate()
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
            }).Start();
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

        public static void Run(string[] args)
        {
            if (args.Length > 0)
            {
                var d = new ServiceDescriptor();
                Win32Services svc = new WmiRoot().GetCollection<Win32Services>();
                Win32Service s = svc.Select(d.Id);

                args[0] = args[0].ToLower();
                if (args[0] == "install")
                {
                    svc.Create(
                        d.Id,
                        d.Caption,
                        ServiceDescriptor.ExecutablePath,
                        WMI.ServiceType.OwnProcess,
                        ErrorControl.UserNotified,
                        StartMode.Automatic,
                        d.Interactive,
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
                    wsvc.OnStart(args);
                    Thread.Sleep(1000);
                    wsvc.OnStop();
                }
                return;
            }
            ServiceBase.Run(new WrapperService());
        }
    }
}
