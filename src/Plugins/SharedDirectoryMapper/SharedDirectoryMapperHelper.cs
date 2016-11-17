using System;
using System.Diagnostics;
using winsw.Util;

namespace winsw.Plugins.SharedDirectoryMapper
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
            Process p = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    FileName = command,
                    Arguments = args
                }
            };

            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new MapperException(p, command, args);
            }
        }

        /// <summary>
        /// Maps the remote directory
        /// </summary>
        /// <param name="label">Disk label</param>
        /// <param name="uncPath">UNC path to the directory</param>
        /// <exception cref="MapperException">Operation failure</exception>
        public void MapDirectory(String label, String uncPath)
        {
            InvokeCommand("net.exe", " use " + label + " " + uncPath);
        }

        /// <summary>
        /// Unmaps the label
        /// </summary>
        /// <param name="label">Disk label</param>
        /// <exception cref="MapperException">Operation failure</exception>
        public void UnmapDirectory(String label)
        {
            InvokeCommand("net.exe", " use /DELETE /YES " + label);
        }
    }

    class MapperException : WinSWException
    {
        public String Call { get; private set; }
        public Process Process { get; private set; }

        public MapperException(Process process, string command, string args)
            : base("Command " + command + " " + args + " failed with code " + process.ExitCode)
        {
            Call = command + " " + args;
            Process = process;
        }
    }
}
