using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static WinSW.Native.SecurityApis;

namespace WinSW.Native
{
    internal static class Security
    {
        /// <exception cref="CommandException" />
        /// <exception cref="Win32Exception" />
        internal static void AddServiceLogonRight(ref string userName)
        {
            var sid = GetAccountSid(ref userName);

            try
            {
                AddAccountRight(sid, AccountRights.SE_SERVICE_LOGON_NAME);
            }
            finally
            {
                Marshal.FreeHGlobal(sid);
            }
        }

        /// <exception cref="CommandException" />
        private static IntPtr GetAccountSid(ref string accountName)
        {
            int sidSize = 0;
            int domainNameLength = 0;

            if (accountName.StartsWith(".\\"))
            {
                accountName = Environment.MachineName + accountName.Substring(1);
            }

            _ = LookupAccountName(null, accountName, IntPtr.Zero, ref sidSize, null, ref domainNameLength, out _);

            var sid = Marshal.AllocHGlobal(sidSize);
            try
            {
                string? domainName = domainNameLength == 0 ? null : new string('\0', domainNameLength - 1);

                if (!LookupAccountName(null, accountName, sid, ref sidSize, domainName, ref domainNameLength, out _))
                {
                    Throw.Command.Win32Exception("Failed to find the account.");
                }

                // intentionally undocumented
                if (!accountName.Contains("\\") && !accountName.Contains("@"))
                {
                    accountName = domainName + '\\' + accountName;
                }

                return sid;
            }
            catch
            {
                Marshal.FreeHGlobal(sid);
                throw;
            }
        }

        /// <exception cref="Win32Exception" />
        private static void AddAccountRight(IntPtr sid, string rightName)
        {
            uint status = LsaOpenPolicy(IntPtr.Zero, default, PolicyAccess.ALL_ACCESS, out var policyHandle);
            if (status != 0)
            {
                throw new Win32Exception(LsaNtStatusToWinError(status));
            }

            try
            {
                var userRight = new LSA_UNICODE_STRING
                {
                    Buffer = rightName,
                    Length = (ushort)(rightName.Length * sizeof(char)),
                    MaximumLength = (ushort)((rightName.Length + 1) * sizeof(char)),
                };

                status = LsaAddAccountRights(policyHandle, sid, userRight, 1);
                if (status != 0)
                {
                    throw new Win32Exception(LsaNtStatusToWinError(status));
                }
            }
            finally
            {
                _ = LsaClose(policyHandle);
            }
        }

        internal static bool IsSpecialAccount(string accountName) => accountName switch
        {
            @"LocalSystem" => true,
            @".\LocalSystem" => true,
            @"NT AUTHORITY\LocalService" => true,
            @"NT AUTHORITY\NetworkService" => true,
            string name when name == $@"{Environment.MachineName}\LocalSystem" => true,
            _ => false
        };
    }
}
