using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using log4net;
using WinSW.Configuration;
using WinSW.Extensions;
using WinSW.Util;
using static System.Environment;
using static WinSW.Plugins.RunawayProcessKillerExtension.NativeMethods;

namespace WinSW.Plugins
{
    public partial class RunawayProcessKillerExtension : AbstractWinSWExtension
    {
        /// <summary>
        /// Absolute path to the PID file, which stores ID of the previously launched process.
        /// </summary>
        public string PidFile { get; private set; }

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
        public RunawayProcessKillerExtension(string pidfile, int stopTimeoutMs = 5000, bool stopParentFirst = true, bool checkWinSWEnvironmentVariable = true)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.PidFile = pidfile;
            this.StopTimeout = TimeSpan.FromMilliseconds(stopTimeoutMs);
            this.StopParentProcessFirst = stopParentFirst;
            this.CheckWinSWEnvironmentVariable = checkWinSWEnvironmentVariable;
        }

        private static unsafe string? ReadEnvironmentVariable(IntPtr processHandle, string variable)
        {
            if (IntPtr.Size == sizeof(long))
            {
                return SearchEnvironmentVariable(
                    processHandle,
                    variable,
                    GetEnvironmentAddress64(processHandle).ToInt64(),
                    (handle, address, buffer, size) => NtReadVirtualMemory(handle, new IntPtr(address), buffer, new IntPtr(size)));
            }

            if (Is64BitOSWhen32BitProcess(Process.GetCurrentProcess().Handle) && !Is64BitOSWhen32BitProcess(processHandle))
            {
                return SearchEnvironmentVariable(
                    processHandle,
                    variable,
                    GetEnvironmentAddressWow64(processHandle),
                    (handle, address, buffer, size) => NtWow64ReadVirtualMemory64(handle, address, buffer, size));
            }

            return SearchEnvironmentVariable(
                processHandle,
                variable,
                GetEnvironmentAddress32(processHandle).ToInt64(),
                (handle, address, buffer, size) => NtReadVirtualMemory(handle, new IntPtr(address), buffer, new IntPtr(size)));
        }

        private static bool Is64BitOSWhen32BitProcess(IntPtr processHandle) =>
            IsWow64Process(processHandle, out int isWow64) != 0 && isWow64 != 0;

        private unsafe delegate int ReadMemoryCallback(IntPtr processHandle, long baseAddress, void* buffer, int bufferSize);

        private static unsafe string? SearchEnvironmentVariable(IntPtr processHandle, string variable, long address, ReadMemoryCallback reader)
        {
            const int BaseBufferSize = 0x1000;
            string variableKey = '\0' + variable + '=';
            string buffer = new('\0', BaseBufferSize + variableKey.Length);
            fixed (char* bufferPtr = buffer)
            {
                long startAddress = address;
                for (; ; )
                {
                    int status = reader(processHandle, address, bufferPtr, buffer.Length * sizeof(char));
                    int index = buffer.IndexOf("\0\0");
                    if (index >= 0)
                    {
                        break;
                    }

                    address += BaseBufferSize * sizeof(char);
                }

                for (; ; )
                {
                    int variableIndex = buffer.IndexOf(variableKey);
                    if (variableIndex >= 0)
                    {
                        int valueStartIndex = variableIndex + variableKey.Length;
                        int valueEndIndex = buffer.IndexOf('\0', valueStartIndex);
                        string value = buffer.Substring(valueStartIndex, valueEndIndex - valueStartIndex);
                        return value;
                    }

                    address -= BaseBufferSize * sizeof(char);
                    if (address < startAddress)
                    {
                        break;
                    }

                    int status = reader(processHandle, address, bufferPtr, buffer.Length * sizeof(char));
                }
            }

            return null;
        }

        private static unsafe IntPtr GetEnvironmentAddress32(IntPtr processHandle)
        {
            _ = NtQueryInformationProcess(
                processHandle,
                PROCESSINFOCLASS.ProcessBasicInformation,
                out PROCESS_BASIC_INFORMATION32 information,
                sizeof(PROCESS_BASIC_INFORMATION32));

            PEB32 peb;
            _ = NtReadVirtualMemory(processHandle, new IntPtr(information.PebBaseAddress), &peb, new IntPtr(sizeof(PEB32)));
            RTL_USER_PROCESS_PARAMETERS32 parameters;
            _ = NtReadVirtualMemory(processHandle, new IntPtr(peb.ProcessParameters), &parameters, new IntPtr(sizeof(RTL_USER_PROCESS_PARAMETERS32)));
            return new IntPtr(parameters.Environment);
        }

        private static unsafe IntPtr GetEnvironmentAddress64(IntPtr processHandle)
        {
            _ = NtQueryInformationProcess(
                processHandle,
                PROCESSINFOCLASS.ProcessBasicInformation,
                out PROCESS_BASIC_INFORMATION64 information,
                sizeof(PROCESS_BASIC_INFORMATION64));

            PEB64 peb;
            _ = NtReadVirtualMemory(processHandle, new IntPtr(information.PebBaseAddress), &peb, new IntPtr(sizeof(PEB64)));
            RTL_USER_PROCESS_PARAMETERS64 parameters;
            _ = NtReadVirtualMemory(processHandle, new IntPtr(peb.ProcessParameters), &parameters, new IntPtr(sizeof(RTL_USER_PROCESS_PARAMETERS64)));
            return new IntPtr(parameters.Environment);
        }

        private static unsafe long GetEnvironmentAddressWow64(IntPtr processHandle)
        {
            _ = NtWow64QueryInformationProcess64(
                processHandle,
                PROCESSINFOCLASS.ProcessBasicInformation,
                out var information,
                sizeof(PROCESS_BASIC_INFORMATION64));

            PEB64 peb;
            _ = NtWow64ReadVirtualMemory64(processHandle, information.PebBaseAddress, &peb, sizeof(PEB64));
            RTL_USER_PROCESS_PARAMETERS64 parameters;
            _ = NtWow64ReadVirtualMemory64(processHandle, peb.ProcessParameters, &parameters, sizeof(RTL_USER_PROCESS_PARAMETERS64));
            return parameters.Environment;
        }

        public override void Configure(IServiceConfig descriptor, XmlNode node)
        {
            // We expect the upper logic to process any errors
            // TODO: a better parser API for types would be useful
            this.PidFile = XmlHelper.SingleElement(node, "pidfile", false)!;
            this.StopTimeout = TimeSpan.FromMilliseconds(int.Parse(XmlHelper.SingleElement(node, "stopTimeout", false)!));
            this.StopParentProcessFirst = bool.Parse(XmlHelper.SingleElement(node, "stopParentFirst", false)!);
            this.ServiceId = descriptor.Name;
            // TODO: Consider making it documented
            string? checkWinSWEnvironmentVariable = XmlHelper.SingleElement(node, "checkWinSWEnvironmentVariable", true);
            this.CheckWinSWEnvironmentVariable = checkWinSWEnvironmentVariable is null ? true : bool.Parse(checkWinSWEnvironmentVariable);
        }

        public override void Configure(IServiceConfig descriptor, YamlExtensionConfig config)
        {
            var dict = config.GetSettings();

            this.PidFile = ExpandEnvironmentVariables((string)dict["pidFile"]);

            string stopTimeOutConfig = ExpandEnvironmentVariables((string)dict["stopTimeout"]);
            this.StopTimeout = TimeSpan.FromMilliseconds(int.Parse(stopTimeOutConfig));

            string StopParentProcessFirstConfig = ExpandEnvironmentVariables((string)dict["stopParentFirst"]);
            this.StopParentProcessFirst = bool.Parse(StopParentProcessFirstConfig);

            try
            {
                string CheckWinSWEnvironmentVariableConfig = ExpandEnvironmentVariables((string)dict["checkWinSWEnvironmentVariable"]);
                this.CheckWinSWEnvironmentVariable = bool.Parse(CheckWinSWEnvironmentVariableConfig);
            }
            catch
            {
                this.CheckWinSWEnvironmentVariable = true;
            }
        }

        /// <summary>
        /// This method checks if the PID file is stored on the disk and then terminates runaway processes if they exist.
        /// </summary>
        /// <param name="logger">Unused logger</param>
        public override void OnWrapperStarted()
        {
            // Read PID file from the disk
            int pid;
            if (File.Exists(this.PidFile))
            {
                string pidstring;
                try
                {
                    pidstring = File.ReadAllText(this.PidFile);
                }
                catch (Exception ex)
                {
                    Logger.Error("Cannot read PID file from " + this.PidFile, ex);
                    return;
                }

                try
                {
                    pid = int.Parse(pidstring);
                }
                catch (FormatException e)
                {
                    Logger.Error("Invalid PID file number in '" + this.PidFile + "'. The runaway process won't be checked", e);
                    return;
                }
            }
            else
            {
                Logger.Warn("The requested PID file '" + this.PidFile + "' does not exist. The runaway process won't be checked");
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
            string expectedEnvVarName = WinSWSystem.EnvVarNameServiceId;
            string? affiliatedServiceId = ReadEnvironmentVariable(proc.Handle, expectedEnvVarName);
            if (affiliatedServiceId is null && this.CheckWinSWEnvironmentVariable)
            {
                Logger.Warn("The process " + pid + " has no " + expectedEnvVarName + " environment variable defined. "
                    + "The process has not been started by WinSW, hence it won't be terminated.");

                return;
            }

            // Check the service ID value
            if (this.CheckWinSWEnvironmentVariable && !this.ServiceId.Equals(affiliatedServiceId))
            {
                Logger.Warn("The process " + pid + " has been started by Windows service with ID='" + affiliatedServiceId + "'. "
                    + "It is another service (current service id is '" + this.ServiceId + "'), hence the process won't be terminated.");
                return;
            }

            // Kill the runaway process
            var bldr = new StringBuilder("Stopping the runaway process (pid=");
            bldr.Append(pid);
            bldr.Append(") and its children. Environment was ");
            if (!this.CheckWinSWEnvironmentVariable)
            {
                bldr.Append("not ");
            }

            bldr.Append("checked, affiliated service ID: ");
            bldr.Append(affiliatedServiceId ?? "undefined");
            bldr.Append(", process to kill: ");
            bldr.Append(proc);

            Logger.Warn(bldr.ToString());
            ProcessHelper.StopProcessTree(proc, this.StopTimeout, this.StopParentProcessFirst);
        }

        /// <summary>
        /// Records the started process PID for the future use in OnStart() after the restart.
        /// </summary>
        /// <param name="process"></param>
        public override void OnProcessStarted(Process process)
        {
            Logger.Info("Recording PID of the started process:" + process.Id + ". PID file destination is " + this.PidFile);
            try
            {
                File.WriteAllText(this.PidFile, process.Id.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error("Cannot update the PID file " + this.PidFile, ex);
            }
        }
    }
}
