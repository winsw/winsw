#pragma warning disable SA1310 // Field names should not contain underscore

using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace WinSW.Native
{
    internal static class ProcessApis
    {
        internal const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;

        [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW")]
        internal static extern bool CreateProcess(
            string? applicationName,
            string? commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string? currentDirectory,
            in STARTUPINFO startupInfo,
            out PROCESS_INFORMATION processInformation);

        [DllImport(Libraries.Kernel32)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport(Libraries.NtDll)]
        internal static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            PROCESSINFOCLASS processInformationClass,
            out PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength,
            IntPtr returnLength = default);

        [DllImport(Libraries.Advapi32, SetLastError = true)]
        internal static extern bool OpenProcessToken(
            IntPtr processHandle,
            TokenAccessLevels desiredAccess,
            out Handle tokenHandle);

        internal enum PROCESSINFOCLASS
        {
            ProcessBasicInformation = 0,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct PROCESS_BASIC_INFORMATION
        {
#pragma warning disable SA1306 // Field names should begin with lower-case letter
            private readonly IntPtr Reserved1;
            private readonly IntPtr PebBaseAddress;
            private readonly IntPtr Reserved2_1;
            private readonly IntPtr Reserved2_2;
            internal readonly IntPtr UniqueProcessId;
            internal readonly IntPtr InheritedFromUniqueProcessId;
#pragma warning restore SA1306 // Field names should begin with lower-case letter
        }

        internal struct PROCESS_INFORMATION
        {
            public IntPtr ProcessHandle;
            public IntPtr ThreadHandle;
            public int ProcessId;
            public int ThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public int Size;
            public string Reserved;
            public string Desktop;
            public string Title;
            public int X;
            public int Y;
            public int XSize;
            public int YSize;
            public int XCountChars;
            public int YCountChars;
            public int FillAttribute;
            public int Flags;
            public short ShowWindow;
            public short ReservedSize2;
            public IntPtr Reserved2;
            public IntPtr StdInputHandle;
            public IntPtr StdOutputHandle;
            public IntPtr StdErrorHandle;
        }
    }
}
