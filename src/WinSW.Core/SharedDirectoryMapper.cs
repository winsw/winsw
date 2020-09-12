using System.Collections.Generic;
using WinSW.Native;
using static WinSW.Native.NetworkApis;

namespace WinSW
{
    public sealed class SharedDirectoryMapper
    {
        private readonly List<SharedDirectoryMapperConfig> entries;

        public SharedDirectoryMapper(List<SharedDirectoryMapperConfig> entries)
        {
            this.entries = entries;
        }

        public void Map()
        {
            foreach (var config in this.entries)
            {
                string label = config.Label;
                string uncPath = config.UncPath;

                int error = WNetAddConnection2W(new()
                {
                    Type = RESOURCETYPE_DISK,
                    LocalName = label,
                    RemoteName = uncPath,
                });
                if (error != 0)
                {
                    Throw.Command.Win32Exception(error, $"Failed to map {label}.");
                }
            }
        }

        public void Unmap()
        {
            foreach (var config in this.entries)
            {
                string label = config.Label;

                int error = WNetCancelConnection2W(label);
                if (error != 0)
                {
                    Throw.Command.Win32Exception(error, $"Failed to unmap {label}.");
                }
            }
        }
    }
}
