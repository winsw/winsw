using System;
using System.Runtime.InteropServices;

namespace WinSW.Plugins
{
    public partial class RunawayProcessKillerExtension
    {
        internal static class NativeMethods
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
