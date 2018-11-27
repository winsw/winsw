using System;
using System.Runtime.InteropServices;

namespace winsw.Native
{
    internal static class CredentialApis
    {
        internal const uint CREDUIWIN_GENERIC = 0x00000001;

        [DllImport(Libraries.CredUI, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredPackAuthenticationBufferW")]
        internal static extern bool CredPackAuthenticationBuffer(
            uint flags,
            string userName,
            string password,
            IntPtr packedCredentials,
            ref int packedCredentialsSize);

        [DllImport(Libraries.CredUI, SetLastError = false, CharSet = CharSet.Unicode, EntryPoint = "CredUIPromptForWindowsCredentialsW")]
        internal static extern int CredUIPromptForWindowsCredentials(
            in CREDUI_INFO uiInfo,
            uint authError,
            ref uint authPackage,
            IntPtr inAuthBuffer,
            int inAuthBufferSize,
            out IntPtr outAuthBuffer,
            out uint outAuthBufferSize,
            ref bool save,
            uint flags);

        [DllImport(Libraries.CredUI, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredUnPackAuthenticationBufferW")]
        internal static extern bool CredUnPackAuthenticationBuffer(
            uint flags,
            IntPtr authBuffer,
            uint authBufferSize,
            string? userName,
            ref int maxUserName,
            string? domainName,
            IntPtr maxDomainName,
            string? password,
            ref int maxPassword);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct CREDUI_INFO
        {
            internal int Size;
            internal IntPtr ParentWindow;
            internal string MessageText;
            internal string CaptionText;
            internal IntPtr BannerBitmap;
        }
    }
}
