using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinSW.Native
{
    internal static class RegistryApis
    {
        [DllImport(Libraries.Advapi32, CharSet = CharSet.Unicode)]
        internal static extern unsafe int RegQueryInfoKeyW(
            SafeRegistryHandle keyHandle,
            char* @class,
            int* classLength,
            int* reserved,
            int* subKeysCount,
            int* maxSubKeyLength,
            int* maxClassLength,
            int* valuesCount,
            int* maxValueNameLength,
            int* maxValueLength,
            int* securityDescriptorLength,
            out FileTime lastWriteTime);
    }
}
