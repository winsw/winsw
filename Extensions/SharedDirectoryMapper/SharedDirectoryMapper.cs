using System;
using System.Collections.Generic;
using System.Text;

namespace winsw.Extensions.SharedDirectoryMapper
{
    class SharedDirectoryMapper
    {
        public void InvokeCommand(String filename, String args)
        {
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;

            p.StartInfo.FileName = filename;
            p.StartInfo.Arguments = args;
            p.Start();
            p.WaitForExit();
        }

        public void MountDirectory(String Label, String UNCPath)
        {
            InvokeCommand("net.exe", " use " + Label + " " + UNCPath);
        }

        public void UnmountDirectory(String Label)
        {
            InvokeCommand("net.exe", " use /DELETE /YES " + Label);
        }
    }
}
