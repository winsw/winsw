﻿using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Text;

namespace WinSW.Native
{
    internal static class ServiceApis
    {
        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ChangeServiceConfig2W")]
        internal static extern bool ChangeServiceConfig2(IntPtr serviceHandle, ServiceConfigInfoLevels infoLevel, in SERVICE_DESCRIPTION info);

        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ChangeServiceConfig2W")]
        internal static extern bool ChangeServiceConfig2(IntPtr serviceHandle, ServiceConfigInfoLevels infoLevel, in SERVICE_FAILURE_ACTIONS info);

        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ChangeServiceConfig2W")]
        internal static extern bool ChangeServiceConfig2(IntPtr serviceHandle, ServiceConfigInfoLevels infoLevel, in SERVICE_DELAYED_AUTO_START_INFO info);

        [DllImport(Libraries.Advapi32)]
        internal static extern bool CloseServiceHandle(IntPtr objectHandle);

        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateServiceW")]
        internal static extern IntPtr CreateService(
            IntPtr databaseHandle,
            string serviceName,
            string displayName,
            ServiceAccess desiredAccess,
            ServiceType serviceType,
            ServiceStartMode startType,
            ServiceErrorControl errorControl,
            string binaryPath,
            string? loaderOrderGroup,
            IntPtr tagId,
            StringBuilder? dependencies, // TODO
            string? serviceStartName,
            string? password);

        [DllImport(Libraries.Advapi32, SetLastError = true)]
        internal static extern bool DeleteService(IntPtr serviceHandle);

        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenSCManagerW")]
        internal static extern IntPtr OpenSCManager(string? machineName, string? databaseName, ServiceManagerAccess desiredAccess);

        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenServiceW")]
        internal static extern IntPtr OpenService(IntPtr databaseHandle, string serviceName, ServiceAccess desiredAccess);

        [DllImport(Libraries.Advapi32, SetLastError = true)]
        internal static extern bool QueryServiceStatus(IntPtr serviceHandle, out SERVICE_STATUS serviceStatus);

        [DllImport(Libraries.Advapi32, SetLastError = true)]
        internal static extern bool SetServiceObjectSecurity(IntPtr serviceHandle, SecurityInfos securityInformation, byte[] securityDescriptor);

        [DllImport(Libraries.Advapi32)]
        internal static extern bool SetServiceStatus(IntPtr serviceStatusHandle, in SERVICE_STATUS serviceStatus);

        // SERVICE_
        // https://docs.microsoft.com/windows/win32/services/service-security-and-access-rights
        [Flags]
        internal enum ServiceAccess : uint
        {
            QUERY_CONFIG = 0x0001,
            CHANGE_CONFIG = 0x0002,
            QUERY_STATUS = 0x0004,
            ENUMERATE_DEPENDENTS = 0x0008,
            START = 0x0010,
            STOP = 0x0020,
            PAUSE_CONTINUE = 0x0040,
            INTERROGATE = 0x0080,
            USER_DEFINED_CONTROL = 0x0100,

            ALL_ACCESS =
                SecurityApis.StandardAccess.REQUIRED |
                QUERY_CONFIG |
                CHANGE_CONFIG |
                QUERY_STATUS |
                ENUMERATE_DEPENDENTS |
                START |
                STOP |
                PAUSE_CONTINUE |
                INTERROGATE |
                USER_DEFINED_CONTROL,
        }

        // SERVICE_CONFIG_
        // https://docs.microsoft.com/windows/win32/api/winsvc/nf-winsvc-changeserviceconfig2w
        internal enum ServiceConfigInfoLevels : uint
        {
            DESCRIPTION = 1,
            FAILURE_ACTIONS = 2,
            DELAYED_AUTO_START_INFO = 3,
            FAILURE_ACTIONS_FLAG = 4,
            SERVICE_SID_INFO = 5,
            REQUIRED_PRIVILEGES_INFO = 6,
            PRESHUTDOWN_INFO = 7,
            TRIGGER_INFO = 8,
            PREFERRED_NODE = 9,
        }

        // SERVICE_ERROR_
        internal enum ServiceErrorControl : uint
        {
            Ignore = 0x00000000,
            Normal = 0x00000001,
            Severe = 0x00000002,
            Critical = 0x00000003,
        }

        // SC_MANAGER_
        // https://docs.microsoft.com/windows/win32/services/service-security-and-access-rights
        [Flags]
        internal enum ServiceManagerAccess : uint
        {
            CONNECT = 0x0001,
            CREATE_SERVICE = 0x0002,
            ENUMERATE_SERVICE = 0x0004,
            LOCK = 0x0008,
            QUERY_LOCK_STATUS = 0x0010,
            MODIFY_BOOT_CONFIG = 0x0020,

            ALL_ACCESS =
                SecurityApis.StandardAccess.REQUIRED |
                CONNECT |
                CREATE_SERVICE |
                ENUMERATE_SERVICE |
                LOCK |
                QUERY_LOCK_STATUS |
                MODIFY_BOOT_CONFIG,
        }

        internal struct SERVICE_DELAYED_AUTO_START_INFO
        {
            public bool DelayedAutostart;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SERVICE_DESCRIPTION
        {
            public string Description;
        }

        // https://docs.microsoft.com/windows/win32/api/winsvc/ns-winsvc-service_failure_actionsw
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SERVICE_FAILURE_ACTIONS
        {
            public int ResetPeriod;
            public string RebootMessage;
            public string Command;
            public int ActionsCount;
            public unsafe SC_ACTION* Actions;
        }

        internal struct SERVICE_STATUS
        {
            public int ServiceType;
            public ServiceControllerStatus CurrentState;
            public int ControlsAccepted;
            public int Win32ExitCode;
            public int ServiceSpecificExitCode;
            public int CheckPoint;
            public int WaitHint;
        }
    }
}
