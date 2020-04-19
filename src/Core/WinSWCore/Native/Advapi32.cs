using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

// ReSharper disable InconsistentNaming

namespace winsw.Native
{
    public class ServiceManager : IDisposable
    {
        private IntPtr _handle;

        public ServiceManager()
        {
            _handle = Advapi32.OpenSCManager(null, null, (uint)SCM_ACCESS.SC_MANAGER_ALL_ACCESS);
            if (_handle == IntPtr.Zero)
            {
                throw new Exception(string.Format("Error connecting to Service Control Manager. Error provided was: 0x{0:X}", Marshal.GetLastWin32Error()));
            }
        }

        public Service Open(string serviceName)
        {
            IntPtr svcHandle = Advapi32.OpenService(_handle, serviceName, (int)SERVICE_ACCESS.SERVICE_ALL_ACCESS);
            if (svcHandle == IntPtr.Zero)
            {
                throw new Exception(string.Format("Error opening service for modifying. Error returned was: 0x{0:X}", Marshal.GetLastWin32Error()));
            }

            return new Service(svcHandle);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
                _ = Advapi32.CloseServiceHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    public struct Service : IDisposable
    {
        public IntPtr Handle;

        internal Service(IntPtr service)
        {
            Handle = service;
        }

        public void SetFailureActions(TimeSpan failureResetPeriod, List<SC_ACTION> actions)
        {
            SERVICE_FAILURE_ACTIONS sfa = new SERVICE_FAILURE_ACTIONS
            {
                dwResetPeriod = (int)failureResetPeriod.TotalSeconds,
                lpRebootMsg = string.Empty,
                lpCommand = string.Empty
            };
            // delete message
            // delete the command to run

            int len = Marshal.SizeOf(typeof(SC_ACTION));

            sfa.cActions = actions.Count;
            sfa.lpsaActions = Marshal.AllocHGlobal(len * actions.Count);
            try
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    Marshal.StructureToPtr(actions[i], new IntPtr(sfa.lpsaActions.ToInt64() + i * len), false);
                }

                if (!Advapi32.ChangeServiceConfig2(Handle, SERVICE_CONFIG_INFOLEVEL.SERVICE_CONFIG_FAILURE_ACTIONS, sfa))
                    throw new Exception("Failed to change the failure actions", new Win32Exception());
            }
            finally
            {
                Marshal.FreeHGlobal(sfa.lpsaActions);
            }
        }

        /// <summary>
        /// Sets the DelayedAutoStart flag.
        /// It will be applioed to services with Automatic startup mode only.
        /// If the platform does not support this flag, an exception may be thrown.
        /// </summary>
        /// <param name="enabled">Value to set</param>
        /// <exception cref="Exception">Operation failure, e.g. the OS does not support this flag</exception>
        public void SetDelayedAutoStart(bool enabled)
        {
            SERVICE_DELAYED_AUTO_START settings = new SERVICE_DELAYED_AUTO_START
            {
                fDelayedAutostart = enabled
            };

            if (!Advapi32.ChangeServiceConfig2(Handle, SERVICE_CONFIG_INFOLEVEL.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, settings))
            {
                throw new Exception("Failed to change the DelayedAutoStart setting", new Win32Exception());
            }
        }

        public void SetDescription(string description)
        {
            _ = Advapi32.ChangeServiceConfig2(Handle, SERVICE_CONFIG_INFOLEVEL.SERVICE_CONFIG_DESCRIPTION, new SERVICE_DESCRIPTION { lpDescription = description });
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
                _ = Advapi32.CloseServiceHandle(Handle);
            Handle = IntPtr.Zero;
        }
    }

    public static class LogonAsAService
    {
        public static void AddLogonAsAServiceRight(string username)
        {
            // Needs to be at least XP or 2003 server
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms724832%28v=vs.85%29.aspx
            OperatingSystem osInfo = Environment.OSVersion;

            if (osInfo.Version.Major >= 5 && osInfo.Version.Minor >= 1)
            {
                var newuser = GetLocalAccountIfLocalAccount(username);
                // Trace.WriteLine("Username for Logon as A Service: " + newuser);
                long rightexitcode = SetRight(newuser, PrivlegeRights.SeServiceLogonRight.ToString());
                if (rightexitcode != 0)
                {
                    Console.WriteLine("Failed to set logon as a service right");
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.WriteLine("Cannot set Logon as a Service right.  Unsupported operating system detected");
            }
        }

        private static string? GetDomain(string s)
        {
            int stop = s.IndexOf("\\", StringComparison.Ordinal);
            if (stop >= 0)
                return s.Substring(0, stop);
            else
                return null;
        }

        private static string GetLogin(string s)
        {
            int stop = s.IndexOf("\\", StringComparison.Ordinal);
            return (stop > -1) ? s.Substring(stop + 1, s.Length - stop - 1) : s;
        }

        private static string GetLocalAccountIfLocalAccount(string username)
        {
            var machinename = Environment.MachineName;
            string? domain = GetDomain(username);
            if (domain is null || domain.ToLower() == machinename.ToLower() || domain == ".")
            {
                return GetLogin(username);
            }

            return username;
        }

        /// <summary>Adds a privilege to an account</summary>
        /// <param name="accountName">Name of an account - "domain\account" or only "account"</param>
        /// <param name="privilegeName">Name ofthe privilege</param>
        /// <returns>The windows error code returned by LsaAddAccountRights</returns>
        private static long SetRight(string accountName, string privilegeName)
        {
            long winErrorCode; // contains the last error

            // pointer an size for the SID
            IntPtr sid = IntPtr.Zero;
            int sidSize = 0;
            // StringBuilder and size for the domain name
            StringBuilder domainName = new StringBuilder();
            int nameSize = 0;

            // get required buffer size
            _ = Advapi32.LookupAccountName(null, accountName, sid, ref sidSize, domainName, ref nameSize, out _);

            // allocate buffers
            domainName = new StringBuilder(nameSize);
            sid = Marshal.AllocHGlobal(sidSize);

            // lookup the SID for the account
            bool result = Advapi32.LookupAccountName(null, accountName, sid, ref sidSize, domainName, ref nameSize, out _);

            // say what you're doing
            // Console.WriteLine("LookupAccountName result = " + result);
            // Console.WriteLine("IsValidSid: " + Advapi32.IsValidSid(sid));
            // Console.WriteLine("LookupAccountName domainName: " + domainName.ToString());

            if (!result)
            {
                winErrorCode = Marshal.GetLastWin32Error();
                Console.WriteLine("LookupAccountName failed: " + winErrorCode);
            }
            else
            {
                // combine all policies
                const int access = (int)(
                    LSA_AccessPolicy.POLICY_AUDIT_LOG_ADMIN |
                    LSA_AccessPolicy.POLICY_CREATE_ACCOUNT |
                    LSA_AccessPolicy.POLICY_CREATE_PRIVILEGE |
                    LSA_AccessPolicy.POLICY_CREATE_SECRET |
                    LSA_AccessPolicy.POLICY_GET_PRIVATE_INFORMATION |
                    LSA_AccessPolicy.POLICY_LOOKUP_NAMES |
                    LSA_AccessPolicy.POLICY_NOTIFICATION |
                    LSA_AccessPolicy.POLICY_SERVER_ADMIN |
                    LSA_AccessPolicy.POLICY_SET_AUDIT_REQUIREMENTS |
                    LSA_AccessPolicy.POLICY_SET_DEFAULT_QUOTA_LIMITS |
                    LSA_AccessPolicy.POLICY_TRUST_ADMIN |
                    LSA_AccessPolicy.POLICY_VIEW_AUDIT_INFORMATION |
                    LSA_AccessPolicy.POLICY_VIEW_LOCAL_INFORMATION
                    );
                // initialize a pointer for the policy handle

                // get a policy handle
                uint resultPolicy = Advapi32.LsaOpenPolicy(default, default, access, out IntPtr policyHandle);
                winErrorCode = Advapi32.LsaNtStatusToWinError(resultPolicy);

                if (winErrorCode != 0)
                {
                    Console.WriteLine("OpenPolicy failed: " + winErrorCode);
                }
                else
                {
                    // Now that we have the SID an the policy,
                    // we can add rights to the account.

                    // initialize an unicode-string for the privilege name
                    LSA_UNICODE_STRING[] userRights = new LSA_UNICODE_STRING[1];
                    userRights[0].Buffer = Marshal.StringToHGlobalUni(privilegeName);
                    userRights[0].Length = (ushort)(privilegeName.Length * UnicodeEncoding.CharSize);
                    userRights[0].MaximumLength = (ushort)((privilegeName.Length + 1) * UnicodeEncoding.CharSize);

                    // add the right to the account
                    uint res = Advapi32.LsaAddAccountRights(policyHandle, sid, userRights, 1);
                    winErrorCode = Advapi32.LsaNtStatusToWinError(res);
                    if (winErrorCode != 0)
                    {
                        Console.WriteLine("LsaAddAccountRights failed: " + winErrorCode);
                    }

                    _ = Advapi32.LsaClose(policyHandle);
                }

                Advapi32.FreeSid(sid);
            }

            return winErrorCode;
        }
    }

    /// <summary>
    /// Advapi32.dll wrapper for performing additional service related operations that are not
    /// available in WMI.
    /// </summary>
    public class Advapi32
    {
        private const string Advapi32LibraryName = "advapi32.dll";

        [DllImport(Advapi32LibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ChangeServiceConfig2W")]
        internal static extern bool ChangeServiceConfig2(IntPtr hService, SERVICE_CONFIG_INFOLEVEL dwInfoLevel, in SERVICE_FAILURE_ACTIONS lpInfo);

        [DllImport(Advapi32LibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ChangeServiceConfig2W")]
        internal static extern bool ChangeServiceConfig2(IntPtr hService, SERVICE_CONFIG_INFOLEVEL dwInfoLevel, in SERVICE_DELAYED_AUTO_START lpInfo);

        [DllImport(Advapi32LibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ChangeServiceConfig2W")]
        internal static extern bool ChangeServiceConfig2(IntPtr hService, SERVICE_CONFIG_INFOLEVEL dwInfoLevel, in SERVICE_DESCRIPTION lpInfo);

        [DllImport(Advapi32LibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenSCManagerW")]
        internal static extern IntPtr OpenSCManager(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

        [DllImport(Advapi32LibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenServiceW")]
        internal static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

        [DllImport(Advapi32LibraryName, SetLastError = true)]
        internal static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport(Advapi32LibraryName)]
        public static extern bool SetServiceStatus(IntPtr hServiceStatus, in SERVICE_STATUS lpServiceStatus);

        [DllImport(Advapi32LibraryName)]
        public static extern bool SetServiceObjectSecurity(IntPtr hService, SecurityInfos dwSecurityInformation, byte[] lpSecurityDescriptor);

        [DllImport(Advapi32LibraryName)]
        internal static extern uint LsaOpenPolicy(
            in LSA_UNICODE_STRING SystemName,
            in LSA_OBJECT_ATTRIBUTES ObjectAttributes,
            int DesiredAccess,
            out IntPtr PolicyHandle);

        [DllImport(Advapi32LibraryName, SetLastError = true)]
        internal static extern uint LsaAddAccountRights(IntPtr PolicyHandle, IntPtr AccountSid, LSA_UNICODE_STRING[] UserRights, uint CountOfRights);

        [DllImport(Advapi32LibraryName)]
        internal static extern void FreeSid(IntPtr pSid);

        [DllImport(Advapi32LibraryName, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "LookupAccountNameW")]
        internal static extern bool LookupAccountName(
            string? lpSystemName,
            string lpAccountName,
            IntPtr psid,
            ref int cbsid,
            StringBuilder domainName,
            ref int cbdomainLength,
            out int use);

        [DllImport(Advapi32LibraryName)]
        internal static extern bool IsValidSid(IntPtr pSid);

        [DllImport(Advapi32LibraryName, SetLastError = true)]
        internal static extern uint LsaClose(IntPtr ObjectHandle);

        [DllImport(Advapi32LibraryName, SetLastError = false)]
        internal static extern uint LsaNtStatusToWinError(uint status);

        [DllImport(Advapi32LibraryName, SetLastError = true)]
        public static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            TokenAccessLevels DesiredAccess,
            out IntPtr TokenHandle);

        [DllImport(Advapi32LibraryName, SetLastError = true)]
        public static extern bool GetTokenInformation(
          IntPtr TokenHandle,
          TOKEN_INFORMATION_CLASS TokenInformationClass,
          out TOKEN_ELEVATION TokenInformation,
          int TokenInformationLength,
          out int ReturnLength);
    }

    // http://msdn.microsoft.com/en-us/library/windows/desktop/bb545671(v=vs.85).aspx
    internal enum PrivlegeRights
    {
        SeServiceLogonRight,                 // Required for an account to log on using the service logon type.
        SeRemoteInteractiveLogonRight,       // Required for an account to log on remotely using the interactive logon type.
        SeNetworkLogonRight,                 // Required for an account to log on using the network logon type.
        SeInteractiveLogonRight,             // Required for an account to log on using the interactive logon type.
        SeDenyServiceLogonRight,             // Explicitly denies an account the right to log on using the service logon type.
        SeDenyRemoteInteractiveLogonRight,   // Explicitly denies an account the right to log on remotely using the interactive logon type.
        SeDenyNetworkLogonRight,             // Explicitly denies an account the right to log on using the network logon type.
        SeDenyInteractiveLogonRight,         // Explicitly denies an account the right to log on using the interactive logon type.
        SeDenyBatchLogonRight,               // Explicitly denies an account the right to log on using the batch logon type.
        SeBatchLogonRight                    // Required for an account to log on using the batch logon type.
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LSA_UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LSA_OBJECT_ATTRIBUTES
    {
        public int Length;
        public IntPtr RootDirectory;
        public LSA_UNICODE_STRING ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    // enum all policies
    [Flags]
    enum LSA_AccessPolicy : long
    {
        POLICY_VIEW_LOCAL_INFORMATION = 0x00000001L,
        POLICY_VIEW_AUDIT_INFORMATION = 0x00000002L,
        POLICY_GET_PRIVATE_INFORMATION = 0x00000004L,
        POLICY_TRUST_ADMIN = 0x00000008L,
        POLICY_CREATE_ACCOUNT = 0x00000010L,
        POLICY_CREATE_SECRET = 0x00000020L,
        POLICY_CREATE_PRIVILEGE = 0x00000040L,
        POLICY_SET_DEFAULT_QUOTA_LIMITS = 0x00000080L,
        POLICY_SET_AUDIT_REQUIREMENTS = 0x00000100L,
        POLICY_AUDIT_LOG_ADMIN = 0x00000200L,
        POLICY_SERVER_ADMIN = 0x00000400L,
        POLICY_LOOKUP_NAMES = 0x00000800L,
        POLICY_NOTIFICATION = 0x00001000L
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
        SERVICE_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED |
                          SERVICE_QUERY_CONFIG |
                          SERVICE_CHANGE_CONFIG |
                          SERVICE_QUERY_STATUS |
                          SERVICE_ENUMERATE_DEPENDENTS |
                          SERVICE_START |
                          SERVICE_STOP |
                          SERVICE_PAUSE_CONTINUE |
                          SERVICE_INTERROGATE |
                          SERVICE_USER_DEFINED_CONTROL
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
            Type = type;
            Delay = (uint)delay.TotalMilliseconds;
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
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SERVICE_FAILURE_ACTIONS
    {
        /// <summary>
        /// The time after which to reset the failure count to zero if there are no failures, in seconds.
        /// Specify INFINITE to indicate that this value should never be reset.
        /// </summary>
        public int dwResetPeriod;

        public string lpRebootMsg;
        public string lpCommand;
        public int cActions;
        public IntPtr/*SC_ACTION[]*/ lpsaActions;
    }

    // https://msdn.microsoft.com/en-us/library/windows/desktop/ms685155(v=vs.85).aspx
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SERVICE_DELAYED_AUTO_START
    {
        public bool fDelayedAutostart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SERVICE_DESCRIPTION
    {
        public string lpDescription;
    }

    public enum TOKEN_INFORMATION_CLASS
    {
        TokenElevation = 20,
    }

    public struct TOKEN_ELEVATION
    {
        public uint TokenIsElevated;
    }
}
