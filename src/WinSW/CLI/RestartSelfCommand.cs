using CommandLine;
using System;
using System.Runtime.InteropServices;
using WinSW.Native;

namespace WinSW.CLI
{
    [Verb("restart!", HelpText = "self-restart (can be called from child processes)")]
    public class RestartSelfCommand : CliCommand
    {
        public override void Run(ServiceDescriptor descriptor)
        {
            if (!Program.elevated)
            {
                throw new UnauthorizedAccessException("Access is denied.");
            }

            Program.Log.Info("Restarting the service with id '" + descriptor.Id + "'");

            // run restart from another process group. see README.md for why this is useful.

            if (!ProcessApis.CreateProcess(null, descriptor.ExecutablePath + " restart", IntPtr.Zero, IntPtr.Zero, false, ProcessApis.CREATE_NEW_PROCESS_GROUP, IntPtr.Zero, null, default, out _))
            {
                throw new CommandException("Failed to invoke restart: " + Marshal.GetLastWin32Error());
            }
        }
    }
}
