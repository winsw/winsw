using System;
using System.IO;
using System.Text;
using static WinSW.Native.ConsoleApis;

namespace WinSW.Native
{
    internal static class ConsoleEx
    {
        internal static Handle OpenConsoleInput()
        {
            return FileApis.CreateFileW(
                "CONIN$",
                FileApis.GenericAccess.Read | FileApis.GenericAccess.Write,
                FileShare.Read | FileShare.Write,
                IntPtr.Zero,
                FileMode.Open,
                0,
                IntPtr.Zero);
        }

        internal static Handle OpenConsoleOutput()
        {
            return FileApis.CreateFileW(
                "CONOUT$",
                FileApis.GenericAccess.Write,
                FileShare.Write,
                IntPtr.Zero,
                FileMode.Open,
                0,
                IntPtr.Zero);
        }

        internal static string ReadPassword()
        {
            using var consoleInput = OpenConsoleInput();
            using var consoleOutput = OpenConsoleOutput();

            if (!GetConsoleMode(consoleInput, out uint mode))
            {
                Throw.Command.Win32Exception("Failed to get console mode.");
            }

            uint newMode = mode;
            newMode &= ~ENABLE_PROCESSED_INPUT;
            newMode &= ~ENABLE_LINE_INPUT;
            newMode &= ~ENABLE_ECHO_INPUT;
            newMode &= ~ENABLE_MOUSE_INPUT;

            if (newMode != mode)
            {
                if (!SetConsoleMode(consoleInput, newMode))
                {
                    Throw.Command.Win32Exception("Failed to set console mode.");
                }
            }

            try
            {
                var buffer = new StringBuilder();

                while (true)
                {
                    if (!ReadConsoleW(consoleInput, out char key, 1, out _, IntPtr.Zero))
                    {
                        Throw.Command.Win32Exception("Failed to read console.");
                    }

                    if (key == (char)3)
                    {
                        // Ctrl+C
                        Write(consoleOutput, Environment.NewLine);
                        Throw.Command.Win32Exception(Errors.ERROR_CANCELLED);
                    }
                    else if (key == '\r')
                    {
                        Write(consoleOutput, Environment.NewLine);
                        break;
                    }
                    else if (key == '\b')
                    {
                        if (buffer.Length > 0)
                        {
                            buffer.Remove(buffer.Length - 1, 1);
                            Write(consoleOutput, "\b \b");
                        }
                    }
                    else
                    {
                        buffer.Append(key);
                        Write(consoleOutput, "*");
                    }
                }

                return buffer.ToString();
            }
            finally
            {
                if (newMode != mode)
                {
                    if (!SetConsoleMode(consoleInput, mode))
                    {
                        Throw.Command.Win32Exception("Failed to set console mode.");
                    }
                }
            }
        }

        internal static void Write(Handle consoleOutput, string value)
        {
            if (!WriteConsoleW(consoleOutput, value, value.Length, out _, IntPtr.Zero))
            {
                Throw.Command.Win32Exception("Failed to write console.");
            }
        }
    }
}
