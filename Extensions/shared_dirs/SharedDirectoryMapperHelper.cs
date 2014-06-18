using System;
using System.Collections.Generic;
using System.Text;

namespace winsw.extensions.shared_dirs
{
    class SharedDirectoryMappingHelper
    {
        /// <summary>
        /// Invokes a system command
        /// </summary>
        /// <see cref="SharedDirectoryMapper"/>
        /// <param name="command">Command to be executed</param>
        /// <param name="args">Command arguments</param>
        /// <exception cref="MapperException">Operation failure</exception>
        private void InvokeCommand(String command, String args)
        {
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;

            p.StartInfo.FileName = command;
            p.StartInfo.Arguments = args;
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new MapperException("Command "+command+" failed with code "+p.ExitCode);
            }
        }

        /// <summary>
        /// Maps the remote directory
        /// </summary>
        /// <param name="Label">Disk label</param>
        /// <param name="UNCPath">UNC path to the directory</param>
        /// <exception cref="MapperException">Operation failure</exception>
        public void MapDirectory(String Label, String UNCPath)
        {
            InvokeCommand("net.exe", " use " + Label + " " + UNCPath);
        }

        /// <summary>
        /// Unmaps the label
        /// </summary>
        /// <param name="Label">Disk label</param>
        /// <exception cref="MapperException">Operation failure</exception>
        public void UnmapDirectory(String Label)
        {
            InvokeCommand("net.exe", " use /DELETE /YES " + Label);
        }
    }

    class MapperException : WinSWException
    {
        public MapperException(string message) 
            : base(message)
        { }
    }
}
