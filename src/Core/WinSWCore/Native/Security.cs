using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static WinSW.Native.SecurityApis;

namespace WinSW.Native
{
    internal static class Security
    {
        internal static void AddServiceLogonRight(string domain, string user)
        {
            IntPtr sid = GetAccountSid(domain, user);

            try
            {
                AddAccountRight(sid, AccountRights.SE_SERVICE_LOGON_NAME);
            }
            finally
            {
                _ = FreeSid(sid);
                Marshal.FreeHGlobal(sid);
            }
        }

        private static IntPtr GetAccountSid(string domain, string user)
        {
            int sidSize = 0;
            int domainNameLength = 0;

            if (domain == ".")
            {
                domain = Environment.MachineName;
            }

            string accountName = domain + "\\" + user;
            _ = LookupAccountName(null, accountName, IntPtr.Zero, ref sidSize, IntPtr.Zero, ref domainNameLength, out _);

            IntPtr sid = Marshal.AllocHGlobal(sidSize);
            IntPtr domainName = Marshal.AllocHGlobal(domainNameLength * sizeof(char));

            try
            {
                if (!LookupAccountName(null, accountName, sid, ref sidSize, domainName, ref domainNameLength, out _))
                {
                    Throw.Win32Exception("Failed to find the account.");
                }

                return sid;
            }
            finally
            {
                Marshal.FreeHGlobal(domainName);
            }
        }

        private static void AddAccountRight(IntPtr sid, string rightName)
        {
            uint status = LsaOpenPolicy(IntPtr.Zero, default, PolicyAccess.ALL_ACCESS, out IntPtr policyHandle);
            if (status != 0)
            {
                throw new Win32Exception(LsaNtStatusToWinError(status));
            }

            try
            {
                LSA_UNICODE_STRING userRight = new LSA_UNICODE_STRING
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
    }
}
