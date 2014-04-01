using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace winsw
{
    class ServiceManager : IDisposable
    {
        private IntPtr Handle;

        public ServiceManager()
        {
            Handle = Advapi32.OpenSCManager(null, null, (uint)SCM_ACCESS.SC_MANAGER_ALL_ACCESS);
            if (Handle == IntPtr.Zero)
            {
                throw new Exception(String.Format("Error connecting to Service Control Manager. Error provided was: 0x{0:X}", Marshal.GetLastWin32Error()));
            }
        }

        public Service Open(string serviceName)
        {
            IntPtr svcHandle = Advapi32.OpenService(Handle, serviceName, (int)SERVICE_ACCESS.SERVICE_ALL_ACCESS);
            if (svcHandle == IntPtr.Zero)
            {
                throw new Exception(String.Format("Error opening service for modifying. Error returned was: 0x{0:X}", Marshal.GetLastWin32Error()));
            }
            return new Service(svcHandle);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
                Advapi32.CloseServiceHandle(Handle);
            Handle = IntPtr.Zero;
        }
    }

    class Service : IDisposable
    {
        internal IntPtr Handle;

        internal Service(IntPtr service)
        {
            Handle = service;
        }

        public void ChangeConfig(TimeSpan failureResetPeriod, List<SC_ACTION> actions)
        {
            SERVICE_FAILURE_ACTIONS sfa = new SERVICE_FAILURE_ACTIONS();
            sfa.dwResetPeriod = failureResetPeriod.Seconds;
            sfa.lpRebootMsg = ""; // delete message
            sfa.lpCommand = "";   // delete the command to run
            
            int len = Marshal.SizeOf(typeof(SC_ACTION));

            sfa.cActions = actions.Count;
            sfa.lpsaActions = Marshal.AllocHGlobal(len * actions.Count);
            try
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    Marshal.StructureToPtr(actions[i], new IntPtr(sfa.lpsaActions.ToInt64() + i * len), false);
                }

                if (!Advapi32.ChangeServiceConfig2(Handle, SERVICE_CONFIG_INFOLEVEL.SERVICE_CONFIG_FAILURE_ACTIONS, ref sfa))
                    throw new Exception("Failed to change the failure actions", new Win32Exception());
            }
            finally
            {
                Marshal.FreeHGlobal(sfa.lpsaActions);
            }
        }

        public void Dispose()
        {
            if (Handle!=IntPtr.Zero)
                Advapi32.CloseServiceHandle(Handle);
            Handle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Advapi32.dll wrapper for performing additional service related operations that are not
    /// available in WMI.
    /// </summary>
    internal class Advapi32
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChangeServiceConfig2(IntPtr hService, SERVICE_CONFIG_INFOLEVEL dwInfoLevel, IntPtr lpInfo);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChangeServiceConfig2(IntPtr hService, SERVICE_CONFIG_INFOLEVEL dwInfoLevel, ref SERVICE_FAILURE_ACTIONS sfa);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("ADVAPI32.DLL")]
        internal static extern bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);
    }


    internal enum SCM_ACCESS : uint
    {
        /// <summary>
        /// Required to connect to the service control manager.
        /// </summary>
        SC_MANAGER_CONNECT = 0x00001,

        /// <summary>
        /// Required to call the CreateService function to create a service
        /// object and add it to the database.
        /// </summary>
        SC_MANAGER_CREATE_SERVICE = 0x00002,

        /// <summary>
        /// Required to call the EnumServicesStatusEx function to list the 
        /// services that are in the database.
        /// </summary>
        SC_MANAGER_ENUMERATE_SERVICE = 0x00004,

        /// <summary>
        /// Required to call the LockServiceDatabase function to acquire a 
        /// lock on the database.
        /// </summary>
        SC_MANAGER_LOCK = 0x00008,

        /// <summary>
        /// Required to call the QueryServiceLockStatus function to retrieve 
        /// the lock status information for the database.
        /// </summary>
        SC_MANAGER_QUERY_LOCK_STATUS = 0x00010,

        /// <summary>
        /// Required to call the NotifyBootConfigStatus function.
        /// </summary>
        SC_MANAGER_MODIFY_BOOT_CONFIG = 0x00020,

        /// <summary>
        /// Includes STANDARD_RIGHTS_REQUIRED, in addition to all access 
        /// rights in this table.
        /// </summary>
        SC_MANAGER_ALL_ACCESS = ACCESS_MASK.STANDARD_RIGHTS_REQUIRED |
            SC_MANAGER_CONNECT |
            SC_MANAGER_CREATE_SERVICE |
            SC_MANAGER_ENUMERATE_SERVICE |
            SC_MANAGER_LOCK |
            SC_MANAGER_QUERY_LOCK_STATUS |
            SC_MANAGER_MODIFY_BOOT_CONFIG,

        GENERIC_READ = ACCESS_MASK.STANDARD_RIGHTS_READ |
            SC_MANAGER_ENUMERATE_SERVICE |
            SC_MANAGER_QUERY_LOCK_STATUS,

        GENERIC_WRITE = ACCESS_MASK.STANDARD_RIGHTS_WRITE |
            SC_MANAGER_CREATE_SERVICE |
            SC_MANAGER_MODIFY_BOOT_CONFIG,

        GENERIC_EXECUTE = ACCESS_MASK.STANDARD_RIGHTS_EXECUTE |
            SC_MANAGER_CONNECT | SC_MANAGER_LOCK,

        GENERIC_ALL = SC_MANAGER_ALL_ACCESS,
    }

    [Flags]
    internal enum SERVICE_ACCESS : uint
    {
        STANDARD_RIGHTS_REQUIRED = 0xF0000,
        SERVICE_QUERY_CONFIG = 0x00001,
        SERVICE_CHANGE_CONFIG = 0x00002,
        SERVICE_QUERY_STATUS = 0x00004,
        SERVICE_ENUMERATE_DEPENDENTS = 0x00008,
        SERVICE_START = 0x00010,
        SERVICE_STOP = 0x00020,
        SERVICE_PAUSE_CONTINUE = 0x00040,
        SERVICE_INTERROGATE = 0x00080,
        SERVICE_USER_DEFINED_CONTROL = 0x00100,
        SERVICE_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED |
                          SERVICE_QUERY_CONFIG |
                          SERVICE_CHANGE_CONFIG |
                          SERVICE_QUERY_STATUS |
                          SERVICE_ENUMERATE_DEPENDENTS |
                          SERVICE_START |
                          SERVICE_STOP |
                          SERVICE_PAUSE_CONTINUE |
                          SERVICE_INTERROGATE |
                          SERVICE_USER_DEFINED_CONTROL)
    }

    [Flags]
    internal enum ACCESS_MASK : uint
    {
        DELETE = 0x00010000,
        READ_CONTROL = 0x00020000,
        WRITE_DAC = 0x00040000,
        WRITE_OWNER = 0x00080000,
        SYNCHRONIZE = 0x00100000,

        STANDARD_RIGHTS_REQUIRED = 0x000f0000,

        STANDARD_RIGHTS_READ = 0x00020000,
        STANDARD_RIGHTS_WRITE = 0x00020000,
        STANDARD_RIGHTS_EXECUTE = 0x00020000,

        STANDARD_RIGHTS_ALL = 0x001f0000,

        SPECIFIC_RIGHTS_ALL = 0x0000ffff,

        ACCESS_SYSTEM_SECURITY = 0x01000000,

        MAXIMUM_ALLOWED = 0x02000000,

        GENERIC_READ = 0x80000000,
        GENERIC_WRITE = 0x40000000,
        GENERIC_EXECUTE = 0x20000000,
        GENERIC_ALL = 0x10000000,

        DESKTOP_READOBJECTS = 0x00000001,
        DESKTOP_CREATEWINDOW = 0x00000002,
        DESKTOP_CREATEMENU = 0x00000004,
        DESKTOP_HOOKCONTROL = 0x00000008,
        DESKTOP_JOURNALRECORD = 0x00000010,
        DESKTOP_JOURNALPLAYBACK = 0x00000020,
        DESKTOP_ENUMERATE = 0x00000040,
        DESKTOP_WRITEOBJECTS = 0x00000080,
        DESKTOP_SWITCHDESKTOP = 0x00000100,

        WINSTA_ENUMDESKTOPS = 0x00000001,
        WINSTA_READATTRIBUTES = 0x00000002,
        WINSTA_ACCESSCLIPBOARD = 0x00000004,
        WINSTA_CREATEDESKTOP = 0x00000008,
        WINSTA_WRITEATTRIBUTES = 0x00000010,
        WINSTA_ACCESSGLOBALATOMS = 0x00000020,
        WINSTA_EXITWINDOWS = 0x00000040,
        WINSTA_ENUMERATE = 0x00000100,
        WINSTA_READSCREEN = 0x00000200,

        WINSTA_ALL_ACCESS = 0x0000037f
    }

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
    

    // http://msdn.microsoft.com/en-us/library/windows/desktop/ms685126(v=vs.85).aspx
    [StructLayout(LayoutKind.Sequential)]
    public struct SC_ACTION
    {
        public SC_ACTION_TYPE Type;
        /// <summary>
        /// The time to wait before performing the specified action, in milliseconds.
        /// </summary>
        public uint Delay;

        public SC_ACTION(SC_ACTION_TYPE type, TimeSpan delay)
        {
            this.Type = type;
            this.Delay = (uint)delay.TotalMilliseconds;
        }
    }

    internal enum SERVICE_CONFIG_INFOLEVEL
    {
        SERVICE_CONFIG_DESCRIPTION = 1,
        SERVICE_CONFIG_FAILURE_ACTIONS = 2,
        SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3,
        SERVICE_CONFIG_FAILURE_ACTIONS_FLAG = 4,
        SERVICE_CONFIG_SERVICE_SID_INFO = 5,
        SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO = 6,
        SERVICE_CONFIG_PRESHUTDOWN_INFO = 7,
        SERVICE_CONFIG_TRIGGER_INFO = 8,
        SERVICE_CONFIG_PREFERRED_NODE = 9
    }

    public enum SC_ACTION_TYPE
    {
        SC_ACTION_NONE = 0,
        SC_ACTION_RESTART = 1,
        SC_ACTION_REBOOT = 2,
        SC_ACTION_RUN_COMMAND = 3
    }

    // http://msdn.microsoft.com/en-us/library/windows/desktop/ms685939(v=vs.85).aspx
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    public struct SERVICE_FAILURE_ACTIONS
    {
        /// <summary>
        /// The time after which to reset the failure count to zero if there are no failures, in seconds.
        /// Specify INFINITE to indicate that this value should never be reset.
        /// </summary>
        public int dwResetPeriod;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpRebootMsg;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpCommand;
        public int cActions;
        public IntPtr/*SC_ACTION[]*/ lpsaActions;
    }
}
