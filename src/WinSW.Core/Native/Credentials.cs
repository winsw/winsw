using System;

namespace WinSW.Native
{
    internal static class Credentials
    {
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
    }
}
