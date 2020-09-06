using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static WinSW.Native.CredentialApis;

namespace WinSW.Native
{
    internal static class Credentials
    {
        internal static unsafe bool Load(string targetName, out string? userName, out string? password)
        {
            if (!CredReadW(targetName, CRED_TYPE_GENERIC, 0, out var credential))
            {
                userName = null;
                password = null;
                return false;
            }

            try
            {
                userName = Marshal.PtrToStringUni((IntPtr)credential->UserName);
                password = Marshal.PtrToStringUni(credential->CredentialBlob, credential->CredentialBlobSize);
                return true;
            }
            finally
            {
                CredFree(credential);
            }
        }

        internal static unsafe void Save(string targetName, string? userName, string? password)
        {
#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement
            fixed (char* targetNamePtr = targetName)
            fixed (char* userNamePtr = userName)
            fixed (char* passwordPtr = password)
            {
                var credential = new CREDENTIALW
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = targetNamePtr,
                    CredentialBlobSize = password?.Length * sizeof(char) ?? 0,
                    CredentialBlob = (IntPtr)passwordPtr,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    UserName = userNamePtr,
                };

                if (!CredWriteW(credential, 0))
                {
                    Throw.Command.Win32Exception("Failed to save credential.");
                }
            }
#pragma warning restore SA1519 // Braces should not be omitted from multi-line child statement
        }

        internal static void PromptForCredentialsConsole(ref string? userName, ref string? password)
        {
            using var consoleOutput = ConsoleEx.OpenConsoleOutput();

            if (userName is null)
            {
                ConsoleEx.Write(consoleOutput, "Username: ");
                userName = Console.ReadLine()!;
            }

            if (password is null && !Security.IsSpecialAccount(userName))
            {
                ConsoleEx.Write(consoleOutput, "Password: ");
                password = ConsoleEx.ReadPassword();
            }
        }

        internal static void PromptForCredentialsDialog(ref string? userName, ref string? password, string caption, string message, ref bool save)
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

            var inBuffer = Marshal.AllocCoTaskMem(inBufferSize);
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

                var info = new CREDUI_INFO
                {
                    Size = Marshal.SizeOf<CREDUI_INFO>(),
                    CaptionText = caption,
                    MessageText = message,
                };
                uint authPackage = 0;
                int error = CredUIPromptForWindowsCredentials(
                    info,
                    0,
                    ref authPackage,
                    inBuffer,
                    inBufferSize,
                    out var outBuffer,
                    out uint outBufferSize,
                    ref save,
                    CREDUIWIN_GENERIC | CREDUIWIN_CHECKBOX);

                if (error != Errors.ERROR_SUCCESS)
                {
                    if (error == Errors.ERROR_CANCELLED)
                    {
                        Throw.Command.Win32Exception(error);
                    }

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
