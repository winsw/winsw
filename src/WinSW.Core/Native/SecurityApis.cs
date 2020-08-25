#pragma warning disable SA1310 // Field names should not contain underscore

using System;
using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class SecurityApis
    {
        [DllImport(Libraries.Advapi32, SetLastError = true)]
        internal static extern bool GetTokenInformation(
            IntPtr tokenHandle,
            TOKEN_INFORMATION_CLASS tokenInformationClass,
            out TOKEN_ELEVATION tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "LookupAccountNameW")]
        internal static extern bool LookupAccountName(
            string? systemName,
            string accountName,
            IntPtr sid,
            ref int sidSize,
            IntPtr referencedDomainName,
            ref int referencedDomainNameLength,
            out int use);

        [DllImport(Libraries.Advapi32, SetLastError = false)]
        internal static extern uint LsaAddAccountRights(IntPtr policyHandle, IntPtr accountSid, in LSA_UNICODE_STRING/*[]*/ userRights, uint countOfRights);

        [DllImport(Libraries.Advapi32, SetLastError = false)]
        internal static extern uint LsaClose(IntPtr objectHandle);

        [DllImport(Libraries.Advapi32, SetLastError = false)]
        internal static extern int LsaNtStatusToWinError(uint status);

        [DllImport(Libraries.Advapi32, SetLastError = false)]
        internal static extern uint LsaOpenPolicy(
            IntPtr systemName,
            in LSA_OBJECT_ATTRIBUTES objectAttributes,
            PolicyAccess desiredAccess,
            out IntPtr policyHandle);

        // POLICY_
        // https://docs.microsoft.com/windows/win32/secmgmt/policy-object-access-rights
        [Flags]
        internal enum PolicyAccess : uint
        {
            VIEW_LOCAL_INFORMATION = 0x00000001,
            VIEW_AUDIT_INFORMATION = 0x00000002,
            GET_PRIVATE_INFORMATION = 0x00000004,
            TRUST_ADMIN = 0x00000008,
            CREATE_ACCOUNT = 0x00000010,
            CREATE_SECRET = 0x00000020,
            CREATE_PRIVILEGE = 0x00000040,
            SET_DEFAULT_QUOTA_LIMITS = 0x00000080,
            SET_AUDIT_REQUIREMENTS = 0x00000100,
            AUDIT_LOG_ADMIN = 0x00000200,
            SERVER_ADMIN = 0x00000400,
            LOOKUP_NAMES = 0x00000800,
            NOTIFICATION = 0x00001000,

            ALL_ACCESS =
                StandardAccess.REQUIRED |
                VIEW_LOCAL_INFORMATION |
                VIEW_AUDIT_INFORMATION |
                GET_PRIVATE_INFORMATION |
                TRUST_ADMIN |
                CREATE_ACCOUNT |
                CREATE_SECRET |
                CREATE_PRIVILEGE |
                SET_DEFAULT_QUOTA_LIMITS |
                SET_AUDIT_REQUIREMENTS |
                AUDIT_LOG_ADMIN |
                SERVER_ADMIN |
                LOOKUP_NAMES,
        }

        // STANDARD_RIGHTS_
        // https://docs.microsoft.com/windows/win32/secauthz/standard-access-rights
        [Flags]
        internal enum StandardAccess : uint
        {
            REQUIRED = 0x000F0000,

            READ = 0x00020000,
            WRITE = 0x00020000,
            EXECUTE = 0x00020000,

            ALL = 0x001F0000,
        }

        internal enum TOKEN_INFORMATION_CLASS
        {
            TokenElevation = 20,
        }

        internal struct LSA_OBJECT_ATTRIBUTES
        {
            public uint Length;
            public IntPtr RootDirectory;
            public LSA_UNICODE_STRING ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LSA_UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public string Buffer;
        }

        internal struct TOKEN_ELEVATION
        {
            public uint TokenIsElevated;
        }

        // https://docs.microsoft.com/windows/win32/secauthz/account-rights-constants
        internal static class AccountRights
        {
            internal const string SE_SERVICE_LOGON_NAME = "SeServiceLogonRight";
        }
    }
}
