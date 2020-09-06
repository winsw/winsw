using System;
using Microsoft.Win32;
using WinSW.Native;
using static WinSW.Native.RegistryApis;

namespace WinSW.Util
{
    internal static class RegistryKeyExtensions
    {
        /// <exception cref="CommandException" />
        internal static unsafe DateTime GetLastWriteTime(this RegistryKey registryKey)
        {
            int error = RegQueryInfoKeyW(registryKey.Handle, null, null, null, null, null, null, null, null, null, null, out var lastWriteTime);
            if (error != Errors.ERROR_SUCCESS)
            {
                Throw.Command.Win32Exception(error, "Failed to query registry key.");
            }

            return lastWriteTime.ToDateTime();
        }
    }
}
