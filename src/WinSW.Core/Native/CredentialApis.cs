#pragma warning disable SA1310 // Field names should not contain underscore

using System;
using System.Runtime.InteropServices;

namespace WinSW.Native
{
    internal static class CredentialApis
    {
        internal const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        internal const uint CRED_TYPE_GENERIC = 1;

        internal const uint CREDUIWIN_GENERIC = 0x00000001;
        internal const uint CREDUIWIN_CHECKBOX = 0x00000002;

        [DllImport(Libraries.Advapi32, SetLastError = false)]
        internal static extern unsafe void CredFree(CREDENTIALW* buffer);

        [DllImport(Libraries.CredUI, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredPackAuthenticationBufferW")]
        internal static extern bool CredPackAuthenticationBuffer(
            uint flags,
            string userName,
            string password,
            IntPtr packedCredentials,
            ref int packedCredentialsSize);

        [DllImport(Libraries.Advapi32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern unsafe bool CredReadW(string targetName, uint type, uint flags, out CREDENTIALW* credential);

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

        [DllImport(Libraries.Advapi32, SetLastError = true)]
        internal static extern bool CredWriteW(in CREDENTIALW credential, uint flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal unsafe struct CREDENTIALW
        {
            internal uint Flags;
            internal uint Type;
            internal char* TargetName;
            internal char* Comment;
            internal FileTime LastWritten;
            internal int CredentialBlobSize;
            internal IntPtr CredentialBlob;
            internal uint Persist;
            internal uint AttributeCount;
            internal uint Attributes;
            internal char* TargetAlias;
            internal char* UserName;
        }

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
