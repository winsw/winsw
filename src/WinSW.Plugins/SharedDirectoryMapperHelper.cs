using System.Diagnostics;

namespace WinSW.Plugins
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
        private void InvokeCommand(string command, string args)
        {
            var p = new Process
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
        public void MapDirectory(string label, string uncPath)
        {
            this.InvokeCommand("net.exe", " use " + label + " " + uncPath);
        }

        /// <summary>
        /// Unmaps the label
        /// </summary>
        /// <param name="label">Disk label</param>
        /// <exception cref="MapperException">Operation failure</exception>
        public void UnmapDirectory(string label)
        {
            this.InvokeCommand("net.exe", " use /DELETE /YES " + label);
        }
    }

    class MapperException : WinSWException
    {
        public string Call { get; private set; }
        public Process Process { get; private set; }

        public MapperException(Process process, string command, string args)
            : base("Command " + command + " " + args + " failed with code " + process.ExitCode)
        {
            this.Call = command + " " + args;
            this.Process = process;
        }
    }
}
