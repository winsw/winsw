#pragma warning disable SA1310 // Field names should not contain underscore

using System;
using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class JobApis
    {
        internal const uint JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800;
        internal const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport(Libraries.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateJobObjectW(IntPtr jobAttributes = default, string? name = null);

        [DllImport(Libraries.Kernel32, SetLastError = true)]
        internal static extern bool SetInformationJobObject(
            IntPtr job,
            JOBOBJECTINFOCLASS jobObjectInformationClass,
            in JOBOBJECT_EXTENDED_LIMIT_INFORMATION jobObjectInformation,
            int jobObjectInformationLength);

        internal enum JOBOBJECTINFOCLASS
        {
            JobObjectExtendedLimitInformation = 9,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public IntPtr MinimumWorkingSetSize;
            public IntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public IntPtr ProcessMemoryLimit;
            public IntPtr JobMemoryLimit;
            public IntPtr PeakProcessMemoryUsed;
            public IntPtr PeakJobMemoryUsed;
        }
    }
}
