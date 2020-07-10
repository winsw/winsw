using CommandLine;
using System;
using System.Runtime.InteropServices;
using winsw.Native;
using WMI;

namespace winsw.CLI
{
    [Verb("restart!", HelpText = "self-restart (can be called from child processes)")]
    public class RestartSelf : CliOption
    {
        public override void Run(ServiceDescriptor descriptor, Win32Services svcs, Win32Service? svc)
        {
            var Log = Program.Log;

            if (!Program.elevated)
            {
                throw new UnauthorizedAccessException("Access is denied.");
            }

            Log.Info("Restarting the service with id '" + descriptor.Id + "'");

            // run restart from another process group. see README.md for why this is useful.

            bool result = ProcessApis.CreateProcess(null, descriptor.ExecutablePath + " restart", IntPtr.Zero, IntPtr.Zero, false, ProcessApis.CREATE_NEW_PROCESS_GROUP, IntPtr.Zero, null, default, out _);
            if (!result)
            {
                throw new Exception("Failed to invoke restart: " + Marshal.GetLastWin32Error());
            }
        }
    }
}
