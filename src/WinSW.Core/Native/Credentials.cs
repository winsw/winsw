using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static WinSW.Native.CredentialApis;

namespace WinSW.Native
{
    internal static class Credentials
    {
        internal static void PropmtForCredentialsDialog(ref string? userName, ref string? password, string caption, string message)
        {
            userName ??= string.Empty;
            password ??= string.Empty;

            int inBufferSize = 0;
            _ = CredPackAuthenticationBuffer(
                0,
                userName,
                password,
                IntPtr.Zero,
                ref inBufferSize);

            IntPtr inBuffer = Marshal.AllocCoTaskMem(inBufferSize);
            try
            {
                if (!CredPackAuthenticationBuffer(
                    0,
                    userName,
                    password,
                    inBuffer,
                    ref inBufferSize))
                {
                    Throw.Command.Win32Exception("Failed to pack auth buffer.");
                }

                CREDUI_INFO info = new CREDUI_INFO
                {
                    Size = Marshal.SizeOf(typeof(CREDUI_INFO)),
                    CaptionText = caption,
                    MessageText = message,
                };
                uint authPackage = 0;
                bool save = false;
                int error = CredUIPromptForWindowsCredentials(
                    info,
                    0,
                    ref authPackage,
                    inBuffer,
                    inBufferSize,
                    out IntPtr outBuffer,
                    out uint outBufferSize,
                    ref save,
                    CREDUIWIN_GENERIC);

                if (error != Errors.ERROR_SUCCESS)
                {
                    throw new Win32Exception(error);
                }

                try
                {
                    int userNameLength = 0;
                    int passwordLength = 0;
                    _ = CredUnPackAuthenticationBuffer(
                        0,
                        outBuffer,
                        outBufferSize,
                        null,
                        ref userNameLength,
                        default,
                        default,
                        null,
                        ref passwordLength);

                    userName = userNameLength == 0 ? null : new string('\0', userNameLength - 1);
                    password = passwordLength == 0 ? null : new string('\0', passwordLength - 1);

                    if (!CredUnPackAuthenticationBuffer(
                        0,
                        outBuffer,
                        outBufferSize,
                        userName,
                        ref userNameLength,
                        default,
                        default,
                        password,
                        ref passwordLength))
                    {
                        Throw.Command.Win32Exception("Failed to unpack auth buffer.");
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(outBuffer);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(inBuffer);
            }
        }
    }
}
