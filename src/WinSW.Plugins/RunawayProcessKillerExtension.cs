using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using log4net;
using WinSW.Extensions;
using WinSW.Util;
using static WinSW.Plugins.RunawayProcessKiller.RunawayProcessKillerExtension.Native;

namespace WinSW.Plugins.RunawayProcessKiller
{
    public partial class RunawayProcessKillerExtension : AbstractWinSWExtension
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
        public RunawayProcessKillerExtension(string pidfile, int stopTimeoutMs = 5000, bool checkWinSWEnvironmentVariable = true)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        {
            this.Pidfile = pidfile;
            this.StopTimeout = TimeSpan.FromMilliseconds(stopTimeoutMs);
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
            string buffer = new string('\0', BaseBufferSize + variableKey.Length);
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
                out PROCESS_BASIC_INFORMATION64 information,
                sizeof(PROCESS_BASIC_INFORMATION64));

            PEB64 peb;
            _ = NtWow64ReadVirtualMemory64(processHandle, information.PebBaseAddress, &peb, sizeof(PEB64));
            RTL_USER_PROCESS_PARAMETERS64 parameters;
            _ = NtWow64ReadVirtualMemory64(processHandle, peb.ProcessParameters, &parameters, sizeof(RTL_USER_PROCESS_PARAMETERS64));
            return parameters.Environment;
        }

        public override void Configure(ServiceDescriptor descriptor, XmlNode node)
        {
            // We expect the upper logic to process any errors
            // TODO: a better parser API for types would be useful
            this.Pidfile = XmlHelper.SingleElement(node, "pidfile", false)!;
            this.StopTimeout = TimeSpan.FromMilliseconds(int.Parse(XmlHelper.SingleElement(node, "stopTimeout", false)!));
            this.ServiceId = descriptor.Id;

            // TODO: Consider making it documented
            var checkWinSWEnvironmentVariable = XmlHelper.SingleElement(node, "checkWinSWEnvironmentVariable", true);
            this.CheckWinSWEnvironmentVariable = checkWinSWEnvironmentVariable is null ? true : bool.Parse(checkWinSWEnvironmentVariable);
        }

        /// <summary>
        /// This method checks if the PID file is stored on the disk and then terminates runaway processes if they exist.
        /// </summary>
        /// <param name="logger">Unused logger</param>
        public override void OnWrapperStarted()
        {
            // Read PID file from the disk
            int pid;
            if (File.Exists(this.Pidfile))
            {
                string pidstring;
                try
                {
                    pidstring = File.ReadAllText(this.Pidfile);
                }
                catch (Exception ex)
                {
                    Logger.Error("Cannot read PID file from " + this.Pidfile, ex);
                    return;
                }

                try
                {
                    pid = int.Parse(pidstring);
                }
                catch (FormatException e)
                {
                    Logger.Error("Invalid PID file number in '" + this.Pidfile + "'. The runaway process won't be checked", e);
                    return;
                }
            }
            else
            {
                Logger.Warn("The requested PID file '" + this.Pidfile + "' does not exist. The runaway process won't be checked");
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
            StringBuilder bldr = new StringBuilder("Stopping the runaway process (pid=");
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
            ProcessHelper.StopProcessTree(proc, this.StopTimeout);
        }

        /// <summary>
        /// Records the started process PID for the future use in OnStart() after the restart.
        /// </summary>
        public override void OnProcessStarted(Process process)
        {
            Logger.Info("Recording PID of the started process:" + process.Id + ". PID file destination is " + this.Pidfile);
            try
            {
                File.WriteAllText(this.Pidfile, process.Id.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error("Cannot update the PID file " + this.Pidfile, ex);
            }
        }

        internal static class Native
        {
            private const string Kernel32 = "kernel32.dll";
            private const string NTDll = "ntdll.dll";

            [DllImport(Kernel32)]
            internal static extern int IsWow64Process(IntPtr hProcess, out int Wow64Process);

            [DllImport(NTDll)]
            internal static extern int NtQueryInformationProcess(
                IntPtr ProcessHandle,
                PROCESSINFOCLASS ProcessInformationClass,
                out PROCESS_BASIC_INFORMATION32 ProcessInformation,
                int ProcessInformationLength,
                IntPtr ReturnLength = default);

            [DllImport(NTDll)]
            internal static extern int NtQueryInformationProcess(
                IntPtr ProcessHandle,
                PROCESSINFOCLASS ProcessInformationClass,
                out PROCESS_BASIC_INFORMATION64 ProcessInformation,
                int ProcessInformationLength,
                IntPtr ReturnLength = default);

            [DllImport(NTDll)]
            internal static extern unsafe int NtReadVirtualMemory(
                IntPtr ProcessHandle,
                IntPtr BaseAddress,
                void* Buffer,
                IntPtr BufferSize,
                IntPtr NumberOfBytesRead = default);

            [DllImport(NTDll)]
            internal static extern int NtWow64QueryInformationProcess64(
                IntPtr ProcessHandle,
                PROCESSINFOCLASS ProcessInformationClass,
                out PROCESS_BASIC_INFORMATION64 ProcessInformation,
                int ProcessInformationLength,
                IntPtr ReturnLength = default);

            [DllImport(NTDll)]
            internal static extern unsafe int NtWow64ReadVirtualMemory64(
                IntPtr ProcessHandle,
                long BaseAddress,
                void* Buffer,
                long BufferSize,
                long NumberOfBytesRead = default);

            internal enum PROCESSINFOCLASS
            {
                ProcessBasicInformation = 0,
            }

            [StructLayout(LayoutKind.Sequential)]
            internal readonly struct MEMORY_BASIC_INFORMATION
            {
                public readonly IntPtr BaseAddress;
                private readonly IntPtr AllocationBase;
                private readonly uint AllocationProtect;
                public readonly IntPtr RegionSize;
                private readonly uint State;
                private readonly uint Protect;
                private readonly uint Type;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct PROCESS_BASIC_INFORMATION32
            {
                private readonly int Reserved1;
                public readonly int PebBaseAddress;
                private fixed int Reserved2[2];
                private readonly uint UniqueProcessId;
                private readonly int Reserved3;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct PROCESS_BASIC_INFORMATION64
            {
                private readonly long Reserved1;
                public readonly long PebBaseAddress;
                private fixed long Reserved2[2];
                private readonly ulong UniqueProcessId;
                private readonly long Reserved3;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct PEB32
            {
                private fixed byte Reserved1[2];
                private readonly byte BeingDebugged;
                private fixed byte Reserved2[1];
                private fixed int Reserved3[2];
                private readonly int Ldr;
                public readonly int ProcessParameters;
                private fixed int Reserved4[3];
                private readonly int AtlThunkSListPtr;
                private readonly int Reserved5;
                private readonly uint Reserved6;
                private readonly int Reserved7;
                private readonly uint Reserved8;
                private readonly uint AtlThunkSListPtr32;
                private fixed int Reserved9[45];
                private fixed byte Reserved10[96];
                private readonly int PostProcessInitRoutine;
                private fixed byte Reserved11[128];
                private fixed int Reserved12[1];
                private readonly uint SessionId;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct PEB64
            {
                private fixed byte Reserved1[2];
                private readonly byte BeingDebugged;
                private fixed byte Reserved2[1];
                private fixed long Reserved3[2];
                private readonly long Ldr;
                public readonly long ProcessParameters;
                private fixed long Reserved4[3];
                private readonly long AtlThunkSListPtr;
                private readonly long Reserved5;
                private readonly uint Reserved6;
                private readonly long Reserved7;
                private readonly uint Reserved8;
                private readonly uint AtlThunkSListPtr32;
                private fixed long Reserved9[45];
                private fixed byte Reserved10[96];
                private readonly long PostProcessInitRoutine;
                private fixed byte Reserved11[128];
                private fixed long Reserved12[1];
                private readonly uint SessionId;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct RTL_USER_PROCESS_PARAMETERS32
            {
                private fixed byte Reserved1[16];
                private fixed int Reserved2[10];
                private readonly UNICODE_STRING32 ImagePathName;
                private readonly UNICODE_STRING32 CommandLine;

                internal readonly int Environment;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct RTL_USER_PROCESS_PARAMETERS64
            {
                private fixed byte Reserved1[16];
                private fixed long Reserved2[10];
                private readonly UNICODE_STRING64 ImagePathName;
                private readonly UNICODE_STRING64 CommandLine;

                internal readonly long Environment;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal readonly struct UNICODE_STRING32
            {
                private readonly ushort Length;
                private readonly ushort MaximumLength;
                private readonly int Buffer;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal readonly struct UNICODE_STRING64
            {
                private readonly ushort Length;
                private readonly ushort MaximumLength;
                private readonly long Buffer;
            }
        }
    }
}
